namespace RevitNumbering.Core.Model
{
    /// <summary>An expected problem as a code with arguments; the text is built by the UI.</summary>
    public sealed class Problem
    {
        public Problem()
        {
        }

        public Problem(ProblemCode code, params string[] args)
        {
            Code = code;
            Args = args.ToList();
        }

        public ProblemCode Code { get; set; }

        public List<string> Args { get; set; } = new List<string>();
    }

    public enum ProblemCode
    {
        /// <summary>A brace without its pair.</summary>
        UnclosedBrace,

        /// <summary>Args: the placeholder as written.</summary>
        UnknownPlaceholder,

        /// <summary>The template has no {N}.</summary>
        NoNumber,

        /// <summary>Args: the placeholder as written.</summary>
        BadWidth,

        /// <summary>No level placeholder while the scope has several levels. Args: level names ("" for no level).</summary>
        NeedLevel,

        /// <summary>Level names have no requested part. Args: level names.</summary>
        LevelPartMissing,

        /// <summary>The same number is given twice inside the scope. Args: the number, then level names ("" for no level).</summary>
        DuplicateInScope,

        /// <summary>Warning: the counter does not fit the width. Args: the largest number, the width.</summary>
        WidthTooSmall,
    }
}
