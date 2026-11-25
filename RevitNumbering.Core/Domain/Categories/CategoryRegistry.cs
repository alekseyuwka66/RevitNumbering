namespace RevitNumbering.Core.Domain.Categories
{
    /// <summary>
    /// Read-only view of a category registry. Covariant: the adapter's CategoryRegistry&lt;IRevitCategory&gt;
    /// is passed to the window as ICategoryRegistry&lt;INumberingCategory&gt; without copying.
    /// </summary>
    public interface ICategoryRegistry<out TCategory> where TCategory : class, INumberingCategory
    {
        /// <summary>In registration order; the first one is the default category.</summary>
        IReadOnlyList<TCategory> All { get; }

        TCategory Default { get; }

        /// <summary>null: unknown key.</summary>
        TCategory Find(string key);

        /// <summary>Unknown or empty key gives the default category.</summary>
        TCategory GetOrDefault(string key);
    }

    /// <summary>
    /// Categories by key in registration order. Generic: the adapter keeps its own category type
    /// (CategoryRegistry&lt;IRevitCategory&gt;) without casts, the core sees the same objects as INumberingCategory.
    /// A new category = one class + one registration line.
    /// </summary>
    public sealed class CategoryRegistry<TCategory> : ICategoryRegistry<TCategory>
        where TCategory : class, INumberingCategory
    {
        private readonly Dictionary<string, TCategory> _byKey = new Dictionary<string, TCategory>(StringComparer.Ordinal);

        /// <param name="defaultKey">Key of the default category (Q-09); null: the first registered one.</param>
        public CategoryRegistry(IEnumerable<TCategory> categories, string defaultKey = null)
        {
            if (categories == null)
            {
                throw new ArgumentNullException(nameof(categories));
            }

            var all = new List<TCategory>();
            foreach (TCategory category in categories)
            {
                if (category == null)
                {
                    throw new ArgumentException("Categories must not contain null.", nameof(categories));
                }

                if (string.IsNullOrWhiteSpace(category.Key) || string.IsNullOrWhiteSpace(category.DisplayName))
                {
                    throw new ArgumentException("Category key and display name must not be empty.", nameof(categories));
                }

                if (category.DefaultTemplate == null)
                {
                    throw new ArgumentException("Category default template must not be null: " + category.Key,
                        nameof(categories));
                }

                if (_byKey.ContainsKey(category.Key))
                {
                    throw new ArgumentException("Category keys must be unique: " + category.Key, nameof(categories));
                }

                _byKey.Add(category.Key, category);
                all.Add(category);
            }

            if (all.Count == 0)
            {
                throw new ArgumentException("At least one category is required.", nameof(categories));
            }

            All = all.ToArray();
            Default = defaultKey == null
                ? All[0]
                : Find(defaultKey) ?? throw new ArgumentException("Unknown default category: " + defaultKey,
                    nameof(defaultKey));
        }

        /// <summary>In registration order (the order of the window list).</summary>
        public IReadOnlyList<TCategory> All { get; }

        /// <summary>Category for an empty or unknown key (Q-09: set by the composition root).</summary>
        public TCategory Default { get; }

        /// <summary>null: unknown key.</summary>
        public TCategory Find(string key)
        {
            return key != null && _byKey.TryGetValue(key, out TCategory category) ? category : null;
        }

        /// <summary>Unknown or empty key gives the default category (e.g. a key from an old settings file).</summary>
        public TCategory GetOrDefault(string key)
        {
            return Find(key) ?? Default;
        }
    }
}
