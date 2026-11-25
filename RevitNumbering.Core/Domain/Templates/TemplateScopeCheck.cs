using System.Globalization;
using RevitNumbering.Core.Common;
using RevitNumbering.Core.Domain.Elements;

namespace RevitNumbering.Core.Domain.Templates
{
    /// <summary>
    /// Template checks that depend on the scope. Called by NumberingEngine.Compute.
    /// </summary>
    public static class TemplateScopeCheck
    {
        /// <summary>Name of the group of elements without a level.</summary>
        internal const string NoLevelGroupName = Texts.TextCatalog.NoLevelGroup;

        /// <summary>
        /// MissingLevelForManyLevels with level group names when the template has no level placeholder and
        /// the scope has more than one level group; null and unknown keys form the "no level" group.
        /// </summary>
        public static TemplateIssue CheckLevelCount(NumberTemplate template, IEnumerable<string> scopeLevelKeys,
            IEnumerable<LevelData> levels)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (template.UsesLevel || scopeLevelKeys == null)
            {
                return null;
            }

            var known = DistinctByKey(levels);
            var used = new Dictionary<string, LevelData>(StringComparer.Ordinal);
            bool hasNoLevelGroup = false;
            foreach (string key in scopeLevelKeys)
            {
                if (key != null && known.TryGetValue(key, out LevelData level))
                {
                    used[key] = level;
                }
                else
                {
                    hasNoLevelGroup = true;
                }
            }

            if (used.Count + (hasNoLevelGroup ? 1 : 0) <= 1)
            {
                return null;
            }

            var names = Sort(used.Values).Select(level => level.Name).ToList();
            if (hasNoLevelGroup)
            {
                names.Add(NoLevelGroupName);
            }

            return new TemplateIssue(TemplateIssueCode.MissingLevelForManyLevels, names);
        }

        /// <summary>
        /// LevelCodePartMissing with the levels for which rule.Resolve gives null; null when there are none
        /// or the template has no level placeholder.
        /// </summary>
        public static TemplateIssue CheckLevelCodes(NumberTemplate template, LevelCodeRule rule,
            IEnumerable<LevelData> levels)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (!template.UsesLevel)
            {
                return null;
            }

            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            var missing = DistinctByKey(levels).Values.Where(level => rule.Resolve(level) == null).ToList();
            if (missing.Count == 0)
            {
                return null;
            }

            return new TemplateIssue(TemplateIssueCode.LevelCodePartMissing,
                Sort(missing).Select(level => level.Name).ToList());
        }

        /// <summary>
        /// WidthTooSmall when CounterWidth is at least 2 and maxCounter has more digits; otherwise null.
        /// </summary>
        public static TemplateIssue CheckWidth(NumberTemplate template, int maxCounter)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (maxCounter < 1 || template.CounterWidth < 2)
            {
                return null;
            }

            string digits = maxCounter.ToString(CultureInfo.InvariantCulture);
            if (digits.Length <= template.CounterWidth)
            {
                return null;
            }

            return new TemplateIssue(TemplateIssueCode.WidthTooSmall,
                new[] { template.CounterWidth.ToString(CultureInfo.InvariantCulture), digits });
        }

        /// <summary>Levels by key (Ordinal); a repeated key is taken once, the first wins.</summary>
        private static Dictionary<string, LevelData> DistinctByKey(IEnumerable<LevelData> levels)
        {
            var result = new Dictionary<string, LevelData>(StringComparer.Ordinal);
            if (levels == null)
            {
                return result;
            }

            foreach (var level in levels)
            {
                if (level?.Key != null && !result.ContainsKey(level.Key))
                {
                    result.Add(level.Key, level);
                }
            }

            return result;
        }

        /// <summary>Elevation ascending, then name and key (Ordinal).</summary>
        private static IEnumerable<LevelData> Sort(IEnumerable<LevelData> levels)
        {
            return levels
                .OrderBy(level => level.ElevationMm)
                .ThenBy(level => level.Name, StringComparer.Ordinal)
                .ThenBy(level => level.Key, StringComparer.Ordinal);
        }
    }
}
