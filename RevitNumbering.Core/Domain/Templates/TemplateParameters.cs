namespace RevitNumbering.Core.Domain.Templates
{
    /// <summary>
    /// Parameters of the template module.
    /// </summary>
    public static class TemplateParameters
    {
        /// <summary>The largest number of zeros in {N:0…0}.</summary>
        public const int CounterMaxWidth = 9;

        /// <summary>Space (U+0020) and underscore.</summary>
        public const string LevelWordSeparators = " _";
    }
}
