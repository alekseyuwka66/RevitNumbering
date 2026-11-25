using System.Text;
using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Templates
{
    /// <summary>Result of Template.Parse: the template, or the problems when it is null.</summary>
    public sealed class TemplateResult
    {
        public Template Template { get; set; }

        public List<Problem> Problems { get; set; } = new List<Problem>();
    }

    /// <summary>
    /// Parsed number template, for example "Д-{Уровень}-{N:000}". Text outside the braces is copied as is.
    /// </summary>
    public sealed class Template
    {
        private const char Open = '{';
        private const char Close = '}';

        // The list of placeholders: each line tries to build its part from the name inside the braces.
        private static readonly List<Func<string, TemplatePart>> Placeholders = new List<Func<string, TemplatePart>>
        {
            LevelPart.TryCreate,
            NumberPart.TryCreate,
        };

        private readonly List<TemplatePart> _parts;

        private Template(List<TemplatePart> parts)
        {
            _parts = parts;
            HasLevel = parts.Any(part => part is LevelPart);
            NumberPart number = parts.OfType<NumberPart>().FirstOrDefault();
            HasNumber = number != null;
            Width = number?.Width ?? 0;
        }

        public bool HasLevel { get; }

        public bool HasNumber { get; }

        /// <summary>Number of zeros in {N:000}; 0 for {N}.</summary>
        public int Width { get; }

        public string Format(string levelCode, int number)
        {
            var context = new NumberContext { LevelCode = levelCode, Number = number };
            var builder = new StringBuilder();
            foreach (TemplatePart part in _parts)
            {
                builder.Append(part.Render(context));
            }

            return builder.ToString();
        }

        /// <summary>Parses the text left to right. Reports only what is visible from the text.</summary>
        public static TemplateResult Parse(string text)
        {
            text = text ?? string.Empty;

            var result = new TemplateResult();
            var parts = new List<TemplatePart>();
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
                        AddProblem(result, ProblemCode.UnclosedBrace, null);
                    }
                    else if (i > textStart)
                    {
                        parts.Add(new TextPart(text.Substring(textStart, i - textStart)));
                    }

                    openIndex = i;
                }
                else if (c == Close)
                {
                    if (openIndex < 0)
                    {
                        AddProblem(result, ProblemCode.UnclosedBrace, null);
                    }
                    else
                    {
                        AddPlaceholder(text.Substring(openIndex + 1, i - openIndex - 1), parts, result);
                        openIndex = -1;
                    }

                    textStart = i + 1;
                }
            }

            if (openIndex >= 0)
            {
                AddProblem(result, ProblemCode.UnclosedBrace, null);
            }
            else if (text.Length > textStart)
            {
                parts.Add(new TextPart(text.Substring(textStart)));
            }

            bool hasNumber = parts.Any(part => part is NumberPart);
            if (!hasNumber && result.Problems.All(problem => problem.Code != ProblemCode.UnclosedBrace))
            {
                AddProblem(result, ProblemCode.NoNumber, null);
            }

            if (result.Problems.Count == 0)
            {
                result.Template = new Template(parts);
            }

            return result;
        }

        private static void AddPlaceholder(string name, List<TemplatePart> parts, TemplateResult result)
        {
            TemplatePart part = null;
            foreach (Func<string, TemplatePart> tryCreate in Placeholders)
            {
                part = tryCreate(name);
                if (part != null)
                {
                    break;
                }
            }

            if (part == null)
            {
                AddProblem(result, ProblemCode.UnknownPlaceholder, Open + name + Close);
                return;
            }

            if (part is NumberPart number)
            {
                // Every counter must be written like the first one.
                NumberPart first = parts.OfType<NumberPart>().FirstOrDefault();
                if (!number.IsValid || (first != null && first.IsValid && first.Width != number.Width))
                {
                    AddProblem(result, ProblemCode.BadWidth, Open + name + Close);
                }
            }

            parts.Add(part);
        }

        /// <summary>Problems with the same code and argument are reported once.</summary>
        private static void AddProblem(TemplateResult result, ProblemCode code, string arg)
        {
            if (result.Problems.Any(p => p.Code == code && p.Args.FirstOrDefault() == arg))
            {
                return;
            }

            result.Problems.Add(arg == null ? new Problem(code) : new Problem(code, arg));
        }
    }
}
