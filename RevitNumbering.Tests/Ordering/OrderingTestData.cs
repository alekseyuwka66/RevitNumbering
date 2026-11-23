using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Ordering;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Tests.Ordering
{
    /// <summary>
    /// Test data: the 3×3 doors, the corridor rooms and builders for NumberingEngine cases.
    /// </summary>
    internal static class OrderingTestData
    {
        public const double DoorsTolerance = 500;
        public const double RoomsTolerance = 3000;

        /// <summary>Doors A…I in reading order (A B C / D E F / G H I).</summary>
        public static IReadOnlyList<OrderItem> Doors3x3()
        {
            return new[]
            {
                new OrderItem(305, 0, 10000),
                new OrderItem(112, 5000, 10300),
                new OrderItem(240, 10000, 10000),
                new OrderItem(118, 200, 5000),
                new OrderItem(301, 5000, 5000),
                new OrderItem(127, 10000, 4800),
                new OrderItem(233, 0, 0),
                new OrderItem(109, 5000, 300),
                new OrderItem(316, 10000, 0)
            };
        }

        /// <summary>Rooms for the corridor case.</summary>
        public static IReadOnlyList<OrderItem> CorridorRooms()
        {
            return new[]
            {
                new OrderItem(101, 3000, 2000),
                new OrderItem(102, 9000, 4500),
                new OrderItem(103, 15000, 2000),
                new OrderItem(104, 21000, 4500),
                new OrderItem(105, 3000, -4000),
                new OrderItem(106, 9000, -6500)
            };
        }

        /// <summary>Expected id sequence of the doors grid for a variant.</summary>
        public static long[] Expected3x3(string corner, string direction)
        {
            switch (corner + "|" + direction)
            {
                case "TopLeft|Rows": return new long[] { 305, 112, 240, 118, 301, 127, 233, 109, 316 };
                case "TopRight|Rows": return new long[] { 240, 112, 305, 127, 301, 118, 316, 109, 233 };
                case "BottomLeft|Rows": return new long[] { 233, 109, 316, 118, 301, 127, 305, 112, 240 };
                case "BottomRight|Rows": return new long[] { 316, 109, 233, 127, 301, 118, 240, 112, 305 };
                case "TopLeft|Columns": return new long[] { 305, 118, 233, 112, 301, 109, 240, 127, 316 };
                case "TopRight|Columns": return new long[] { 240, 127, 316, 112, 301, 109, 305, 118, 233 };
                case "BottomLeft|Columns": return new long[] { 233, 118, 305, 109, 301, 112, 316, 127, 240 };
                case "BottomRight|Columns": return new long[] { 316, 127, 240, 109, 301, 112, 233, 118, 305 };
                default: throw new ArgumentException("Unknown variant: " + corner + ", " + direction);
            }
        }

        public static IEnumerable<object[]> Variants()
        {
            foreach (string direction in new[] { "Rows", "Columns" })
            {
                foreach (string corner in new[] { "TopLeft", "TopRight", "BottomLeft", "BottomRight" })
                {
                    yield return new object[] { corner, direction };
                }
            }
        }

        public static Dictionary<string, string> Parameters(string corner, string direction)
        {
            return new Dictionary<string, string>
            {
                { GridTraversal.CornerParameter, corner },
                { GridTraversal.DirectionParameter, direction }
            };
        }

        public static CategoryInfo Category(string key)
        {
            return new CategoryInfo(key, key, "Д-{Уровень}-{N:000}", DoorsTolerance, ConflictScope.Document, false);
        }

        public static LevelData Level(string key, string name, double elevationMm)
        {
            return new LevelData(key, name, elevationMm, null);
        }

        /// <summary>Neutral element: empty value, not owned, not read-only, located.</summary>
        public static ElementData Element(long id, double? xmm, double? ymm, LevelData level,
            string currentValue = "", bool ownedByOtherUser = false)
        {
            return new ElementData(id, "Door " + id, level?.Key, level?.Name, currentValue, xmm, ymm, null,
                false, ownedByOtherUser, ownedByOtherUser ? "other" : null, true, 10, true);
        }

        public static CategorySnapshot Snapshot(IEnumerable<ElementData> elements, IEnumerable<LevelData> levels,
            string categoryKey = "Doors")
        {
            return new CategorySnapshot(new DocumentKey("doc"), 1, Category(categoryKey), elements.ToArray(),
                levels.ToArray(), false);
        }

        /// <summary>Neutral input: scope is the whole snapshot, SkipOccupied, no append.</summary>
        public static NumberingInput Input(CategorySnapshot snapshot, string template, TraversalOrder order,
            double tolerance = DoorsTolerance)
        {
            return new NumberingInput(snapshot, snapshot.Elements.Select(e => e.Id).ToArray(), template,
                new LevelCodeRule(LevelCodeSource.LevelName, "", LevelCodePart.Whole, 1), order, tolerance,
                ConflictStrategy.SkipOccupied, false);
        }

        public static TraversalOrder GridOrder(string corner, string direction)
        {
            return new TraversalOrder(GridTraversal.StrategyKey, Parameters(corner, direction));
        }

        public static TraversalRegistry GridRegistry()
        {
            return new TraversalRegistry(new ITraversalStrategy[] { new GridTraversal() });
        }

        public static PlannedRow Row(NumberingPlan plan, long id)
        {
            return plan.Rows.Single(row => row.Id == id);
        }
    }

    /// <summary>Test strategy: ids in descending order, no parameters.</summary>
    internal sealed class FakeTraversal : ITraversalStrategy
    {
        public string Key => "test";

        public string DisplayName => "Test";

        public IReadOnlyList<StrategyParameter> Parameters { get; } = new StrategyParameter[0];

        public IReadOnlyList<long> Order(IReadOnlyList<OrderItem> items,
            IReadOnlyDictionary<string, string> parameters, double rowToleranceMm)
        {
            return items.Select(item => item.Id).OrderByDescending(id => id).ToArray();
        }
    }
}
