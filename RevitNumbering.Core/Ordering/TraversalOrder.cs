using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Ordering
{
    /// <summary>
    /// Chosen traversal: strategy key and its parameter values.
    /// </summary>
    public sealed class TraversalOrder
    {
        public TraversalOrder(string strategyKey, IReadOnlyDictionary<string, string> parameters)
        {
            StrategyKey = strategyKey;
            Parameters = DtoCollections.CopyDictionary(parameters);
        }

        public string StrategyKey { get; }

        public IReadOnlyDictionary<string, string> Parameters { get; }
    }

    public sealed class OrderItem
    {
        public OrderItem(long id, double xmm, double ymm)
        {
            Id = id;
            Xmm = xmm;
            Ymm = ymm;
        }

        public long Id { get; }

        public double Xmm { get; }

        public double Ymm { get; }
    }
}
