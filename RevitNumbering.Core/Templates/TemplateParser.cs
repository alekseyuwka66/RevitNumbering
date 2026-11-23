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

    /// <summary>
    /// Parses the template text left to right. Reports only what is visible from the text.
    /// </summary>
    public static class TemplateParser
    {
        private const char Open = '{';
        private const char Close = '}';
        private const string LevelRu = "Уровень";
        private const string LevelEn = "Level";
        private const string Counter = "N";
        private const string CounterWithWidth = "N:";

        public static TemplateParseResult Parse(string text)
        {
            text = text ?? string.Empty;

            var issues = new List<TemplateIssue>();
            var parts = new List<TemplatePart>();
            string firstCounter = null; // content of the first counter placeholder, valid or not
            int counterWidth = 1;
            int textStart = 0;
            int openIndex = -1;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == Open)
                {
                    if (openIndex >= 0)
                    {
                        // The previous brace was never closed; parsing goes on from the new one.
                        AddIssue(issues, TemplateIssueCode.UnclosedBrace, null);
                    }
                    else if (i > textStart)
                    {
                        parts.Add(new TemplatePart(TemplatePartKind.Text, text.Substring(textStart, i - textStart)));
                    }

                    openIndex = i;
                }
                else if (c == Close)
                {
                    if (openIndex < 0)
                    {
                        AddIssue(issues, TemplateIssueCode.UnclosedBrace, null);
                        if (i > textStart)
                        {
                            parts.Add(new TemplatePart(TemplatePartKind.Text,
                                text.Substring(textStart, i - textStart)));
                        }
                    }
                    else
                    {
                        string content = text.Substring(openIndex + 1, i - openIndex - 1);
                        AddPlaceholder(content, parts, issues, ref firstCounter, ref counterWidth);
                        openIndex = -1;
                    }

                    textStart = i + 1;
                }
            }

            if (openIndex >= 0)
            {
                AddIssue(issues, TemplateIssueCode.UnclosedBrace, null);
            }
            else if (text.Length > textStart)
            {
                parts.Add(new TemplatePart(TemplatePartKind.Text, text.Substring(textStart)));
            }

            if (firstCounter == null && issues.All(issue => issue.Code != TemplateIssueCode.UnclosedBrace))
            {
                AddIssue(issues, TemplateIssueCode.MissingCounter, null);
            }

            var template = issues.Count == 0 ? new NumberTemplate(text, counterWidth, parts) : null;
            return new TemplateParseResult(template, issues);
        }

        private static void AddPlaceholder(string content, List<TemplatePart> parts, List<TemplateIssue> issues,
            ref string firstCounter, ref int counterWidth)
        {
            if (string.Equals(content, LevelRu, StringComparison.Ordinal)
                || string.Equals(content, LevelEn, StringComparison.Ordinal))
            {
                parts.Add(new TemplatePart(TemplatePartKind.Level, null));
                return;
            }

            bool plainCounter = string.Equals(content, Counter, StringComparison.Ordinal);
            if (!plainCounter && !content.StartsWith(CounterWithWidth, StringComparison.Ordinal))
            {
                AddIssue(issues, TemplateIssueCode.UnknownPlaceholder, Open + content + Close);
                return;
            }

            int width = plainCounter ? 1 : WidthOf(content);
            if (width < 1)
            {
                AddIssue(issues, TemplateIssueCode.InvalidWidth, Open + content + Close);
            }
            else if (firstCounter != null && !string.Equals(content, firstCounter, StringComparison.Ordinal))
            {
                // Every counter must be written like the first one.
                AddIssue(issues, TemplateIssueCode.InvalidWidth, Open + content + Close);
            }

            if (firstCounter == null)
            {
                firstCounter = content;
                counterWidth = Math.Max(width, 1);
            }

            parts.Add(new TemplatePart(TemplatePartKind.Counter, null));
        }

        /// <summary>Number of zeros after "N:", or 0 when the width is written incorrectly.</summary>
        private static int WidthOf(string content)
        {
            int width = content.Length - CounterWithWidth.Length;
            if (width < 1 || width > TemplateParameters.CounterMaxWidth)
            {
                return 0;
            }

            for (int i = CounterWithWidth.Length; i < content.Length; i++)
            {
                if (content[i] != '0')
                {
                    return 0;
                }
            }

            return width;
        }

        /// <summary>Issues with the same code and arguments are reported once.</summary>
        private static void AddIssue(List<TemplateIssue> issues, TemplateIssueCode code, string argument)
        {
            foreach (var issue in issues)
            {
                if (issue.Code == code
                    && string.Equals(issue.Arguments.FirstOrDefault(), argument, StringComparison.Ordinal))
                {
                    return;
                }
            }

            issues.Add(new TemplateIssue(code, argument == null ? null : new[] { argument }));
        }
    }
}
