using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Ordering
{
    public sealed class StrategyOption
    {
        public StrategyOption(string value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public string Value { get; }

        public string DisplayName { get; }
    }

    /// <summary>
    /// Traversal strategy parameter: the window builds a drop-down list from it.
    /// </summary>
    public sealed class StrategyParameter
    {
        public StrategyParameter(string key, string displayName, IReadOnlyList<StrategyOption> options,
            string defaultValue)
        {
            Key = key;
            DisplayName = displayName;
            Options = DtoCollections.CopyList(options);
            DefaultValue = defaultValue;
        }

        public string Key { get; }

        public string DisplayName { get; }

        public IReadOnlyList<StrategyOption> Options { get; }

        public string DefaultValue { get; }
    }
}
