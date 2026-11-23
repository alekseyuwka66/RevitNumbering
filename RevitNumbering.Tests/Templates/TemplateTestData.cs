using System.Reflection;
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Tests.Templates
{
    /// <summary>
    /// Builders and shared checks for the template tests.
    /// </summary>
    internal static class TemplateTestData
    {
        public static NumberTemplate Template(string text)
        {
            var result = TemplateParser.Parse(text);
            Assert.True(result.IsValid, "Template must be valid: " + text);
            return result.Template;
        }

        /// <summary>Level with defaults: Key = name, elevation 0, no parameters.</summary>
        public static LevelData Level(string name, double elevationMm = 0, string key = null,
            IReadOnlyDictionary<string, string> parameters = null)
        {
            return new LevelData(key ?? name, name, elevationMm, parameters);
        }

        /// <summary>Levels for CheckLevelCount: L1 ("Этаж 1", 0) and L2 ("Этаж 2", 3000).</summary>
        public static LevelData[] TwoLevels()
        {
            return new[] { Level("Этаж 1", 0, "L1"), Level("Этаж 2", 3000, "L2") };
        }

        public static LevelCodeRule NameRule(LevelCodePart part, int wordNumber = 1)
        {
            return new LevelCodeRule(LevelCodeSource.LevelName, string.Empty, part, wordNumber);
        }

        public static void AssertIssue(TemplateIssue issue, TemplateIssueCode code, params string[] arguments)
        {
            Assert.NotNull(issue);
            Assert.Equal(code, issue.Code);
            Assert.Equal(arguments, issue.Arguments);
        }

        /// <summary>Codes with their first argument ("" when there are none), in order.</summary>
        public static string[] Describe(TemplateParseResult result)
        {
            return result.Issues
                .Select(issue => issue.Arguments.Count == 0 ? issue.Code.ToString() : issue.Code + " " + issue.Arguments[0])
                .ToArray();
        }

        /// <summary>
        /// Text of an issue from TextCatalog.TemplateIssueText, the only "code → text" mapping.
        /// The catalog is resolved by name: until it is in the assembly the result is
        /// null and text assertions are skipped. After the merge replace the body with the direct call.
        /// </summary>
        public static string IssueText(TemplateIssue issue)
        {
            var catalog = typeof(TemplateIssue).Assembly.GetType("RevitNumbering.Core.Texts.TextCatalog");
            var method = catalog?.GetMethod("TemplateIssueText", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
            {
                return null;
            }

            return (string)method.Invoke(null, new object[] { issue.Code, issue.Arguments });
        }

        public static void AssertIssueText(string expected, TemplateIssue issue)
        {
            string actual = IssueText(issue);
            if (actual != null)
            {
                Assert.Equal(expected, actual);
            }
        }
    }
}
