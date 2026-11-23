using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Templates
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
        public LevelCodeRule(LevelCodeSource source, string parameterName, LevelCodePart part, int wordNumber)
        {
            Source = source;
            ParameterName = parameterName;
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
            throw new NotImplementedException();
        }
    }
}
