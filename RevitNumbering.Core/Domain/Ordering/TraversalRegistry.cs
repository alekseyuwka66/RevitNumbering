namespace RevitNumbering.Core.Domain.Ordering
{
    /// <summary>
    /// Registry of traversal strategies (an instance, not a static class). The window builds the list
    /// of strategies and their parameters from it.
    /// </summary>
    public sealed class TraversalRegistry
    {
        private readonly ITraversalStrategy fallback;

        public TraversalRegistry(IEnumerable<ITraversalStrategy> strategies)
        {
            if (strategies == null)
            {
                throw new ArgumentNullException(nameof(strategies));
            }

            ITraversalStrategy[] all = strategies.ToArray();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (ITraversalStrategy strategy in all)
            {
                if (strategy == null)
                {
                    throw new ArgumentException("Strategies must not contain null.", nameof(strategies));
                }

                if (!keys.Add(strategy.Key))
                {
                    throw new ArgumentException("Strategy keys must be unique: " + strategy.Key,
                        nameof(strategies));
                }
            }

            fallback = Find(all, GridTraversal.StrategyKey);
            if (fallback == null)
            {
                throw new ArgumentException("The \"" + GridTraversal.StrategyKey + "\" strategy is required.",
                    nameof(strategies));
            }

            All = all;
        }

        /// <summary>Strategies in registration order.</summary>
        public IReadOnlyList<ITraversalStrategy> All { get; }

        /// <summary>null, empty or unknown key gives the "grid" strategy.</summary>
        public ITraversalStrategy Get(string key)
        {
            return Find(All, key) ?? fallback;
        }

        private static ITraversalStrategy Find(IReadOnlyList<ITraversalStrategy> strategies, string key)
        {
            foreach (ITraversalStrategy strategy in strategies)
            {
                if (string.Equals(strategy.Key, key, StringComparison.Ordinal))
                {
                    return strategy;
                }
            }

            return null;
        }
    }
}
