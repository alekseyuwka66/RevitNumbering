using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Orderings
{
    public enum Corner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    }

    // Goes by rows or by columns from one of four corners: 8 variants. Directions follow the model axes
    // (right is +X, up is +Y), not the active view.
    public sealed class GridOrdering : Ordering
    {
        public const int MaxRowToleranceMm = 100000;

        // Coordinates closer than this are equal. Then the id decides.
        private const double CoordEpsMm = 1;

        public Corner Start { get; set; } = Corner.TopLeft;

        public bool ByColumns { get; set; }

        // Elements whose coordinates across the row differ by no more than this are in one row (or column).
        public int RowToleranceMm { get; set; } = 500;

        public override bool CanOrder(NumberingItem item)
        {
            return item.X != null && item.Y != null;
        }

        public override bool IsValid()
        {
            return RowToleranceMm > 0 && RowToleranceMm <= MaxRowToleranceMm;
        }

        public override List<NumberingItem> Order(List<NumberingItem> items)
        {
            // The signs turn the chosen corner into "smallest coordinates first".
            double signX = Start == Corner.TopRight || Start == Corner.BottomRight ? -1 : 1;
            double signY = Start == Corner.TopLeft || Start == Corner.TopRight ? -1 : 1;

            var keys = new List<SortKey>(items.Count);
            foreach (NumberingItem item in items)
            {
                double x = signX * item.X.Value;
                double y = signY * item.Y.Value;
                keys.Add(ByColumns ? new SortKey(x, y, item) : new SortKey(y, x, item));
            }

            keys.Sort((a, b) => a.Across != b.Across ? a.Across.CompareTo(b.Across) : a.Item.Id.CompareTo(b.Item.Id));

            var result = new List<NumberingItem>(keys.Count);
            int rowStart = 0;
            while (rowStart < keys.Count)
            {
                // A row is measured from its first element, not from the previous one.
                int rowEnd = rowStart + 1;
                while (rowEnd < keys.Count && keys[rowEnd].Across - keys[rowStart].Across <= RowToleranceMm)
                {
                    rowEnd++;
                }

                List<SortKey> row = keys.GetRange(rowStart, rowEnd - rowStart);
                row.Sort((a, b) => a.Along != b.Along ? a.Along.CompareTo(b.Along) : a.Item.Id.CompareTo(b.Item.Id));
                AddRow(row, result);
                rowStart = rowEnd;
            }

            return result;
        }

        // Elements of a row that are less than 1 mm apart along the row go by id.
        private static void AddRow(List<SortKey> row, List<NumberingItem> result)
        {
            int groupStart = 0;
            while (groupStart < row.Count)
            {
                int groupEnd = groupStart + 1;
                while (groupEnd < row.Count && row[groupEnd].Along - row[groupStart].Along < CoordEpsMm)
                {
                    groupEnd++;
                }

                result.AddRange(row.GetRange(groupStart, groupEnd - groupStart)
                    .Select(key => key.Item)
                    .OrderBy(item => item.Id));
                groupStart = groupEnd;
            }
        }

        private readonly struct SortKey
        {
            public SortKey(double across, double along, NumberingItem item)
            {
                Across = across;
                Along = along;
                Item = item;
            }

            // Coordinate across the row. It joins elements into rows (or columns).
            public double Across { get; }

            // Coordinate along the row. It sets the order inside a row (or column).
            public double Along { get; }

            public NumberingItem Item { get; }
        }
    }
}
