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

    /// <summary>
    /// What to write: only elements whose value changes, never an empty value.
    /// </summary>
    public sealed class WritePlan
    {
        public WritePlan(DocumentKey document, long snapshotGeneration, string categoryKey,
            string categoryDisplayName, IReadOnlyList<WriteItem> items)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            SnapshotGeneration = snapshotGeneration;
            CategoryKey = categoryKey;
            CategoryDisplayName = categoryDisplayName;
            Items = DtoCollections.CopyList(items);

            var ids = new HashSet<long>();
            foreach (WriteItem item in Items)
            {
                if (item == null)
                {
                    throw new ArgumentException("Write items must not contain null.", nameof(items));
                }

                if (ValueComparer.Normalize(item.NewValue).Length == 0)
                {
                    throw new ArgumentException("An empty value is never written: element " + item.Id + ".",
                        nameof(items));
                }

                if (ValueComparer.AreEqual(item.NewValue, item.ExpectedCurrentValue))
                {
                    throw new ArgumentException("The value does not change: element " + item.Id + ".",
                        nameof(items));
                }

                if (!ids.Add(item.Id))
                {
                    throw new ArgumentException("Element ids must be unique: " + item.Id + ".", nameof(items));
                }
            }
        }

        public DocumentKey Document { get; }

        /// <summary>Nothing is written when the document generation differs.</summary>
        public long SnapshotGeneration { get; }

        public string CategoryKey { get; }

        public string CategoryDisplayName { get; }

        public IReadOnlyList<WriteItem> Items { get; }

        /// <summary>
        /// Write plan from the preview: WillChange and Conflict rows in plan.Rows order,
        /// ExpectedCurrentValue is the snapshot value of the element.
        /// </summary>
        public static WritePlan Create(DocumentKey document, CategorySnapshot snapshot, NumberingPlan plan)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (!document.Equals(snapshot.Document))
            {
                throw new ArgumentException("The snapshot belongs to another document.", nameof(snapshot));
            }

            if (plan.SnapshotGeneration != snapshot.Generation)
            {
                throw new ArgumentException("The plan was computed from another snapshot generation.",
                    nameof(plan));
            }

            var currentValues = new Dictionary<long, string>();
            foreach (ElementData element in snapshot.Elements)
            {
                currentValues[element.Id] = element.CurrentValue;
            }

            var items = new List<WriteItem>();
            foreach (PlannedRow row in plan.Rows)
            {
                if (row.Status != RowStatus.WillChange && row.Status != RowStatus.Conflict)
                {
                    continue;
                }

                if (!currentValues.TryGetValue(row.Id, out string currentValue))
                {
                    throw new ArgumentException("The snapshot has no element " + row.Id + ".", nameof(plan));
                }

                items.Add(new WriteItem(row.Id, currentValue, row.NewValue));
            }

            return new WritePlan(document, plan.SnapshotGeneration, snapshot.Category?.Key,
                snapshot.Category?.DisplayName, items);
        }
    }
}
