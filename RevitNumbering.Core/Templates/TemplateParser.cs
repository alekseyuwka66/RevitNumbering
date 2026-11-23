using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Templates
{
    public sealed class TemplateParseResult
    {
        public TemplateParseResult(NumberTemplate template, IReadOnlyList<TemplateIssue> issues)
        {
            Template = template;
            Issues = DtoCollections.CopyList(issues);
            IsValid = Issues.All(issue => issue.IsWarning);
        }

        /// <summary>null on error.</summary>
        public NumberTemplate Template { get; }

        public IReadOnlyList<TemplateIssue> Issues { get; }

        /// <summary>No errors (warnings are allowed).</summary>
        public bool IsValid { get; }
    }

    public static class TemplateParser
    {
        public static TemplateParseResult Parse(string text)
        {
            throw new NotImplementedException();
        }
    }
}
