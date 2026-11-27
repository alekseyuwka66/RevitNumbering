using System.Globalization;
using RevitNumbering.Core.Settings;

namespace RevitNumbering.Core.Templates
{
    // Builds a number from the settings: prefix + level + separator + counter + suffix,
    // or with the counter before the level. Without the level: prefix + counter + suffix.
    public sealed class Template
    {
        private readonly string _prefix;
        private readonly string _separator;
        private readonly string _suffix;
        private readonly bool _levelFirst;

        private Template(CategorySettings settings)
        {
            _prefix = settings.Prefix ?? string.Empty;
            _separator = settings.Separator ?? string.Empty;
            _suffix = settings.Suffix ?? string.Empty;
            _levelFirst = settings.LevelFirst;
            HasLevel = settings.IncludeLevel;
            Width = settings.Width;
        }

        public bool HasLevel { get; }

        // Minimum digits of the counter. Shorter numbers get leading zeros. Longer ones are never cut.
        public int Width { get; }

        public static Template From(CategorySettings settings)
        {
            return new Template(settings);
        }

        public string Format(string levelName, int number)
        {
            string counter = number.ToString("D" + Width, CultureInfo.InvariantCulture);
            if (!HasLevel)
            {
                return _prefix + counter + _suffix;
            }

            return _levelFirst
                ? _prefix + levelName + _separator + counter + _suffix
                : _prefix + counter + _separator + levelName + _suffix;
        }
    }
}
