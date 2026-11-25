using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Orderings;
using RevitNumbering.Core.Settings;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Tests
{
    /// <summary>Short builders of test data.</summary>
    internal static class Make
    {
        public const string DoorTemplate = "Д-{Уровень}-{N:000}";

        public static NumberingItem Item(long id, long? levelId, double? x, double? y, string value = "")
        {
            return new NumberingItem { Id = id, Name = "Item " + id, LevelId = levelId, X = x, Y = y, Value = value };
        }

        public static LevelInfo Level(long id, string name, double elevationMm)
        {
            return new LevelInfo { Id = id, Name = name, ElevationMm = elevationMm };
        }

        public static CategorySettings Settings(string template = DoorTemplate,
            LevelCodePart part = LevelCodePart.Whole, int wordNumber = 1)
        {
            return new CategorySettings
            {
                CategoryKey = "Doors",
                Template = template,
                LevelCode = new LevelCode { Part = part, WordNumber = wordNumber },
                Ordering = new GridOrdering(),
            };
        }

        /// <summary>scope == null: every element is in the scope.</summary>
        public static NumberingRequest Request(List<NumberingItem> items, List<LevelInfo> levels,
            CategorySettings settings = null, IEnumerable<long> scope = null)
        {
            return new NumberingRequest
            {
                Items = items,
                Levels = levels,
                Scope = new HashSet<long>(scope ?? items.Select(item => item.Id)),
                Settings = settings ?? Settings(),
            };
        }
    }
}
