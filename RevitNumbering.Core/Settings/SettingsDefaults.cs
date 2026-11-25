using RevitNumbering.Core.Domain.Numbering;
using RevitNumbering.Core.Domain.Ordering;
using RevitNumbering.Core.Domain.Templates;

namespace RevitNumbering.Core.Settings
{
    /// <summary>
    /// Default values of the settings file. Keys of categories and of strategy parameters are not repeated
    /// here: the default category comes from the category registry, traversal parameters from the strategy.
    /// </summary>
    internal static class SettingsDefaults
    {
        public const string DefaultLevelCodeSource = nameof(LevelCodeSource.LevelName);

        public const string DefaultLevelCodePart = nameof(LevelCodePart.Whole);

        public const int DefaultLevelWordNumber = 1;

        public const string DefaultLevelParameterName = "";

        public const string DefaultStrategy = nameof(ConflictStrategy.SkipOccupied);

        public const bool DefaultAppend = false;

        /// <summary>
        /// General defaults: no file or a corrupted file. Always a new object.
        /// LastCategoryKey is null: the window takes the first registered category.
        /// </summary>
        public static NumberingSettings CreateGeneral()
        {
            return new NumberingSettings
            {
                SchemaVersion = SettingsLimits.SettingsSchema,
                Traversal = CreateTraversal(),
                LastCategoryKey = null,
                Window = null,
                Categories = new Dictionary<string, CategorySettings>(StringComparer.Ordinal)
            };
        }

        /// <summary>The "grid" strategy with the defaults of its general parameters (corner, direction).</summary>
        public static TraversalSettings CreateTraversal()
        {
            var grid = new GridTraversal();
            return new TraversalSettings
            {
                StrategyKey = grid.Key,
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { grid.Corner.Key, grid.Corner.DefaultValue },
                    { grid.Direction.Key, grid.Direction.DefaultValue }
                }
            };
        }
    }
}
