namespace RevitNumbering.Core.Model
{
    public enum ProblemKind
    {
        // The scope has several levels, but the level is not part of the number.
        NeedLevel,

        // The same number is given on several levels.
        DuplicateNumber,
    }

    // A problem that blocks Apply. The UI builds the text. A level name "" means "no level".
    public sealed class Problem
    {
        public Problem(ProblemKind kind, string text = "", List<string> levels = null)
        {
            Kind = kind;
            Text = text;
            Levels = levels ?? new List<string>();
        }

        public ProblemKind Kind { get; }

        public string Text { get; }

        public List<string> Levels { get; }
    }
}
