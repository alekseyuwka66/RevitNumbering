using RevitNumbering.Core.Model;
using RevitNumbering.Core.Orderings;

namespace RevitNumbering.Tests.Orderings
{
    public class GridOrderingTests
    {
        /// <summary>Doors A…I in reading order (A B C / D E F / G H I); rows and columns are slightly uneven.</summary>
        private static List<NumberingItem> Doors3x3()
        {
            return new List<NumberingItem>
            {
                Make.Item(305, 1, 0, 10000),
                Make.Item(112, 1, 5000, 10300),
                Make.Item(240, 1, 10000, 10000),
                Make.Item(118, 1, 200, 5000),
                Make.Item(301, 1, 5000, 5000),
                Make.Item(127, 1, 10000, 4800),
                Make.Item(233, 1, 0, 0),
                Make.Item(109, 1, 5000, 300),
                Make.Item(316, 1, 10000, 0),
            };
        }

        private static List<NumberingItem> CorridorRooms()
        {
            return new List<NumberingItem>
            {
                Make.Item(101, 1, 3000, 2000),
                Make.Item(102, 1, 9000, 4500),
                Make.Item(103, 1, 15000, 2000),
                Make.Item(104, 1, 21000, 4500),
                Make.Item(105, 1, 3000, -4000),
                Make.Item(106, 1, 9000, -6500),
            };
        }

        private static long[] Ids(List<NumberingItem> items)
        {
            return items.Select(item => item.Id).ToArray();
        }

        [Theory]
        [InlineData(Corner.TopLeft, false, new long[] { 305, 112, 240, 118, 301, 127, 233, 109, 316 })]
        [InlineData(Corner.TopRight, false, new long[] { 240, 112, 305, 127, 301, 118, 316, 109, 233 })]
        [InlineData(Corner.BottomLeft, false, new long[] { 233, 109, 316, 118, 301, 127, 305, 112, 240 })]
        [InlineData(Corner.BottomRight, false, new long[] { 316, 109, 233, 127, 301, 118, 240, 112, 305 })]
        [InlineData(Corner.TopLeft, true, new long[] { 305, 118, 233, 112, 301, 109, 240, 127, 316 })]
        [InlineData(Corner.TopRight, true, new long[] { 240, 127, 316, 112, 301, 109, 305, 118, 233 })]
        [InlineData(Corner.BottomLeft, true, new long[] { 233, 118, 305, 109, 301, 112, 316, 127, 240 })]
        [InlineData(Corner.BottomRight, true, new long[] { 316, 127, 240, 109, 301, 112, 233, 118, 305 })]
        public void Grid3x3_EveryCornerAndDirection(Corner start, bool byColumns, long[] expected)
        {
            var ordering = new GridOrdering { Start = start, ByColumns = byColumns, RowToleranceMm = 500 };

            Assert.Equal(expected, Ids(ordering.Order(Doors3x3())));

            // The order of the input does not matter.
            List<NumberingItem> reversed = Doors3x3();
            reversed.Reverse();
            Assert.Equal(expected, Ids(ordering.Order(reversed)));
        }

        [Fact]
        public void RowTolerance_JoinsElementsIntoOneRow()
        {
            var wide = new GridOrdering { RowToleranceMm = 3000 };
            var narrow = new GridOrdering { RowToleranceMm = 500 };

            Assert.Equal(new long[] { 101, 102, 103, 104, 105, 106 }, Ids(wide.Order(CorridorRooms())));
            Assert.Equal(new long[] { 102, 104, 101, 103, 105, 106 }, Ids(narrow.Order(CorridorRooms())));
        }

        [Fact]
        public void CoordinatesCloserThanOneMillimeter_AreOrderedById()
        {
            var items = new List<NumberingItem>
            {
                Make.Item(7, 1, 1000.0, 0),
                Make.Item(3, 1, 1000.4, 0),
                Make.Item(5, 1, 0, 0),
            };

            Assert.Equal(new long[] { 5, 3, 7 }, Ids(new GridOrdering().Order(items)));
        }

        [Fact]
        public void CanOrder_NeedsPoint()
        {
            var ordering = new GridOrdering();

            Assert.True(ordering.CanOrder(Make.Item(1, 1, 0, 0)));
            Assert.False(ordering.CanOrder(Make.Item(2, 1, null, null)));
            Assert.False(ordering.CanOrder(Make.Item(3, 1, 0, null)));
        }
    }
}
