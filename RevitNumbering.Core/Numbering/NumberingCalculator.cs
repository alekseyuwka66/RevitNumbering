using System.Globalization;
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Rules;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Core.Numbering
{
    /// <summary>
    /// Builds the preview: a pure function of the elements, the scope and the settings.
    /// The same input gives the same result.
    /// </summary>
    public static class NumberingCalculator
    {
        // Lock rules; the order is the priority, the first reason wins.
        private static readonly List<ItemRule> Rules = new List<ItemRule>
        {
            new RevitLockRule(),
            new NoLevelRule(),
            new OrderingRule(),
        };

        public static Preview Calculate(NumberingRequest request)
        {
            var preview = new Preview();

            // 1. Template: with errors every new value stays empty.
            TemplateResult parsed = Template.Parse(request.Settings.Template);
            request.Template = parsed.Template;
            if (parsed.Template == null)
            {
                preview.Errors.AddRange(parsed.Problems);
                foreach (NumberingItem item in request.Items)
                {
                    preview.Rows.Add(new PreviewRow
                    {
                        Item = item,
                        NewValue = string.Empty,
                        Status = request.Scope.Contains(item.Id) ? RowStatus.NoChange : RowStatus.OutOfScope,
                    });
                }

                return preview;
            }

            // 2. Roles: out of scope, locked, participant.
            var participants = new List<PreviewRow>();
            foreach (NumberingItem item in request.Items)
            {
                var row = new PreviewRow { Item = item, NewValue = string.Empty };
                preview.Rows.Add(row);
                if (!request.Scope.Contains(item.Id))
                {
                    row.Status = RowStatus.OutOfScope;
                    continue;
                }

                row.Lock = FindLock(item, request);
                if (row.Lock != LockReason.None)
                {
                    row.Status = RowStatus.Locked;
                    continue;
                }

                row.Status = RowStatus.NoChange;
                participants.Add(row);
            }

            // 3. Levels: groups of participants and their level codes.
            List<LevelGroup> groups = GroupByLevel(participants, request);
            CheckLevels(groups, request, preview);
            if (preview.Errors.Count > 0)
            {
                return preview;
            }

            // 4. Occupied numbers: values of out-of-scope and locked elements.
            Dictionary<(string, string), long> occupied = FindOccupied(preview.Rows);

            // 5-6. Order inside each level and numbers; the counter of every level starts from 1.
            Dictionary<long, PreviewRow> rowById = participants.ToDictionary(row => row.Item.Id);
            var numbered = new List<PreviewRow>(participants.Count);
            int maxNumber = 0;
            foreach (LevelGroup group in groups)
            {
                int number = 1;
                foreach (NumberingItem item in Order(group, request))
                {
                    string candidate = request.Template.Format(group.Code, number);
                    while (occupied.TryGetValue(Key(item, candidate), out long holder))
                    {
                        // An occupied number is skipped: the element gets the next free one.
                        preview.Skipped.Add(new SkippedNumber { Value = candidate, OccupiedById = holder });
                        number++;
                        candidate = request.Template.Format(group.Code, number);
                    }

                    PreviewRow row = rowById[item.Id];
                    row.NewValue = candidate;
                    row.Status = ValueComparer.AreEqual(candidate, item.Value)
                        ? RowStatus.NoChange
                        : RowStatus.WillChange;
                    numbered.Add(row);
                    maxNumber = Math.Max(maxNumber, number);
                    number++;
                }
            }

            // 7. Checks: repeats inside the scope and the counter width.
            CheckDuplicates(numbered, request, preview);
            if (request.Template.Width > 0
                && maxNumber.ToString(CultureInfo.InvariantCulture).Length > request.Template.Width)
            {
                preview.Warnings.Add(new Problem(ProblemCode.WidthTooSmall,
                    maxNumber.ToString(CultureInfo.InvariantCulture),
                    request.Template.Width.ToString(CultureInfo.InvariantCulture)));
            }

            return preview;
        }

        private static LockReason FindLock(NumberingItem item, NumberingRequest request)
        {
            foreach (ItemRule rule in Rules)
            {
                LockReason reason = rule.Check(item, request);
                if (reason != LockReason.None)
                {
                    return reason;
                }
            }

            return LockReason.None;
        }

        /// <summary>Groups in numbering order: by elevation, then by name; "no level" is the last one.</summary>
        private static List<LevelGroup> GroupByLevel(List<PreviewRow> participants, NumberingRequest request)
        {
            var groups = new List<LevelGroup>();
            LevelGroup noLevel = null;
            foreach (PreviewRow row in participants)
            {
                LevelInfo level = request.FindLevel(row.Item.LevelId);
                LevelGroup group;
                if (level == null)
                {
                    group = noLevel ?? (noLevel = new LevelGroup());
                }
                else
                {
                    group = groups.Find(g => g.Level == level);
                    if (group == null)
                    {
                        group = new LevelGroup { Level = level };
                        groups.Add(group);
                    }
                }

                group.Items.Add(row.Item);
            }

            groups = groups
                .OrderBy(g => g.Level.ElevationMm)
                .ThenBy(g => g.Level.Name, StringComparer.Ordinal)
                .ThenBy(g => g.Level.Id)
                .ToList();
            if (noLevel != null)
            {
                groups.Add(noLevel);
            }

            return groups;
        }

        /// <summary>Level codes of the groups; several levels need the level placeholder.</summary>
        private static void CheckLevels(List<LevelGroup> groups, NumberingRequest request, Preview preview)
        {
            if (!request.Template.HasLevel)
            {
                if (groups.Count > 1)
                {
                    preview.Errors.Add(new Problem(ProblemCode.NeedLevel, groups.Select(g => g.Name).ToArray()));
                }

                return;
            }

            var missing = new List<string>();
            foreach (LevelGroup group in groups)
            {
                // With the level placeholder every participant has a level (NoLevelRule).
                group.Code = request.Settings.LevelCode.Resolve(group.Level.Name);
                if (group.Code == null)
                {
                    missing.Add(group.Name);
                }
            }

            if (missing.Count > 0)
            {
                preview.Errors.Add(new Problem(ProblemCode.LevelPartMissing, missing.ToArray()));
            }
        }

        /// <summary>
        /// Non-empty values of out-of-scope and locked elements within their conflict groups; the smallest id
        /// holds the value. Current values of participants are free: swapping numbers is not a conflict.
        /// </summary>
        private static Dictionary<(string, string), long> FindOccupied(List<PreviewRow> rows)
        {
            var occupied = new Dictionary<(string, string), long>();
            foreach (PreviewRow row in rows)
            {
                if (row.Status != RowStatus.OutOfScope && row.Status != RowStatus.Locked)
                {
                    continue;
                }

                NumberingItem item = row.Item;
                if (ValueComparer.Normalize(item.Value).Length == 0)
                {
                    continue;
                }

                (string, string) key = Key(item, item.Value);
                if (!occupied.TryGetValue(key, out long holder) || item.Id < holder)
                {
                    occupied[key] = item.Id;
                }
            }

            return occupied;
        }

        private static (string, string) Key(NumberingItem item, string value)
        {
            return (item.ConflictGroup ?? string.Empty, ValueComparer.Normalize(value));
        }

        /// <summary>The strategy must return exactly the elements it got.</summary>
        private static List<NumberingItem> Order(LevelGroup group, NumberingRequest request)
        {
            List<NumberingItem> ordered = request.Settings.Ordering.Order(group.Items);
            if (ordered == null || ordered.Count != group.Items.Count
                || !new HashSet<NumberingItem>(ordered).SetEquals(group.Items))
            {
                throw new InvalidOperationException(
                    request.Settings.Ordering.GetType().Name + ".Order must return exactly the elements it got.");
            }

            return ordered;
        }

        /// <summary>
        /// The same new value given twice in one conflict group, for example when level codes coincide.
        /// One problem per set of levels, with the first repeated number as the example.
        /// </summary>
        private static void CheckDuplicates(List<PreviewRow> numbered, NumberingRequest request, Preview preview)
        {
            var byValue = new Dictionary<(string, string), List<PreviewRow>>();
            foreach (PreviewRow row in numbered)
            {
                (string, string) key = Key(row.Item, row.NewValue);
                if (!byValue.TryGetValue(key, out List<PreviewRow> rows))
                {
                    rows = new List<PreviewRow>(1);
                    byValue.Add(key, rows);
                }

                rows.Add(row);
            }

            var reported = new HashSet<string>();
            foreach (PreviewRow row in numbered)
            {
                List<PreviewRow> rows = byValue[Key(row.Item, row.NewValue)];
                if (rows.Count < 2)
                {
                    continue;
                }

                List<string> levels = rows
                    .Select(r => request.FindLevel(r.Item.LevelId)?.Name ?? string.Empty)
                    .Distinct()
                    .ToList();
                if (reported.Add(string.Join("\n", levels)))
                {
                    var args = new List<string> { row.NewValue };
                    args.AddRange(levels);
                    preview.Errors.Add(new Problem { Code = ProblemCode.DuplicateInScope, Args = args });
                }
            }
        }

        /// <summary>Participants of one level; Level is null for the "no level" group.</summary>
        private sealed class LevelGroup
        {
            public LevelInfo Level { get; set; }

            /// <summary>Level code for the template; "" without the level placeholder.</summary>
            public string Code { get; set; } = string.Empty;

            public List<NumberingItem> Items { get; } = new List<NumberingItem>();

            /// <summary>"" stands for "no level": the text belongs to the UI.</summary>
            public string Name => Level?.Name ?? string.Empty;
        }
    }
}
