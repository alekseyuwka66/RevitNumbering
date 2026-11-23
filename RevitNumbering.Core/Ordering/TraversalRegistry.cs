namespace RevitNumbering.Core.Ordering
{
    /// <summary>
    /// Registry of traversal strategies (an instance, not a static class).
    /// </summary>
    public sealed class TraversalRegistry
    {
        public TraversalRegistry(IEnumerable<ITraversalStrategy> strategies)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyList<ITraversalStrategy> All { get; }

        /// <summary>Unknown key gives the "grid" strategy.</summary>
        public ITraversalStrategy Get(string key)
        {
            throw new NotImplementedException();
        }
    }
}
