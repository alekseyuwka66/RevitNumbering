namespace RevitNumbering.Core.Ordering
{
    /// <summary>
    /// Traversal strategy. A new way of ordering = one implementation + one registration line.
    /// </summary>
    public interface ITraversalStrategy
    {
        string Key { get; }

        string DisplayName { get; }

        IReadOnlyList<StrategyParameter> Parameters { get; }

        /// <summary>Ids in numbering order. Deterministic.</summary>
        IReadOnlyList<long> Order(IReadOnlyList<OrderItem> items, IReadOnlyDictionary<string, string> parameters,
            double rowToleranceMm);
    }
}
