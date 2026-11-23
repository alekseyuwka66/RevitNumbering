using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;

namespace RevitNumbering.Core.Writing
{
    public sealed class WriteItem
    {
        public WriteItem(long id, string expectedCurrentValue, string newValue)
        {
            Id = id;
            ExpectedCurrentValue = expectedCurrentValue ?? string.Empty;
            NewValue = newValue ?? string.Empty;
        }

        public long Id { get; }

        public string ExpectedCurrentValue { get; }

        public string NewValue { get; }
    }

    public sealed class WritePlan
    {
        public WritePlan(DocumentKey document, long snapshotGeneration, string categoryKey,
            string categoryDisplayName, IReadOnlyList<WriteItem> items)
        {
            Document = document;
            SnapshotGeneration = snapshotGeneration;
            CategoryKey = categoryKey;
            CategoryDisplayName = categoryDisplayName;
            Items = DtoCollections.CopyList(items);
        }

        public DocumentKey Document { get; }

        /// <summary>Nothing is written when the document generation differs.</summary>
        public long SnapshotGeneration { get; }

        public string CategoryKey { get; }

        public string CategoryDisplayName { get; }

        public IReadOnlyList<WriteItem> Items { get; }

        /// <summary>Write plan from the preview: WillChange and Conflict rows in plan.Rows order.</summary>
        public static WritePlan Create(DocumentKey document, CategorySnapshot snapshot, NumberingPlan plan)
        {
            throw new NotImplementedException();
        }
    }
}
