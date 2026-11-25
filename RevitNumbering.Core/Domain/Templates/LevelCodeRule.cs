using RevitNumbering.Core.Domain.Elements;

namespace RevitNumbering.Core.Domain.Templates
{
    public enum LevelCodeSource
    {
        LevelName,
        LevelParameter
    }

    public enum LevelCodePart
    {
        Whole,
        WordN,
        LastWord
    }

    /// <summary>
    /// How the level code for {Уровень} is taken from a level.
    /// </summary>
    public sealed class LevelCodeRule
    {
        private static readonly char[] Separators = TemplateParameters.LevelWordSeparators.ToCharArray();

        public LevelCodeRule(LevelCodeSource source, string parameterName, LevelCodePart part, int wordNumber)
        {
            Source = source;
            ParameterName = parameterName ?? string.Empty;
            Part = part;
            WordNumber = wordNumber;
        }

        public LevelCodeSource Source { get; }

        public string ParameterName { get; }

        public LevelCodePart Part { get; }

        /// <summary>For WordN, starting from 1.</summary>
        public int WordNumber { get; }

        /// <summary>null: the level has no such part.</summary>
        public string Resolve(LevelData level)
        {
            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            string source = SourceText(level);
            if (string.IsNullOrWhiteSpace(source))
            {
                return null;
            }

            if (Part == LevelCodePart.Whole)
            {
                return source;
            }

            string[] words = source.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            if (Part == LevelCodePart.LastWord)
            {
                return words.Length == 0 ? null : words[words.Length - 1];
            }

            return WordNumber >= 1 && WordNumber <= words.Length ? words[WordNumber - 1] : null;
        }

        private string SourceText(LevelData level)
        {
            if (Source == LevelCodeSource.LevelName)
            {
                return level.Name;
            }

            // Exact parameter name: no trimming and no case folding.
            if (ParameterName.Length == 0 || level.TextParameters == null)
            {
                return null;
            }

            return level.TextParameters.TryGetValue(ParameterName, out string value) ? value : null;
        }
    }
}
