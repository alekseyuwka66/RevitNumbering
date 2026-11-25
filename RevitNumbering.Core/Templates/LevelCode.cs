namespace RevitNumbering.Core.Templates
{
    public enum LevelCodePart
    {
        /// <summary>The whole level name.</summary>
        Whole,

        /// <summary>The word with the given number.</summary>
        Word,

        LastWord,
    }

    /// <summary>
    /// How the level code for the level placeholder is taken from the level name.
    /// </summary>
    public sealed class LevelCode
    {
        // Words are separated by a space and an underscore.
        private static readonly char[] Separators = { ' ', '_' };

        public LevelCodePart Part { get; set; }

        /// <summary>For Word, starting from 1.</summary>
        public int WordNumber { get; set; } = 1;

        public bool IsValid()
        {
            return Part != LevelCodePart.Word || WordNumber >= 1;
        }

        /// <summary>null: the name has no such part.</summary>
        public string Resolve(string levelName)
        {
            if (string.IsNullOrWhiteSpace(levelName))
            {
                return null;
            }

            if (Part == LevelCodePart.Whole)
            {
                return levelName;
            }

            string[] words = levelName.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            if (Part == LevelCodePart.LastWord)
            {
                return words.Length == 0 ? null : words[words.Length - 1];
            }

            return WordNumber >= 1 && WordNumber <= words.Length ? words[WordNumber - 1] : null;
        }
    }
}
