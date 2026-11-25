using RevitNumbering.Core.Domain.Categories;
using RevitNumbering.Core.Domain.Ordering;

namespace RevitNumbering.Core.Settings
{
    /// <summary>
    /// Defaults of strategy parameters for a category (F6-05: row tolerance 500 / 1000 / 3000 mm).
    /// The only place that links a category and a strategy: neither of them knows about the other.
    /// Filled once in the composition root of the adapter.
    /// </summary>
    public sealed class CategoryStrategyDefaults
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);

        public CategoryStrategyDefaults Set(string categoryKey, string strategyKey, string parameterKey, string value)
        {
            if (categoryKey == null || strategyKey == null || parameterKey == null)
            {
                throw new ArgumentNullException(categoryKey == null ? nameof(categoryKey)
                    : strategyKey == null ? nameof(strategyKey) : nameof(parameterKey));
            }

            _values[Compose(categoryKey, strategyKey, parameterKey)] = value;
            return this;
        }

        /// <summary>null: no override, the strategy default applies.</summary>
        public string Find(string categoryKey, string strategyKey, string parameterKey)
        {
            if (categoryKey == null || strategyKey == null || parameterKey == null)
            {
                return null;
            }

            return _values.TryGetValue(Compose(categoryKey, strategyKey, parameterKey), out string value) ? value : null;
        }

        /// <summary>
        /// Per-category parameter (F6-05): it has a value for this category. Its value is stored in the category
        /// settings; other parameters are stored in the general traversal (F6-01: one order for all categories).
        /// </summary>
        public bool IsPerCategory(string categoryKey, string strategyKey, string parameterKey)
        {
            return Find(categoryKey, strategyKey, parameterKey) != null;
        }

        /// <summary>All parameters of the strategy for the category: override or the strategy default.</summary>
        public IDictionary<string, string> Resolve(INumberingCategory category, ITraversalStrategy strategy)
        {
            if (category == null)
            {
                throw new ArgumentNullException(nameof(category));
            }

            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (StrategyParameter parameter in strategy.Parameters)
            {
                string value = Find(category.Key, strategy.Key, parameter.Key);
                result[parameter.Key] = value != null && parameter.IsValid(value) ? value : parameter.DefaultValue;
            }

            return result;
        }

        /// <summary>
        /// Checks every stored value against the registries (AA-64: a mistake shows at plugin startup):
        /// known category, known strategy, known parameter, allowed value.
        /// </summary>
        public void Validate(ICategoryRegistry<INumberingCategory> categories, TraversalRegistry strategies)
        {
            if (categories == null)
            {
                throw new ArgumentNullException(nameof(categories));
            }

            if (strategies == null)
            {
                throw new ArgumentNullException(nameof(strategies));
            }

            foreach (KeyValuePair<string, string> pair in _values)
            {
                string[] parts = pair.Key.Split('\u001F');
                ITraversalStrategy strategy = strategies.All.FirstOrDefault(
                    s => string.Equals(s.Key, parts[1], StringComparison.Ordinal));
                StrategyParameter parameter = strategy?.Parameters.FirstOrDefault(
                    p => string.Equals(p.Key, parts[2], StringComparison.Ordinal));
                if (categories.Find(parts[0]) == null || parameter == null || !parameter.IsValid(pair.Value))
                {
                    throw new ArgumentException("Invalid strategy default: category " + parts[0] + ", strategy "
                        + parts[1] + ", parameter " + parts[2] + ", value " + pair.Value + ".");
                }
            }
        }

        private static string Compose(string categoryKey, string strategyKey, string parameterKey)
        {
            return categoryKey + "\u001F" + strategyKey + "\u001F" + parameterKey;
        }
    }
}
