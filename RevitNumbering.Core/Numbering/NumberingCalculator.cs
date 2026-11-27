using System.Globalization;
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Orderings;
using RevitNumbering.Core.Settings;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Core.Numbering
{
    // Builds the preview. It is a pure function: the same input always gives the same result.
    // The scope is the set of ids that get numbers: the visible rows, or the elements picked in the model.
    public static class NumberingCalculator
    {
        public static Preview Calculate(CategoryData data, HashSet<long> scope, CategorySettings settings)
        {
            var preview = new Preview();
            Template template = Template.From(settings);

            // 1. Roles: out of scope, locked or participant.
            var participants = new List<PreviewRow>();
            foreach (NumberingItem item in data.Items)
            {
                var row = new PreviewRow { Item = item, NewValue = string.Empty, Status = RowStatus.NoChange };
                preview.Rows.Add(row);
                if (!scope.Contains(item.Id))
                {
                    row.Status = RowStatus.OutOfScope;
                    continue;
                }

                row.Lock = FindLock(item, data, template, settings.Ordering);
                if (row.Lock != LockReason.None)
                {
                    row.Status = RowStatus.Locked;
                    continue;
                }

                participants.Add(row);
            }

            // 2. Without the level in the template, several levels would get the same numbers.
            List<LevelGroup> groups = GroupByLevel(participants, data);
            if (!template.HasLevel && groups.Count > 1)
            {
                preview.Errors.Add(new Problem(ProblemKind.NeedLevel, levels: groups.Select(g => g.Name).ToList()));
                return preview;
            }

            // 3. Numbers that out-of-scope and locked elements already use.
            Dictionary<(string Group, string Value), long> holders = FindHolders(preview.Rows);

            // 4. Order the elements inside each level. The counter starts from 1 on every level.
            Dictionary<long, PreviewRow> rowById = participants.ToDictionary(row => row.Item.Id);
            var numbered = new List<PreviewRow>(participants.Count);
            int maxNumber = 0;
            foreach (LevelGroup group in groups)
            {
                int number = 1;
                foreach (NumberingItem item in settings.Ordering.Order(group.Items))
                {
                    string candidate = template.Format(group.Name, number);
                    while (holders.TryGetValue(Key(item, candidate), out long holder))
                    {
                        // Another element uses this number: skip it and take the next free one.
                        preview.Skipped.Add(new SkippedNumber { Value = candidate, OccupiedById = holder });
                        number++;
                        candidate = template.Format(group.Name, number);
                    }

                    PreviewRow row = rowById[item.Id];
                    row.NewValue = candidate;
                    row.Status = Norm(candidate) == Norm(item.Value) ? RowStatus.NoChange : RowStatus.WillChange;
                    numbered.Add(row);
                    maxNumber = Math.Max(maxNumber, number);
                    number++;
                }
            }

            // 5. Checks: repeated numbers and the counter width.
            CheckDuplicates(numbered, data, preview);
            if (template.Width > 1 && maxNumber.ToString(CultureInfo.InvariantCulture).Length > template.Width)
            {
                preview.NumberTooWide = maxNumber;
            }

            return preview;
        }

        // Why an element of the scope keeps its value. The first reason wins.
        private static LockReason FindLock(NumberingItem item, CategoryData data, Template template, Ordering ordering)
        {
            if (item.Lock != LockReason.None)
            {
                return item.Lock;
            }

            if (template.HasLevel && data.FindLevel(item.LevelId) == null)
            {
                return LockReason.NoLevel;
            }

            return ordering.CanOrder(item) ? LockReason.None : LockReason.NotOrderable;
        }

        // Levels in numbering order: by elevation, then by name. "No level" goes last.
        private static List<LevelGroup> GroupByLevel(List<PreviewRow> participants, CategoryData data)
        {
            return participants
                .GroupBy(row => data.FindLevel(row.Item.LevelId))
                .Select(group => new LevelGroup { Level = group.Key, Items = group.Select(row => row.Item).ToList() })
                .OrderBy(group => group.Level == null)
                .ThenBy(group => group.Level?.ElevationMm ?? 0)
                .ThenBy(group => group.Name, StringComparer.Ordinal)
                .ThenBy(group => group.Level?.Id ?? 0)
                .ToList();
        }

        // Values of out-of-scope and locked elements, per conflict group. The smallest id keeps the number.
        // Current values of participants are free, so swapping numbers is not a conflict.
        private static Dictionary<(string Group, string Value), long> FindHolders(List<PreviewRow> rows)
        {
            var holders = new Dictionary<(string Group, string Value), long>();
            foreach (PreviewRow row in rows)
            {
                if ((row.Status != RowStatus.OutOfScope && row.Status != RowStatus.Locked)
                    || Norm(row.Item.Value).Length == 0)
                {
                    continue;
                }

                (string Group, string Value) key = Key(row.Item, row.Item.Value);
                if (!holders.TryGetValue(key, out long holder) || row.Item.Id < holder)
                {
                    holders[key] = row.Item.Id;
                }
            }

            return holders;
        }

        // A number is unique inside the conflict group of the element (the phase for rooms).
        private static (string Group, string Value) Key(NumberingItem item, string value)
        {
            return (item.ConflictGroup ?? string.Empty, Norm(value));
        }

        // Values are compared after trimming. Case matters.
        private static string Norm(string value)
        {
            return value?.Trim() ?? string.Empty;
        }

        // The same new number on several levels: "1" + "11" and "11" + "1" both give "111".
        // One problem per set of levels, with the first repeated number as the example.
        private static void CheckDuplicates(List<PreviewRow> numbered, CategoryData data, Preview preview)
        {
            var reported = new HashSet<string>();
            foreach (IGrouping<(string Group, string Value), PreviewRow> repeated in numbered
                .GroupBy(row => Key(row.Item, row.NewValue))
                .Where(group => group.Count() > 1))
            {
                List<string> levels = repeated
                    .Select(row => data.FindLevel(row.Item.LevelId)?.Name ?? string.Empty)
                    .Distinct()
                    .ToList();
                if (reported.Add(string.Join("\n", levels)))
                {
                    preview.Errors.Add(new Problem(ProblemKind.DuplicateNumber, repeated.First().NewValue, levels));
                }
            }
        }

        private sealed class LevelGroup
        {
            public LevelInfo Level { get; set; }

            public List<NumberingItem> Items { get; set; }

            // "" means "no level". The UI turns it into text.
            public string Name => Level?.Name ?? string.Empty;
        }
    }
}
