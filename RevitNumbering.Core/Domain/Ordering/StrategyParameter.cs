using System.Globalization;
using RevitNumbering.Core.Common;

namespace RevitNumbering.Core.Domain.Ordering
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
    /// Traversal strategy parameter. Either a choice (the window builds a drop-down list from Options)
    /// or a whole number in [MinValue; MaxValue] (the window builds an input field).
    /// The strategy owns its parameters and their defaults.
    /// </summary>
    public sealed class StrategyParameter
    {
        private StrategyParameter(string key, string displayName, IReadOnlyList<StrategyOption> options,
            string defaultValue, double minValue, double maxValue)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Parameter key must not be empty.", nameof(key));
            }

            Key = key;
            DisplayName = displayName;
            Options = DtoCollections.CopyList(options);
            DefaultValue = defaultValue;
            MinValue = minValue;
            MaxValue = maxValue;
        }

        public string Key { get; }

        public string DisplayName { get; }

        /// <summary>Empty: a numeric parameter.</summary>
        public IReadOnlyList<StrategyOption> Options { get; }

        public string DefaultValue { get; }

        public bool IsNumber => Options.Count == 0;

        public double MinValue { get; }

        public double MaxValue { get; }

        public static StrategyParameter Choice(string key, string displayName, IReadOnlyList<StrategyOption> options,
            string defaultValue)
        {
            if (options == null || options.Count == 0)
            {
                throw new ArgumentException("A choice parameter needs options.", nameof(options));
            }

            return new StrategyParameter(key, displayName, options, defaultValue, 0, 0);
        }

        public static StrategyParameter Number(string key, string displayName, double defaultValue,
            double minValue, double maxValue)
        {
            return new StrategyParameter(key, displayName, null,
                defaultValue.ToString(CultureInfo.InvariantCulture), minValue, maxValue);
        }

        /// <summary>Whether a value is allowed: one of the options or a whole number within the limits.</summary>
        public bool IsValid(string value)
        {
            if (value == null)
            {
                return false;
            }

            if (!IsNumber)
            {
                return Options.Any(option => string.Equals(option.Value, value, StringComparison.Ordinal));
            }

            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                && !double.IsNaN(number) && !double.IsInfinity(number)
                && Math.Floor(number) == number
                && number >= MinValue && number <= MaxValue;
        }
    }

    /// <summary>
    /// Parameter values for one Order call. A missing or invalid value gives the parameter default.
    /// </summary>
    public sealed class StrategyArguments
    {
        public static readonly StrategyArguments Empty = new StrategyArguments(null);

        private readonly IReadOnlyDictionary<string, string> _values;

        public StrategyArguments(IEnumerable<KeyValuePair<string, string>> values)
        {
            _values = DtoCollections.CopyDictionary(values);
        }

        public IReadOnlyDictionary<string, string> Values => _values;

        public string Get(StrategyParameter parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }

            return _values.TryGetValue(parameter.Key, out string value) && parameter.IsValid(value)
                ? value
                : parameter.DefaultValue;
        }

        public double GetNumber(StrategyParameter parameter)
        {
            return double.Parse(Get(parameter), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
