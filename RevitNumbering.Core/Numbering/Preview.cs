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

    // One element with its new value and status.
    public sealed class PreviewRow
    {
        public NumberingItem Item { get; set; }

        // Empty for locked and out-of-scope rows, and when the numbering has errors.
        public string NewValue { get; set; }

        public RowStatus Status { get; set; }

        public LockReason Lock { get; set; }
    }

    // A number the counter skipped because another element holds it.
    public sealed class SkippedNumber
    {
        public string Value { get; set; }

        // The holder. When several elements hold the number, the one with the smallest id.
        public long OccupiedById { get; set; }
    }

    // The result of the calculation. The window shows it before Apply.
    public sealed class Preview
    {
        public List<PreviewRow> Rows { get; set; } = new List<PreviewRow>();

        // Any error blocks Apply.
        public List<Problem> Errors { get; set; } = new List<Problem>();

        // The largest number when it is longer than the counter width. Null when all numbers fit.
        public int? NumberTooWide { get; set; }

        public List<SkippedNumber> Skipped { get; set; } = new List<SkippedNumber>();

        public int ChangeCount => Rows.Count(row => row.Status == RowStatus.WillChange);

        public int LockedCount => Rows.Count(row => row.Status == RowStatus.Locked);

        public bool CanApply => Errors.Count == 0 && ChangeCount > 0;

        // True when every element has the same status and the same new value.
        // Apply uses it to check that the model did not change.
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

        // Only the rows that will change.
        public List<WriteItem> ToWrite()
        {
            return Rows
                .Where(row => row.Status == RowStatus.WillChange)
                .Select(row => new WriteItem { Id = row.Item.Id, Value = row.NewValue })
                .ToList();
        }
    }
}
