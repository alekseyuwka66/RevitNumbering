using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Core.Settings
{
    /// <summary>
    /// Default values of the settings file.
    /// </summary>
    internal static class SettingsDefaults
    {
        public const string DefaultCategory = "Doors";

        /// <summary>Strategy key.</summary>
        public const string DefaultTraversalStrategyKey = "grid";

        /// <summary>Parameter "corner".</summary>
        public const string DefaultTraversalCornerKey = "corner";

        /// <summary>Value of "corner".</summary>
        public const string DefaultTraversalCorner = "TopLeft";

        /// <summary>Parameter "direction".</summary>
        public const string DefaultTraversalDirectionKey = "direction";

        /// <summary>Value of "direction".</summary>
        public const string DefaultTraversalDirection = "Rows";

        /// <summary>Source.</summary>
        public const string DefaultLevelCodeSource = nameof(LevelCodeSource.LevelName);

        /// <summary>Part.</summary>
        public const string DefaultLevelCodePart = nameof(LevelCodePart.Whole);

        /// <summary>Word number.</summary>
        public const int DefaultLevelWordNumber = 1;

        /// <summary>Parameter name.</summary>
        public const string DefaultLevelParameterName = "";

        public const string DefaultStrategy = nameof(ConflictStrategy.SkipOccupied);

        public const bool DefaultAppend = false;

        /// <summary>General defaults: no file or a corrupted file. Always a new object.</summary>
        public static NumberingSettings CreateGeneral()
        {
            return new NumberingSettings
            {
                SchemaVersion = SettingsLimits.SettingsSchema,
                Traversal = CreateTraversal(),
                LastCategoryKey = DefaultCategory,
                Window = null,
                Categories = new Dictionary<string, CategorySettings>(StringComparer.Ordinal)
            };
        }

        public static TraversalSettings CreateTraversal()
        {
            return new TraversalSettings
            {
                StrategyKey = DefaultTraversalStrategyKey,
                Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { DefaultTraversalCornerKey, DefaultTraversalCorner },
                    { DefaultTraversalDirectionKey, DefaultTraversalDirection }
                }
            };
        }
    }
}
