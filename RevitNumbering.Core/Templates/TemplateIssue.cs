using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Templates
{
    public enum TemplateIssueCode
    {
        UnclosedBrace,
        UnknownPlaceholder,
        MissingCounter,
        InvalidWidth,
        MissingLevelForManyLevels,
        LevelCodePartMissing,
        WidthTooSmall
    }

    public sealed class TemplateIssue
    {
        public TemplateIssue(TemplateIssueCode code, IReadOnlyList<string> arguments)
        {
            Code = code;
            IsWarning = code == TemplateIssueCode.WidthTooSmall;
            Arguments = DtoCollections.CopyList(arguments);
        }

        public TemplateIssueCode Code { get; }

        /// <summary>true only for WidthTooSmall.</summary>
        public bool IsWarning { get; }

        /// <summary>Placeholder, level list and so on.</summary>
        public IReadOnlyList<string> Arguments { get; }
    }
}
