using RevitNumbering.Core.Orderings;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Core.Settings
{
    /// <summary>The whole settings file.</summary>
    public sealed class NumberingSettings
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public string LastCategory { get; set; }

        /// <summary>null: the window opens centered with its default size.</summary>
        public WindowPlacement Window { get; set; }

        public List<CategorySettings> Categories { get; set; } = new List<CategorySettings>();

        public CategorySettings Find(string categoryKey)
        {
            return Categories?.FirstOrDefault(c => c != null && c.CategoryKey == categoryKey);
        }
    }

    /// <summary>Numbering settings of one category.</summary>
    public sealed class CategorySettings
    {
        public string CategoryKey { get; set; }

        public string Template { get; set; }

        public LevelCode LevelCode { get; set; }

        public Ordering Ordering { get; set; }

        /// <summary>The template parses, the level code is valid, the ordering is set and valid.</summary>
        public bool IsValid()
        {
            return Templates.Template.Parse(Template).Template != null
                && LevelCode != null && LevelCode.IsValid()
                && Ordering != null && Ordering.IsValid();
        }
    }

    public sealed class WindowPlacement
    {
        public double Left { get; set; }

        public double Top { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }
    }
}
