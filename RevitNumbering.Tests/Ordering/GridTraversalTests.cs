using RevitNumbering.Core.Ordering;

namespace RevitNumbering.Tests.Ordering
{
    /// <summary>
    /// The "grid" strategy.
    /// </summary>
    public class GridTraversalTests
    {
        private static readonly GridTraversal Grid = new GridTraversal();

        private static IReadOnlyList<long> Order3x3(string corner, string direction)
        {
            return Grid.Order(OrderingTestData.Doors3x3(), OrderingTestData.Parameters(corner, direction),
                OrderingTestData.DoorsTolerance);
        }

        [Fact]
        public void Doors3x3_TopLeftRows()
        {
            Assert.Equal(new long[] { 305, 112, 240, 118, 301, 127, 233, 109, 316 }, Order3x3("TopLeft", "Rows"));
        }

        [Fact]
        public void Doors3x3_TopRightRows()
        {
            Assert.Equal(new long[] { 240, 112, 305, 127, 301, 118, 316, 109, 233 }, Order3x3("TopRight", "Rows"));
        }

        [Fact]
        public void Doors3x3_BottomLeftRows()
        {
            Assert.Equal(new long[] { 233, 109, 316, 118, 301, 127, 305, 112, 240 },
                Order3x3("BottomLeft", "Rows"));
        }

        [Fact]
        public void Doors3x3_BottomRightRows()
        {
            Assert.Equal(new long[] { 316, 109, 233, 127, 301, 118, 240, 112, 305 },
                Order3x3("BottomRight", "Rows"));
        }

        [Fact]
        public void Doors3x3_TopLeftColumns()
        {
            Assert.Equal(new long[] { 305, 118, 233, 112, 301, 109, 240, 127, 316 },
                Order3x3("TopLeft", "Columns"));
        }

        [Fact]
        public void Doors3x3_TopRightColumns()
        {
            Assert.Equal(new long[] { 240, 127, 316, 112, 301, 109, 305, 118, 233 },
                Order3x3("TopRight", "Columns"));
        }

        [Fact]
        public void Doors3x3_BottomLeftColumns()
        {
            Assert.Equal(new long[] { 233, 118, 305, 109, 301, 112, 316, 127, 240 },
                Order3x3("BottomLeft", "Columns"));
        }

        [Fact]
        public void Doors3x3_BottomRightColumns()
        {
            Assert.Equal(new long[] { 316, 127, 240, 109, 301, 112, 233, 118, 305 },
                Order3x3("BottomRight", "Columns"));
        }

        [Theory]
        [MemberData(nameof(OrderingTestData.Variants), MemberType = typeof(OrderingTestData))]
        public void InputOrderDoesNotMatter(string corner, string direction)
        {
            long[] expected = OrderingTestData.Expected3x3(corner, direction);
            var parameters = OrderingTestData.Parameters(corner, direction);
            var inputs = new List<IReadOnlyList<OrderItem>>
            {
                OrderingTestData.Doors3x3(),
                OrderingTestData.Doors3x3().Reverse().ToArray()
            };
            for (int seed = 1; seed <= 5; seed++)
            {
                inputs.Add(Shuffle(OrderingTestData.Doors3x3(), new Random(seed)));
            }

            foreach (IReadOnlyList<OrderItem> input in inputs)
            {
                Assert.Equal(expected, Grid.Order(input, parameters, OrderingTestData.DoorsTolerance));
            }
        }

        [Fact]
        public void CorridorRooms_Tolerance3000_OneRowPerSide()
        {
            IReadOnlyList<long> order = Grid.Order(OrderingTestData.CorridorRooms(),
                OrderingTestData.Parameters("TopLeft", "Rows"), OrderingTestData.RoomsTolerance);

            Assert.Equal(new long[] { 101, 102, 103, 104, 105, 106 }, order);
        }

        [Fact]
        public void CorridorRooms_Tolerance500_SplitByDepth()
        {
            IReadOnlyList<long> order = Grid.Order(OrderingTestData.CorridorRooms(),
                OrderingTestData.Parameters("TopLeft", "Rows"), 500);

            Assert.Equal(new long[] { 102, 104, 101, 103, 105, 106 }, order);
        }

        [Fact]
        public void ToleranceBoundaryIsInclusive()
        {
            var items = new[]
            {
                new OrderItem(1, 0, 1000),
                new OrderItem(2, 1000, 500),
                new OrderItem(3, -1000, 499.9)
            };

            IReadOnlyList<long> order = Grid.Order(items, OrderingTestData.Parameters("TopLeft", "Rows"), 500);

            Assert.Equal(new long[] { 1, 2, 3 }, order);
        }

        [Fact]
        public void RowIsAnchoredToItsFirstElement()
        {
            var items = new[]
            {
                new OrderItem(1, 0, 0),
                new OrderItem(2, 1000, 400),
                new OrderItem(3, 2000, 800),
                new OrderItem(4, 3000, 1200)
            };
            var parameters = OrderingTestData.Parameters("TopLeft", "Rows");

            Assert.Equal(new long[] { 3, 4, 1, 2 }, Grid.Order(items, parameters, 500));
            Assert.Equal(new long[] { 1, 2, 3, 4 }, Grid.Order(items, parameters, 2000));
        }

        [Fact]
        public void ColumnTolerance()
        {
            var items = new[]
            {
                new OrderItem(1, 0, 0),
                new OrderItem(2, 400, 5000),
                new OrderItem(3, 600, 10000)
            };

            IReadOnlyList<long> order = Grid.Order(items, OrderingTestData.Parameters("TopLeft", "Columns"), 500);

            Assert.Equal(new long[] { 2, 1, 3 }, order);
        }

        [Fact]
        public void CoordinatesCloserThanOneMm_AreEqual_IdDecides()
        {
            var items = new[]
            {
                new OrderItem(50, 1000.0, 0),
                new OrderItem(20, 1000.6, 0),
                new OrderItem(10, 1001.2, 0)
            };

            Assert.Equal(new long[] { 20, 50, 10 },
                Grid.Order(items, OrderingTestData.Parameters("TopLeft", "Rows"), 500));
            Assert.Equal(new long[] { 10, 20, 50 },
                Grid.Order(items, OrderingTestData.Parameters("TopRight", "Rows"), 500));
        }

        [Fact]
        public void DifferenceOfExactlyOneMm_IsNotEqual()
        {
            var parameters = OrderingTestData.Parameters("TopLeft", "Rows");

            Assert.Equal(new long[] { 9, 5 },
                Grid.Order(new[] { new OrderItem(9, 0, 0), new OrderItem(5, 1.0, 0) }, parameters, 500));
            Assert.Equal(new long[] { 5, 9 },
                Grid.Order(new[] { new OrderItem(9, 0, 0), new OrderItem(5, 0.999, 0) }, parameters, 500));
        }

        [Fact]
        public void SamePoint_OrderedById()
        {
            var items = new[] { new OrderItem(7, 0, 0), new OrderItem(3, 0, 0), new OrderItem(5, 0, 0) };

            IReadOnlyList<long> order = Grid.Order(items, OrderingTestData.Parameters("BottomRight", "Columns"),
                500);

            Assert.Equal(new long[] { 3, 5, 7 }, order);
        }

        [Fact]
        public void DefaultParameters_NullAndEmptyDictionary()
        {
            long[] expected = OrderingTestData.Expected3x3("TopLeft", "Rows");

            Assert.Equal(expected, Grid.Order(OrderingTestData.Doors3x3(), null, 500));
            Assert.Equal(expected, Grid.Order(OrderingTestData.Doors3x3(), new Dictionary<string, string>(), 500));
        }

        [Fact]
        public void UnknownParameterValue_FallsBackToDefault()
        {
            Assert.Equal(OrderingTestData.Expected3x3("TopLeft", "Columns"), Order3x3("Middle", "Columns"));
            Assert.Equal(OrderingTestData.Expected3x3("BottomRight", "Rows"), Order3x3("BottomRight", "columns"));

            var withExtra = OrderingTestData.Parameters("TopRight", "Rows");
            withExtra.Add("extra", "x");
            Assert.Equal(OrderingTestData.Expected3x3("TopRight", "Rows"),
                Grid.Order(OrderingTestData.Doors3x3(), withExtra, 500));

            var withNull = OrderingTestData.Parameters("TopRight", "Rows");
            withNull[GridTraversal.CornerParameter] = null;
            Assert.Equal(OrderingTestData.Expected3x3("TopLeft", "Rows"),
                Grid.Order(OrderingTestData.Doors3x3(), withNull, 500));
        }

        [Fact]
        public void StrategyDescription()
        {
            var grid = new GridTraversal();

            Assert.Equal("grid", grid.Key);
            Assert.Equal("grid", GridTraversal.StrategyKey);
            Assert.Equal("Рядами или столбцами от угла", grid.DisplayName);
            Assert.Equal(2, grid.Parameters.Count);

            StrategyParameter corner = grid.Parameters[0];
            Assert.Equal("corner", corner.Key);
            Assert.Equal("Начальный угол", corner.DisplayName);
            Assert.Equal("TopLeft", corner.DefaultValue);
            Assert.Equal(new[] { "TopLeft", "TopRight", "BottomLeft", "BottomRight" },
                corner.Options.Select(option => option.Value));
            Assert.Equal(new[] { "Верхний левый", "Верхний правый", "Нижний левый", "Нижний правый" },
                corner.Options.Select(option => option.DisplayName));

            StrategyParameter direction = grid.Parameters[1];
            Assert.Equal("direction", direction.Key);
            Assert.Equal("Направление", direction.DisplayName);
            Assert.Equal("Rows", direction.DefaultValue);
            Assert.Equal(new[] { "Rows", "Columns" }, direction.Options.Select(option => option.Value));
            Assert.Equal(new[] { "Рядами", "Столбцами" }, direction.Options.Select(option => option.DisplayName));
        }

        [Fact]
        public void InputErrors_NullAndEmptyItems()
        {
            var parameters = OrderingTestData.Parameters("TopLeft", "Rows");

            Assert.Throws<ArgumentNullException>(() => Grid.Order(null, parameters, 500));
            Assert.Empty(Grid.Order(new OrderItem[0], parameters, 500));
        }

        [Theory]
        [InlineData(0d)]
        [InlineData(-1d)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void InputErrors_BadTolerance(double tolerance)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Grid.Order(OrderingTestData.Doors3x3(), null, tolerance));
        }

        [Fact]
        public void InputErrors_BadCoordinateAndDuplicateId()
        {
            // Assert.Throws checks the exact type: ArgumentException, not its subclasses.
            Assert.Throws<ArgumentException>(
                () => Grid.Order(new[] { new OrderItem(1, double.NaN, 0) }, null, 500));
            Assert.Throws<ArgumentException>(
                () => Grid.Order(new[] { new OrderItem(1, 0, double.PositiveInfinity) }, null, 500));
            Assert.Throws<ArgumentException>(
                () => Grid.Order(new[] { new OrderItem(1, 0, 0), new OrderItem(1, 1000, 0) }, null, 500));
        }

        private static IReadOnlyList<OrderItem> Shuffle(IReadOnlyList<OrderItem> items, Random random)
        {
            var list = items.ToList();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                OrderItem swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }

            return list;
        }
    }
}
