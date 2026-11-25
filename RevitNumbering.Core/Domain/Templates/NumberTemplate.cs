using System.Globalization;
using System.Text;

namespace RevitNumbering.Core.Domain.Templates
{
    internal enum TemplatePartKind
    {
        Text,
        Level,
        Counter
    }

    /// <summary>One part of a parsed template; Text is set only for the Text kind.</summary>
    internal sealed class TemplatePart
    {
        public TemplatePart(TemplatePartKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        public TemplatePartKind Kind { get; }

        public string Text { get; }
    }

    /// <summary>
    /// Parsed number template. Created only by TemplateParser.
    /// </summary>
    public sealed class NumberTemplate
    {
        private readonly TemplatePart[] _parts;
        private readonly int _firstCounterIndex;

        internal NumberTemplate(string text, int counterWidth, IEnumerable<TemplatePart> parts)
        {
            Text = text;
            CounterWidth = counterWidth;
            _parts = parts.ToArray();
            UsesLevel = _parts.Any(part => part.Kind == TemplatePartKind.Level);
            _firstCounterIndex = Array.FindIndex(_parts, part => part.Kind == TemplatePartKind.Counter);
        }

        public string Text { get; }

        public bool UsesLevel { get; }

        /// <summary>Number of zeros in {N:000}; {N} gives 1.</summary>
        public int CounterWidth { get; }

        /// <summary>
        /// Builds a number. The counter is never truncated; levelCode is inserted as is.
        /// </summary>
        public string Format(string levelCode, int counter)
        {
            if (UsesLevel && levelCode == null)
            {
                throw new ArgumentNullException(nameof(levelCode));
            }

            if (counter < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(counter));
            }

            string digits = counter.ToString(CultureInfo.InvariantCulture).PadLeft(CounterWidth, '0');
            var builder = new StringBuilder(Text.Length + digits.Length + (levelCode?.Length ?? 0));
            foreach (var part in _parts)
            {
                builder.Append(PartText(part, levelCode, digits));
            }

            return builder.ToString();
        }

        /// <summary>Whether an existing value matches the template for this level code, and its N.</summary>
        public bool TryMatch(string value, string levelCode, out int counter)
        {
            counter = 0;
            if (value == null || (UsesLevel && levelCode == null) || _firstCounterIndex < 0)
            {
                return false;
            }

            value = value.Trim();

            // Everything before the first counter is literal.
            int position = 0;
            for (int i = 0; i < _firstCounterIndex; i++)
            {
                if (!ConsumeLiteral(value, ref position, PartText(_parts[i], levelCode, null)))
                {
                    return false;
                }
            }

            int digitRun = 0;
            while (position + digitRun < value.Length && IsAsciiDigit(value[position + digitRun]))
            {
                digitRun++;
            }

            // Greedy: the longest first counter wins; every other counter must be the same digit string.
            for (int length = digitRun; length >= 1; length--)
            {
                string digits = value.Substring(position, length);
                if (MatchesRest(value, position + length, levelCode, digits))
                {
                    return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out counter);
                }
            }

            return false;
        }

        private bool MatchesRest(string value, int position, string levelCode, string digits)
        {
            for (int i = _firstCounterIndex + 1; i < _parts.Length; i++)
            {
                if (!ConsumeLiteral(value, ref position, PartText(_parts[i], levelCode, digits)))
                {
                    return false;
                }
            }

            return position == value.Length;
        }

        private static string PartText(TemplatePart part, string levelCode, string digits)
        {
            switch (part.Kind)
            {
                case TemplatePartKind.Level:
                    return levelCode;
                case TemplatePartKind.Counter:
                    return digits;
                default:
                    return part.Text;
            }
        }

        private static bool ConsumeLiteral(string value, ref int position, string literal)
        {
            if (position + literal.Length > value.Length
                || string.CompareOrdinal(value, position, literal, 0, literal.Length) != 0)
            {
                return false;
            }

            position += literal.Length;
            return true;
        }

        private static bool IsAsciiDigit(char c)
        {
            return c >= '0' && c <= '9';
        }
    }
}
