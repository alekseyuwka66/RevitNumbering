namespace RevitNumbering.Core.Templates
{
    /// <summary>
    /// Parsed number template. Created only by TemplateParser.
    /// </summary>
    public sealed class NumberTemplate
    {
        internal NumberTemplate(string text, bool usesLevel, int counterWidth)
        {
            Text = text;
            UsesLevel = usesLevel;
            CounterWidth = counterWidth;
        }

        public string Text { get; }

        public bool UsesLevel { get; }

        /// <summary>Number of zeros in {N:000}; {N} gives 1.</summary>
        public int CounterWidth { get; }

        public string Format(string levelCode, int counter)
        {
            throw new NotImplementedException();
        }

        /// <summary>Whether an existing value matches the template for this level code, and its N.</summary>
        public bool TryMatch(string value, string levelCode, out int counter)
        {
            throw new NotImplementedException();
        }
    }
}
