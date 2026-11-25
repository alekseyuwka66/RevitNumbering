namespace RevitNumbering.Core.Domain.Ordering
{
    /// <summary>
    /// Traversal strategy. Knows nothing about categories: it sees only OrderItem (id and point)
    /// and its own parameters. A new way of ordering = one implementation + one registration line.
    /// </summary>
    public interface ITraversalStrategy
    {
        string Key { get; }

        string DisplayName { get; }

        IReadOnlyList<StrategyParameter> Parameters { get; }

        /// <summary>Ids in numbering order. Deterministic.</summary>
        IReadOnlyList<long> Order(IReadOnlyList<OrderItem> items, StrategyArguments arguments);
    }
}
