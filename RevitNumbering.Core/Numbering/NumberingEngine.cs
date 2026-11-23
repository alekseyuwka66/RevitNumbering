using RevitNumbering.Core.Model;
using RevitNumbering.Core.Ordering;
using RevitNumbering.Core.Templates;
using RevitNumbering.Core.Texts;

namespace RevitNumbering.Core.Numbering
{
    /// <summary>
    /// Builds the numbering plan: a pure function of the snapshot, the scope and the settings.
    /// Keeps no state between calls: the same input gives the same plan.
    /// </summary>
    public static class NumberingEngine
    {
        /// <summary>How a snapshot element takes part in the calculation.</summary>
        private enum Role
        {
            OutOfScope,
            Fixed,
            Kept,
            Participant
        }

        public static NumberingPlan Compute(NumberingInput input, TraversalRegistry traversal)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (traversal == null)
            {
                throw new ArgumentNullException(nameof(traversal));
            }

            if (input.Snapshot == null)
            {
                throw new ArgumentNullException(nameof(input), "Snapshot must not be null.");
            }

            if (input.LevelCode == null)
            {
                throw new ArgumentNullException(nameof(input), "LevelCode must not be null.");
            }

            if (input.Order == null)
            {
                throw new ArgumentNullException(nameof(input), "Order must not be null.");
            }

            return new Calculation(input, traversal).Run();
        }

        /// <summary>
        /// Composition comparison: id sets of WillChange / Fixed / Conflict rows, new values
        /// of WillChange and Conflict rows and the list of skipped numbers. Nothing else is compared.
        /// </summary>
        public static bool SameComposition(NumberingPlan shown, NumberingPlan fresh)
        {
            if (shown == null)
            {
                throw new ArgumentNullException(nameof(shown));
            }

            if (fresh == null)
            {
                throw new ArgumentNullException(nameof(fresh));
            }

            if (shown.SkippedNumbers.Count != fresh.SkippedNumbers.Count)
            {
                return false;
            }

            for (int i = 0; i < shown.SkippedNumbers.Count; i++)
            {
                SkippedNumber a = shown.SkippedNumbers[i];
                SkippedNumber b = fresh.SkippedNumbers[i];
                if (a.OccupiedById != b.OccupiedById || !string.Equals(a.Value, b.Value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            Dictionary<long, PlannedRow> shownRows = CompositionRows(shown);
            Dictionary<long, PlannedRow> freshRows = CompositionRows(fresh);
            if (shownRows.Count != freshRows.Count)
            {
                return false;
            }

            foreach (KeyValuePair<long, PlannedRow> pair in shownRows)
            {
                if (!freshRows.TryGetValue(pair.Key, out PlannedRow other) || other.Status != pair.Value.Status)
                {
                    return false;
                }

                if (pair.Value.Status != RowStatus.Fixed
                    && !string.Equals(pair.Value.NewValue, other.NewValue, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Rows that make the composition: WillChange, Fixed and Conflict, by id.</summary>
        private static Dictionary<long, PlannedRow> CompositionRows(NumberingPlan plan)
        {
            var rows = new Dictionary<long, PlannedRow>();
            foreach (PlannedRow row in plan.Rows)
            {
                if (row.Status == RowStatus.WillChange || row.Status == RowStatus.Fixed
                    || row.Status == RowStatus.Conflict)
                {
                    rows[row.Id] = row;
                }
            }

            return rows;
        }

        /// <summary>Normalized value within its conflict area: the phase for ConflictScope.Phase.</summary>
        private readonly struct ValueKey : IEquatable<ValueKey>
        {
            private readonly string _phase;
            private readonly string _value;

            public ValueKey(string phase, string value)
            {
                _phase = phase;
                _value = value;
            }

            public bool Equals(ValueKey other)
            {
                return string.Equals(_value, other._value, StringComparison.Ordinal)
                    && string.Equals(_phase, other._phase, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is ValueKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                int hash = StringComparer.Ordinal.GetHashCode(_value);
                return _phase == null ? hash : unchecked((hash * 397) ^ StringComparer.Ordinal.GetHashCode(_phase));
            }
        }

        /// <summary>Participants of one level; Level is null for the "no level" group.</summary>
        private sealed class LevelGroup
        {
            public LevelGroup(LevelData level)
            {
                Level = level;
            }

            public LevelData Level { get; }

            public List<int> Participants { get; } = new List<int>();
        }

        /// <summary>One call of Compute. Elements are addressed by their index in the snapshot.</summary>
        private sealed class Calculation
        {
            private const int NoLevelRank = int.MaxValue;

            private readonly NumberingInput _input;
            private readonly TraversalRegistry _traversal;
            private readonly CategorySnapshot _snapshot;
            private readonly bool _byPhase;
            private readonly int _count;

            private readonly ElementData[] _elements;
            private readonly Role[] _roles;
            private readonly FixReason[] _reasons;
            private readonly string[] _newValues;
            private readonly RowStatus[] _statuses;

            private readonly Dictionary<string, LevelData> _levels = new Dictionary<string, LevelData>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> _levelRanks = new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly Dictionary<long, int> _indexById = new Dictionary<long, int>();

            private readonly List<BlockingError> _errors = new List<BlockingError>();
            private readonly List<TemplateIssue> _warnings = new List<TemplateIssue>();
            private readonly List<SkippedNumber> _skipped = new List<SkippedNumber>();
            private readonly List<DuplicateValue> _duplicates = new List<DuplicateValue>();

            /// <summary>Occupied values: the smallest id among external and internal holders.</summary>
            private readonly Dictionary<ValueKey, long> _occupied = new Dictionary<ValueKey, long>();

            /// <summary>Ids of out-of-scope elements by value.</summary>
            private readonly Dictionary<ValueKey, List<long>> _external = new Dictionary<ValueKey, List<long>>();

            /// <summary>Participants in numbering order: groups by level, then the traversal order.</summary>
            private readonly List<int> _sequence = new List<int>();

            private NumberTemplate _template;
            private int _maxCounter;

            public Calculation(NumberingInput input, TraversalRegistry traversal)
            {
                _input = input;
                _traversal = traversal;
                _snapshot = input.Snapshot;
                _byPhase = _snapshot.Category != null && _snapshot.Category.ConflictScope == ConflictScope.Phase;
                _count = _snapshot.Elements.Count;
                _elements = new ElementData[_count];
                _roles = new Role[_count];
                _reasons = new FixReason[_count];
                _newValues = new string[_count];
                _statuses = new RowStatus[_count];
            }

            public NumberingPlan Run()
            {
                ParseTemplate();
                IndexLevels();
                AssignRoles();
                CheckTemplateAgainstScope();

                if (_errors.Count == 0)
                {
                    BuildOccupiedIndex();
                    AssignNumbers();
                }

                AssignStatuses();

                if (_errors.Count == 0)
                {
                    CollectDuplicates();
                    CollectDuplicatesInScope();
                    TemplateIssue width = TemplateScopeCheck.CheckWidth(_template, _maxCounter);
                    if (width != null)
                    {
                        _warnings.Add(width);
                    }
                }

                var rows = new PlannedRow[_count];
                for (int i = 0; i < _count; i++)
                {
                    bool owned = _reasons[i] == FixReason.OwnedByOtherUser;
                    rows[i] = new PlannedRow(_elements[i].Id, _newValues[i], _statuses[i], _reasons[i],
                        owned ? _elements[i].OwnerName : null);
                }

                return new NumberingPlan(_snapshot.Generation, rows, _skipped, _duplicates, _warnings, _errors);
            }

            /// <summary>Step 1: every template error blocks; without a valid template there is none.</summary>
            private void ParseTemplate()
            {
                TemplateParseResult parsed = TemplateParser.Parse(_input.TemplateText);
                foreach (TemplateIssue issue in parsed.Issues)
                {
                    if (!issue.IsWarning)
                    {
                        _errors.Add(new BlockingError(issue, null, null));
                    }
                }

                _template = parsed.IsValid ? parsed.Template : null;
            }

            /// <summary>Levels by key (the first wins) and their order: elevation, name, key.</summary>
            private void IndexLevels()
            {
                foreach (LevelData level in _snapshot.Levels)
                {
                    if (level?.Key != null && !_levels.ContainsKey(level.Key))
                    {
                        _levels.Add(level.Key, level);
                    }
                }

                int rank = 0;
                foreach (LevelData level in _levels.Values
                    .OrderBy(l => l.ElevationMm)
                    .ThenBy(l => l.Name, StringComparer.Ordinal)
                    .ThenBy(l => l.Key, StringComparer.Ordinal))
                {
                    _levelRanks[level.Key] = rank++;
                }
            }

            /// <summary>Working copies (unknown level key becomes null), fix classification, level groups.</summary>
            private void AssignRoles()
            {
                // Without a valid template there is no level placeholder for Classify.
                bool usesLevel = _template != null && _template.UsesLevel;
                var scope = new HashSet<long>(_input.ScopeIds);

                for (int i = 0; i < _count; i++)
                {
                    ElementData element = WithKnownLevel(_snapshot.Elements[i]);
                    _elements[i] = element;
                    if (!_indexById.ContainsKey(element.Id))
                    {
                        _indexById.Add(element.Id, i);
                    }

                    if (!scope.Contains(element.Id))
                    {
                        _roles[i] = Role.OutOfScope;
                        continue;
                    }

                    FixReason reason = FixClassifier.Classify(element, _snapshot.Category, usesLevel);
                    _reasons[i] = reason;
                    if (reason != FixReason.None)
                    {
                        _roles[i] = Role.Fixed;
                    }
                    else if (_input.AppendMode && ValueComparer.Normalize(element.CurrentValue).Length > 0)
                    {
                        // Append mode numbers only elements with an empty value.
                        _roles[i] = Role.Kept;
                    }
                    else
                    {
                        _roles[i] = Role.Participant;
                    }
                }
            }

            private ElementData WithKnownLevel(ElementData element)
            {
                if (element.LevelKey == null || _levels.ContainsKey(element.LevelKey))
                {
                    return element;
                }

                return new ElementData(element.Id, element.Name, null, element.LevelName, element.CurrentValue,
                    element.Xmm, element.Ymm, element.PhaseKey, element.IsParameterReadOnly,
                    element.IsOwnedByOtherUser, element.OwnerName, element.HasLocation, element.AreaSqM,
                    element.HasBoundaries);
            }

            /// <summary>Template checks against the scope; the checks themselves belong to the template module.</summary>
            private void CheckTemplateAgainstScope()
            {
                if (_template == null)
                {
                    return;
                }

                var scopeLevelKeys = new List<string>();
                var codeLevels = new List<LevelData>();
                var seenLevels = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < _count; i++)
                {
                    if (_roles[i] == Role.OutOfScope)
                    {
                        continue;
                    }

                    string key = _elements[i].LevelKey;
                    scopeLevelKeys.Add(key);
                    if (_roles[i] != Role.Fixed && key != null && seenLevels.Add(key))
                    {
                        codeLevels.Add(_levels[key]);
                    }
                }

                AddTemplateError(TemplateScopeCheck.CheckLevelCount(_template, scopeLevelKeys, _snapshot.Levels));
                AddTemplateError(TemplateScopeCheck.CheckLevelCodes(_template, _input.LevelCode, codeLevels));
            }

            private void AddTemplateError(TemplateIssue issue)
            {
                if (issue != null)
                {
                    _errors.Add(new BlockingError(issue, null, null));
                }
            }

            /// <summary>
            /// Non-empty current values of out-of-scope (external) and of fixed and kept (internal)
            /// elements. Current values of participants are not occupied: swapping numbers is not a conflict.
            /// </summary>
            private void BuildOccupiedIndex()
            {
                for (int i = 0; i < _count; i++)
                {
                    if (_roles[i] == Role.Participant)
                    {
                        continue;
                    }

                    ElementData element = _elements[i];
                    string value = ValueComparer.Normalize(element.CurrentValue);
                    if (value.Length == 0)
                    {
                        continue;
                    }

                    var key = new ValueKey(PhaseOf(element), value);
                    if (!_occupied.TryGetValue(key, out long holder) || element.Id < holder)
                    {
                        _occupied[key] = element.Id;
                    }

                    if (_roles[i] == Role.OutOfScope)
                    {
                        if (!_external.TryGetValue(key, out List<long> ids))
                        {
                            ids = new List<long>();
                            _external.Add(key, ids);
                        }

                        ids.Add(element.Id);
                    }
                }
            }

            private string PhaseOf(ElementData element)
            {
                return _byPhase ? element.PhaseKey : null;
            }

            /// <summary>A separate counter per level group; occupied candidates are skipped on request.</summary>
            private void AssignNumbers()
            {
                List<LevelGroup> groups = BuildGroups();
                Dictionary<string, List<int>> byLevel = _input.AppendMode ? IndexAllByLevel() : null;
                List<int> withoutLevel = _input.AppendMode ? IndexWithoutLevel() : null;
                ITraversalStrategy strategy = _traversal.Get(_input.Order.StrategyKey);
                bool skipOccupied = _input.Strategy == ConflictStrategy.SkipOccupied;

                foreach (LevelGroup group in groups)
                {
                    string code = _template.UsesLevel ? _input.LevelCode.Resolve(group.Level) : string.Empty;
                    int n = 1;
                    if (_input.AppendMode)
                    {
                        List<int> all = group.Level == null ? withoutLevel : byLevel[group.Level.Key];
                        n = MaxMatchedCounter(all, code) + 1;
                    }

                    foreach (int index in OrderGroup(group, strategy))
                    {
                        ElementData element = _elements[index];
                        string candidate = _template.Format(code, n);
                        if (skipOccupied)
                        {
                            string phase = PhaseOf(element);
                            while (_occupied.TryGetValue(new ValueKey(phase, ValueComparer.Normalize(candidate)),
                                out long holder))
                            {
                                _skipped.Add(new SkippedNumber(candidate, holder));
                                n++;
                                candidate = _template.Format(code, n);
                            }
                        }

                        _newValues[index] = candidate;
                        _sequence.Add(index);
                        if (n > _maxCounter)
                        {
                            _maxCounter = n;
                        }

                        n++;
                    }
                }
            }

            /// <summary>Groups of participants in level order; the "no level" group is the last one.</summary>
            private List<LevelGroup> BuildGroups()
            {
                var byKey = new Dictionary<string, LevelGroup>(StringComparer.Ordinal);
                LevelGroup noLevel = null;
                for (int i = 0; i < _count; i++)
                {
                    if (_roles[i] != Role.Participant)
                    {
                        continue;
                    }

                    string key = _elements[i].LevelKey;
                    LevelGroup group;
                    if (key == null)
                    {
                        group = noLevel ?? (noLevel = new LevelGroup(null));
                    }
                    else if (!byKey.TryGetValue(key, out group))
                    {
                        group = new LevelGroup(_levels[key]);
                        byKey.Add(key, group);
                    }

                    group.Participants.Add(i);
                }

                List<LevelGroup> groups = byKey.Values.OrderBy(g => _levelRanks[g.Level.Key]).ToList();
                if (noLevel != null)
                {
                    groups.Add(noLevel);
                }

                return groups;
            }

            /// <summary>All snapshot elements by level key (for the append mode).</summary>
            private Dictionary<string, List<int>> IndexAllByLevel()
            {
                var result = new Dictionary<string, List<int>>(StringComparer.Ordinal);
                for (int i = 0; i < _count; i++)
                {
                    string key = _elements[i].LevelKey;
                    if (key == null)
                    {
                        continue;
                    }

                    if (!result.TryGetValue(key, out List<int> list))
                    {
                        list = new List<int>();
                        result.Add(key, list);
                    }

                    list.Add(i);
                }

                return result;
            }

            private List<int> IndexWithoutLevel()
            {
                var result = new List<int>();
                for (int i = 0; i < _count; i++)
                {
                    if (_elements[i].LevelKey == null)
                    {
                        result.Add(i);
                    }
                }

                return result;
            }

            /// <summary>
            /// The largest N among values of the level group that match the template: all snapshot elements,
            /// in the scope or not, of any phase. 0 when nothing matches.
            /// </summary>
            private int MaxMatchedCounter(List<int> levelElements, string code)
            {
                int max = 0;
                foreach (int index in levelElements)
                {
                    string value = ValueComparer.Normalize(_elements[index].CurrentValue);
                    if (value.Length > 0 && _template.TryMatch(value, code, out int counter) && counter > max)
                    {
                        max = counter;
                    }
                }

                return max;
            }

            /// <summary>
            /// Order inside a group: the strategy orders participants that have a point (one call per group);
            /// participants without a point follow by ascending id.
            /// </summary>
            private List<int> OrderGroup(LevelGroup group, ITraversalStrategy strategy)
            {
                var items = new List<OrderItem>(group.Participants.Count);
                var rest = new List<int>();
                var pending = new HashSet<int>();
                foreach (int index in group.Participants)
                {
                    ElementData element = _elements[index];
                    if (element.Xmm.HasValue && element.Ymm.HasValue)
                    {
                        items.Add(new OrderItem(element.Id, element.Xmm.Value, element.Ymm.Value));
                        pending.Add(index);
                    }
                    else
                    {
                        rest.Add(index);
                    }
                }

                var ordered = new List<int>(group.Participants.Count);
                if (items.Count > 0)
                {
                    IReadOnlyList<long> ids = strategy.Order(items, _input.Order.Parameters, _input.RowToleranceMm);
                    if (ids != null)
                    {
                        foreach (long id in ids)
                        {
                            if (_indexById.TryGetValue(id, out int index) && pending.Remove(index))
                            {
                                ordered.Add(index);
                            }
                        }
                    }

                    // A strategy that lost an element must not lose its number: such elements go with the rest.
                    rest.AddRange(pending);
                }

                rest.Sort((a, b) => _elements[a].Id.CompareTo(_elements[b].Id));
                ordered.AddRange(rest);
                return ordered;
            }

            /// <summary>The first matching line of the status table.</summary>
            private void AssignStatuses()
            {
                bool allowDuplicates = _input.Strategy == ConflictStrategy.AllowDuplicates;
                for (int i = 0; i < _count; i++)
                {
                    switch (_roles[i])
                    {
                        case Role.OutOfScope:
                            _statuses[i] = RowStatus.OutOfScope;
                            break;
                        case Role.Fixed:
                            _statuses[i] = RowStatus.Fixed;
                            break;
                        case Role.Kept:
                            _statuses[i] = RowStatus.Unchanged;
                            break;
                        default:
                            _statuses[i] = ParticipantStatus(i, allowDuplicates);
                            break;
                    }
                }
            }

            private RowStatus ParticipantStatus(int index, bool allowDuplicates)
            {
                string newValue = _newValues[index];
                if (newValue == null)
                {
                    // Numbers are not built while a blocking template error exists.
                    return RowStatus.Unchanged;
                }

                ElementData element = _elements[index];
                if (ValueComparer.AreEqual(newValue, element.CurrentValue))
                {
                    return RowStatus.Unchanged;
                }

                if (allowDuplicates && _external.ContainsKey(KeyOfNewValue(index)))
                {
                    return RowStatus.Conflict;
                }

                return RowStatus.WillChange;
            }

            private ValueKey KeyOfNewValue(int index)
            {
                return new ValueKey(PhaseOf(_elements[index]), ValueComparer.Normalize(_newValues[index]));
            }

            /// <summary>
            /// One record per value of Conflict rows, in the order of the first such row; ids are
            /// the Conflict rows and every external element with this value in the same area.
            /// </summary>
            private void CollectDuplicates()
            {
                if (_input.Strategy != ConflictStrategy.AllowDuplicates)
                {
                    return;
                }

                var order = new List<ValueKey>();
                var values = new Dictionary<ValueKey, string>();
                var ids = new Dictionary<ValueKey, List<long>>();
                foreach (int index in _sequence)
                {
                    if (_statuses[index] != RowStatus.Conflict)
                    {
                        continue;
                    }

                    ValueKey key = KeyOfNewValue(index);
                    if (!ids.TryGetValue(key, out List<long> list))
                    {
                        list = new List<long>(_external[key]);
                        ids.Add(key, list);
                        values.Add(key, _newValues[index]);
                        order.Add(key);
                    }

                    list.Add(_elements[index].Id);
                }

                foreach (ValueKey key in order)
                {
                    ids[key].Sort();
                    _duplicates.Add(new DuplicateValue(values[key], ids[key]));
                }
            }

            /// <summary>
            /// A value that two or more scope elements have in one area, at least one of them
            /// a participant, blocks Apply. Participants give the new value, fixed and kept
            /// elements give the current one; empty values do not count.
            /// </summary>
            private void CollectDuplicatesInScope()
            {
                var holders = new Dictionary<ValueKey, List<int>>();
                for (int i = 0; i < _count; i++)
                {
                    if (_roles[i] == Role.OutOfScope)
                    {
                        continue;
                    }

                    ElementData element = _elements[i];
                    string value = ValueComparer.Normalize(
                        _roles[i] == Role.Participant ? _newValues[i] : element.CurrentValue);
                    if (value.Length == 0)
                    {
                        continue;
                    }

                    var key = new ValueKey(PhaseOf(element), value);
                    if (!holders.TryGetValue(key, out List<int> list))
                    {
                        list = new List<int>(1);
                        holders.Add(key, list);
                    }

                    list.Add(i);
                }

                var reported = new HashSet<ValueKey>();
                foreach (int index in _sequence)
                {
                    ValueKey key = KeyOfNewValue(index);
                    List<int> list = holders[key];
                    if (list.Count < 2 || !reported.Add(key))
                    {
                        continue;
                    }

                    List<long> ids = list.Select(i => _elements[i].Id).OrderBy(id => id).ToList();
                    List<string> levelNames = list
                        .Select(i => _elements[i].LevelKey)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(RankOf)
                        .Select(NameOf)
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                    _errors.Add(new BlockingError(null, new DuplicateValue(_newValues[index], ids), levelNames));
                }
            }

            private int RankOf(string levelKey)
            {
                return levelKey == null ? NoLevelRank : _levelRanks[levelKey];
            }

            private string NameOf(string levelKey)
            {
                return levelKey == null ? TextCatalog.NoLevelGroup : _levels[levelKey].Name;
            }
        }
    }
}
