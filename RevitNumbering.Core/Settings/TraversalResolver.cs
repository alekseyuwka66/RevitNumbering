using RevitNumbering.Core.Domain.Categories;
using RevitNumbering.Core.Domain.Ordering;

namespace RevitNumbering.Core.Settings
{
    /// <summary>
    /// The traversal that applies to a category (F6-01, F6-05):
    /// 1) strategy defaults completed by the category overrides of the composition root;
    /// 2) values stored in the category (when its strategy key matches);
    /// 3) general values (one order for all categories) — only for parameters that are not per-category.
    /// A parameter is per-category when CategoryStrategyDefaults has a value for it and this category.
    /// </summary>
    public static class TraversalResolver
    {
        public static TraversalOrder Resolve(TraversalSettings general, TraversalSettings category,
            INumberingCategory numberingCategory, TraversalRegistry strategies, CategoryStrategyDefaults defaults)
        {
            if (numberingCategory == null)
            {
                throw new ArgumentNullException(nameof(numberingCategory));
            }

            if (strategies == null)
            {
                throw new ArgumentNullException(nameof(strategies));
            }

            defaults = defaults ?? new CategoryStrategyDefaults();
            ITraversalStrategy strategy = strategies.Get(general?.StrategyKey);
            var parameters = new Dictionary<string, string>(defaults.Resolve(numberingCategory, strategy),
                StringComparer.Ordinal);

            if (category?.Parameters != null
                && string.Equals(category.StrategyKey, strategy.Key, StringComparison.Ordinal))
            {
                Overlay(parameters, category.Parameters, key => true);
            }

            if (general?.Parameters != null)
            {
                Overlay(parameters, general.Parameters,
                    key => !defaults.IsPerCategory(numberingCategory.Key, strategy.Key, key));
            }

            return new TraversalOrder(strategy.Key, parameters);
        }

        private static void Overlay(Dictionary<string, string> target, IDictionary<string, string> source,
            Func<string, bool> accepts)
        {
            foreach (KeyValuePair<string, string> pair in source)
            {
                if (pair.Key != null && pair.Value != null && accepts(pair.Key))
                {
                    target[pair.Key] = pair.Value;
                }
            }
        }
    }
}
