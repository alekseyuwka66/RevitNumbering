using RevitNumbering.Core.Orderings;

namespace RevitNumbering.Core.Settings
{
    public sealed class NumberingSettings
    {
        public string LastCategory { get; set; }

        public List<CategorySettings> Categories { get; set; } = new List<CategorySettings>();

        public CategorySettings Find(string categoryKey)
        {
            return Categories?.FirstOrDefault(c => c != null && c.CategoryKey == categoryKey);
        }
    }

    public sealed class CategorySettings
    {
        public const int MinWidth = 1;

        public const int MaxWidth = 9;

        public string CategoryKey { get; set; }

        public string Prefix { get; set; } = string.Empty;

        public bool IncludeLevel { get; set; } = true;

        // True: the level goes before the counter. False: after it.
        public bool LevelFirst { get; set; } = true;

        // Text between the level and the counter. Used only with the level.
        public string Separator { get; set; } = string.Empty;

        // Digits of the counter, from 1 to 9. Shorter numbers get leading zeros.
        public int Width { get; set; }

        public string Suffix { get; set; } = string.Empty;

        public Ordering Ordering { get; set; }

        // The text parts are set, the width is in range, and the ordering is set and valid.
        public bool IsValid()
        {
            return Prefix != null && Separator != null && Suffix != null
                && Width >= MinWidth && Width <= MaxWidth
                && Ordering != null && Ordering.IsValid();
        }
    }
}
