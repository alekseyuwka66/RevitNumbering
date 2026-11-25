namespace RevitNumbering.Core.Domain.Ordering
{
    internal enum GridCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    internal enum GridDirection
    {
        Rows,
        Columns
    }

    /// <summary>
    /// Traversal by rows or by columns starting from one of four corners: 8 variants.
    /// Orientation follows the model axes: right is +X, up is +Y.
    /// </summary>
    public sealed class GridTraversal : ITraversalStrategy
    {
        public const string StrategyKey = "grid";
        public const string CornerParameter = "corner";
        public const string DirectionParameter = "direction";
        public const string RowToleranceParameter = "rowToleranceMm";

        /// <summary>Limits of the row tolerance, mm.</summary>
        public const double RowToleranceMinMm = 1;
        public const double RowToleranceMaxMm = 100000;

        /// <summary>The strategy's own default; categories get theirs through CategoryStrategyDefaults.</summary>
        public const double DefaultRowToleranceMm = 500;

        /// <summary>Coordinates closer than this are equal, then ElementId decides.</summary>
        internal const double CoordEpsMm = 1;

        // Texts of the strategy and its parameters are defined by the strategy itself.
        private const string GridDisplayName = "Рядами или столбцами от угла";
        private const string CornerDisplayName = "Начальный угол";
        private const string CornerTopLeftName = "Верхний левый";
        private const string CornerTopRightName = "Верхний правый";
        private const string CornerBottomLeftName = "Нижний левый";
        private const string CornerBottomRightName = "Нижний правый";
        private const string DirectionDisplayName = "Направление";
        private const string DirectionRowsName = "Рядами";
        private const string DirectionColumnsName = "Столбцами";
        private const string RowToleranceDisplayName = "Допуск ряда, мм";

        // Corner = TopLeft, direction = Rows.
        private const GridCorner DefaultCorner = GridCorner.TopLeft;
        private const GridDirection DefaultDirection = GridDirection.Rows;

        private static readonly IComparer<SortKey> ByMainThenId = Comparer<SortKey>.Create(
            (a, b) => a.U != b.U ? a.U.CompareTo(b.U) : a.Id.CompareTo(b.Id));

        private static readonly IComparer<SortKey> BySecondThenId = Comparer<SortKey>.Create(
            (a, b) => a.V != b.V ? a.V.CompareTo(b.V) : a.Id.CompareTo(b.Id));

        public GridTraversal()
        {
            Corner = StrategyParameter.Choice(CornerParameter, CornerDisplayName, new[]
            {
                new StrategyOption(nameof(GridCorner.TopLeft), CornerTopLeftName),
                new StrategyOption(nameof(GridCorner.TopRight), CornerTopRightName),
                new StrategyOption(nameof(GridCorner.BottomLeft), CornerBottomLeftName),
                new StrategyOption(nameof(GridCorner.BottomRight), CornerBottomRightName)
            }, DefaultCorner.ToString());
            Direction = StrategyParameter.Choice(DirectionParameter, DirectionDisplayName, new[]
            {
                new StrategyOption(nameof(GridDirection.Rows), DirectionRowsName),
                new StrategyOption(nameof(GridDirection.Columns), DirectionColumnsName)
            }, DefaultDirection.ToString());
            RowTolerance = StrategyParameter.Number(RowToleranceParameter, RowToleranceDisplayName,
                DefaultRowToleranceMm, RowToleranceMinMm, RowToleranceMaxMm);
            Parameters = new[] { Corner, Direction, RowTolerance };
        }

        public string Key => StrategyKey;

        public string DisplayName => GridDisplayName;

        public IReadOnlyList<StrategyParameter> Parameters { get; }

        public StrategyParameter Corner { get; }

        public StrategyParameter Direction { get; }

        /// <summary>A row (column) is joined while the main coordinate differs by no more than this.</summary>
        public StrategyParameter RowTolerance { get; }

        public IReadOnlyList<long> Order(IReadOnlyList<OrderItem> items, StrategyArguments arguments)
        {
            Validate(items);
            arguments = arguments ?? StrategyArguments.Empty;

            GridCorner corner = ReadCorner(arguments.Get(Corner));
            GridDirection direction = ReadDirection(arguments.Get(Direction));
            double rowToleranceMm = arguments.GetNumber(RowTolerance);
            double sx = corner == GridCorner.TopRight || corner == GridCorner.BottomRight ? -1 : 1;
            double sy = corner == GridCorner.TopLeft || corner == GridCorner.TopRight ? -1 : 1;

            var keys = new List<SortKey>(items.Count);
            foreach (OrderItem item in items)
            {
                keys.Add(direction == GridDirection.Rows
                    ? new SortKey(sy * item.Ymm, sx * item.Xmm, item.Id)
                    : new SortKey(sx * item.Xmm, sy * item.Ymm, item.Id));
            }

            keys.Sort(ByMainThenId);

            var result = new List<long>(keys.Count);
            var equalIds = new List<long>();
            int rowStart = 0;
            while (rowStart < keys.Count)
            {
                // A row is anchored to its first element, not to the previous one.
                int rowEnd = rowStart + 1;
                while (rowEnd < keys.Count && keys[rowEnd].U - keys[rowStart].U <= rowToleranceMm)
                {
                    rowEnd++;
                }

                keys.Sort(rowStart, rowEnd - rowStart, BySecondThenId);

                int groupStart = rowStart;
                while (groupStart < rowEnd)
                {
                    int groupEnd = groupStart + 1;
                    while (groupEnd < rowEnd && keys[groupEnd].V - keys[groupStart].V < CoordEpsMm)
                    {
                        groupEnd++;
                    }

                    equalIds.Clear();
                    for (int i = groupStart; i < groupEnd; i++)
                    {
                        equalIds.Add(keys[i].Id);
                    }

                    equalIds.Sort();
                    result.AddRange(equalIds);
                    groupStart = groupEnd;
                }

                rowStart = rowEnd;
            }

            return result;
        }

        private static void Validate(IReadOnlyList<OrderItem> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var ids = new HashSet<long>();
            foreach (OrderItem item in items)
            {
                if (item == null)
                {
                    throw new ArgumentException("Items must not contain null.", nameof(items));
                }

                if (double.IsNaN(item.Xmm) || double.IsInfinity(item.Xmm)
                    || double.IsNaN(item.Ymm) || double.IsInfinity(item.Ymm))
                {
                    throw new ArgumentException("Item coordinates must be finite numbers.", nameof(items));
                }

                if (!ids.Add(item.Id))
                {
                    throw new ArgumentException("Item ids must be unique.", nameof(items));
                }
            }
        }

        private static GridCorner ReadCorner(string value)
        {
            if (string.Equals(value, nameof(GridCorner.TopLeft), StringComparison.Ordinal))
            {
                return GridCorner.TopLeft;
            }

            if (string.Equals(value, nameof(GridCorner.TopRight), StringComparison.Ordinal))
            {
                return GridCorner.TopRight;
            }

            if (string.Equals(value, nameof(GridCorner.BottomLeft), StringComparison.Ordinal))
            {
                return GridCorner.BottomLeft;
            }

            if (string.Equals(value, nameof(GridCorner.BottomRight), StringComparison.Ordinal))
            {
                return GridCorner.BottomRight;
            }

            return DefaultCorner;
        }

        private static GridDirection ReadDirection(string value)
        {
            if (string.Equals(value, nameof(GridDirection.Rows), StringComparison.Ordinal))
            {
                return GridDirection.Rows;
            }

            if (string.Equals(value, nameof(GridDirection.Columns), StringComparison.Ordinal))
            {
                return GridDirection.Columns;
            }

            return DefaultDirection;
        }

        private readonly struct SortKey
        {
            public SortKey(double u, double v, long id)
            {
                U = u;
                V = v;
                Id = id;
            }

            /// <summary>Main coordinate: rows (columns) are joined by it.</summary>
            public double U { get; }

            /// <summary>Second coordinate: order inside a row (column).</summary>
            public double V { get; }

            public long Id { get; }
        }
    }
}
