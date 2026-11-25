using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Numbering
{
    public enum RowStatus
    {
        WillChange,
        NoChange,
        Locked,
        OutOfScope,
    }

    public sealed class PreviewRow
    {
        public NumberingItem Item { get; set; }

        /// <summary>"" for locked and out-of-scope elements and while the template has errors.</summary>
        public string NewValue { get; set; }

        public RowStatus Status { get; set; }

        public LockReason Lock { get; set; }
    }

    /// <summary>A number the counter stepped over because an element holds it.</summary>
    public sealed class SkippedNumber
    {
        public string Value { get; set; }

        public long OccupiedById { get; set; }

        /// <summary>The level whose counter stepped over the number; null: no level.</summary>
        public long? LevelId { get; set; }

        /// <summary>The counter value: lets the window fold neighbours into a range.</summary>
        public int Number { get; set; }
    }

    /// <summary>Output of the calculation: what the window shows before Apply.</summary>
    public sealed class Preview
    {
        /// <summary>One row per element, in the order of the input.</summary>
        public List<PreviewRow> Rows { get; set; } = new List<PreviewRow>();

        /// <summary>Block Apply.</summary>
        public List<Problem> Errors { get; set; } = new List<Problem>();

        public List<Problem> Warnings { get; set; } = new List<Problem>();

        public List<SkippedNumber> Skipped { get; set; } = new List<SkippedNumber>();

        public int ChangeCount => Rows.Count(row => row.Status == RowStatus.WillChange);

        public int LockedCount => Rows.Count(row => row.Status == RowStatus.Locked);

        public bool CanApply => Errors.Count == 0 && ChangeCount > 0;

        /// <summary>The same composition: every element has the same status and the same new value.</summary>
        public bool SameAs(Preview other)
        {
            if (other == null || other.Rows.Count != Rows.Count)
            {
                return false;
            }

            var otherRows = new Dictionary<long, PreviewRow>(other.Rows.Count);
            foreach (PreviewRow row in other.Rows)
            {
                otherRows[row.Item.Id] = row;
            }

            foreach (PreviewRow row in Rows)
            {
                if (!otherRows.TryGetValue(row.Item.Id, out PreviewRow otherRow)
                    || otherRow.Status != row.Status
                    || !string.Equals(otherRow.NewValue, row.NewValue, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Only the rows that will change.</summary>
        public List<WriteItem> ToWrite()
        {
            return Rows
                .Where(row => row.Status == RowStatus.WillChange)
                .Select(row => new WriteItem { Id = row.Item.Id, Value = row.NewValue })
                .ToList();
        }
    }
}
