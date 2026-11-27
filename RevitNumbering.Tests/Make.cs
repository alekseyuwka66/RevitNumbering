using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Orderings;
using RevitNumbering.Core.Settings;

namespace RevitNumbering.Tests
{
    internal static class Make
    {
        public static NumberingItem Item(long id, long? levelId, double? x, double? y, string value = "")
        {
            return new NumberingItem { Id = id, Name = "Item " + id, LevelId = levelId, X = x, Y = y, Value = value };
        }

        public static LevelInfo Level(long id, string name, double elevationMm)
        {
            return new LevelInfo { Id = id, Name = name, ElevationMm = elevationMm };
        }

        // By default the door template: "Д-" + level + "-" + counter of 3 digits.
        public static CategorySettings Settings(string prefix = "Д-", bool includeLevel = true,
            string separator = "-", int width = 3, bool levelFirst = true, string suffix = "")
        {
            return new CategorySettings
            {
                CategoryKey = "Doors",
                Prefix = prefix,
                IncludeLevel = includeLevel,
                Separator = separator,
                Width = width,
                LevelFirst = levelFirst,
                Suffix = suffix,
                Ordering = new GridOrdering(),
            };
        }

        // scope == null: every element is in the scope.
        public static Preview Calculate(List<NumberingItem> items, List<LevelInfo> levels,
            CategorySettings settings = null, IEnumerable<long> scope = null)
        {
            return NumberingCalculator.Calculate(
                new CategoryData { Items = items, Levels = levels },
                new HashSet<long>(scope ?? items.Select(item => item.Id)),
                settings ?? Settings());
        }
    }
}
