using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Core.Settings
{
    /// <summary>
    /// What is allowed in the settings, normalization of a read file and copies of schema objects.
    /// </summary>
    internal static class SettingsRules
    {
        private static readonly string[] LevelCodeSources = Enum.GetNames(typeof(LevelCodeSource));
        private static readonly string[] LevelCodeParts = Enum.GetNames(typeof(LevelCodePart));
        private static readonly string[] ConflictStrategies = Enum.GetNames(typeof(ConflictStrategy));

        /// <summary>null: the category record is allowed; otherwise the reason for the log.</summary>
        public static string FindCategoryProblem(CategorySettings category)
        {
            if (category == null)
            {
                return "запись пуста (null)";
            }

            if (category.Template == null)
            {
                return "шаблон не задан (null)";
            }

            if (!IsOneOf(category.LevelCodeSource, LevelCodeSources))
            {
                return "неизвестный источник кода уровня";
            }

            if (!IsOneOf(category.LevelCodePart, LevelCodeParts))
            {
                return "неизвестная часть кода уровня";
            }

            if (category.LevelWordNumber < SettingsLimits.LevelWordNumberMin)
            {
                return "номер слова меньше 1";
            }

            if (!IsTolerance(category.RowToleranceMm))
            {
                return "допуск вне диапазона";
            }

            if (!IsOneOf(category.ConflictStrategy, ConflictStrategies))
            {
                return "неизвестная стратегия конфликтов";
            }

            return null;
        }

        public static bool IsWindowValid(WindowPlacement window)
        {
            return window != null
                && IsFinite(window.Left) && IsFinite(window.Top)
                && IsFinite(window.Width) && IsFinite(window.Height)
                && window.Width > 0 && window.Height > 0;
        }

        /// <summary>Normalized copy of a parsed file; the dropped records are reported to the callback.</summary>
        public static NumberingSettings Normalize(NumberingSettings raw, Action<string, string> onDroppedCategory)
        {
            var result = new NumberingSettings
            {
                SchemaVersion = raw.SchemaVersion,
                Traversal = raw.Traversal == null || string.IsNullOrEmpty(raw.Traversal.StrategyKey)
                    ? SettingsDefaults.CreateTraversal()
                    : new TraversalSettings
                    {
                        StrategyKey = raw.Traversal.StrategyKey,
                        Parameters = CopyParameters(raw.Traversal.Parameters)
                            ?? new Dictionary<string, string>(StringComparer.Ordinal)
                    },
                LastCategoryKey = string.IsNullOrEmpty(raw.LastCategoryKey)
                    ? SettingsDefaults.DefaultCategory
                    : raw.LastCategoryKey,
                Window = IsWindowValid(raw.Window) ? Copy(raw.Window) : null,
                Categories = new Dictionary<string, CategorySettings>(StringComparer.Ordinal)
            };

            if (raw.Categories != null)
            {
                foreach (var pair in raw.Categories)
                {
                    string problem = FindCategoryProblem(pair.Value);
                    if (problem == null)
                    {
                        result.Categories[pair.Key] = CopyOwnCategory(pair.Value);
                    }
                    else
                    {
                        onDroppedCategory?.Invoke(pair.Key, problem);
                    }
                }
            }

            return result;
        }

        /// <summary>Copy of a category as it is stored: no per-category traversal, parameter name is not null.</summary>
        public static CategorySettings CopyOwnCategory(CategorySettings source)
        {
            return new CategorySettings
            {
                Template = source.Template,
                LevelCodeSource = source.LevelCodeSource,
                LevelParameterName = source.LevelParameterName ?? string.Empty,
                LevelCodePart = source.LevelCodePart,
                LevelWordNumber = source.LevelWordNumber,
                RowToleranceMm = source.RowToleranceMm,
                ConflictStrategy = source.ConflictStrategy,
                AppendMode = source.AppendMode,
                Traversal = null
            };
        }

        public static TraversalSettings Copy(TraversalSettings source)
        {
            return new TraversalSettings
            {
                StrategyKey = source.StrategyKey,
                Parameters = CopyParameters(source.Parameters)
            };
        }

        public static WindowPlacement Copy(WindowPlacement source)
        {
            return new WindowPlacement
            {
                Left = source.Left,
                Top = source.Top,
                Width = source.Width,
                Height = source.Height
            };
        }

        private static IDictionary<string, string> CopyParameters(IDictionary<string, string> source)
        {
            if (source == null)
            {
                return null;
            }

            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in source)
            {
                copy[pair.Key] = pair.Value;
            }

            return copy;
        }

        private static bool IsOneOf(string value, string[] allowed)
        {
            return value != null && Array.IndexOf(allowed, value) >= 0;
        }

        private static bool IsTolerance(double value)
        {
            return IsFinite(value)
                && Math.Floor(value) == value
                && value >= RowToleranceLimits.MinMm
                && value <= RowToleranceLimits.MaxMm;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
