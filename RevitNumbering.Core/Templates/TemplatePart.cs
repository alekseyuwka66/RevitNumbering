using System.Globalization;

namespace RevitNumbering.Core.Templates
{
    /// <summary>Values a template part can render for one element.</summary>
    public sealed class NumberContext
    {
        public string LevelCode { get; set; }

        public int Number { get; set; }
    }

    /// <summary>
    /// One part of a parsed template. A new placeholder = a new part class + a line in Template.Placeholders.
    /// </summary>
    public abstract class TemplatePart
    {
        public abstract string Render(NumberContext context);
    }

    /// <summary>Text outside the braces, copied as is.</summary>
    public sealed class TextPart : TemplatePart
    {
        public TextPart(string text)
        {
            Text = text;
        }

        public string Text { get; }

        public override string Render(NumberContext context)
        {
            return Text;
        }
    }

    /// <summary>{Уровень} or {Level}: the level code.</summary>
    public sealed class LevelPart : TemplatePart
    {
        private const string NameRu = "Уровень";
        private const string NameEn = "Level";

        public static TemplatePart TryCreate(string name)
        {
            return name == NameRu || name == NameEn ? new LevelPart() : null;
        }

        public override string Render(NumberContext context)
        {
            return context.LevelCode;
        }
    }

    /// <summary>{N} or {N:000}: the counter; the width is the number of zeros (0 for {N}).</summary>
    public sealed class NumberPart : TemplatePart
    {
        public const int MaxWidth = 9;

        private const string Name = "N";
        private const string NameWithWidth = "N:";

        private NumberPart(int width, bool isValid)
        {
            Width = width;
            IsValid = isValid;
        }

        public int Width { get; }

        /// <summary>false: the width is written incorrectly, for example {N:abc}.</summary>
        public bool IsValid { get; }

        public static TemplatePart TryCreate(string name)
        {
            if (name == Name)
            {
                return new NumberPart(0, true);
            }

            if (!name.StartsWith(NameWithWidth, StringComparison.Ordinal))
            {
                return null;
            }

            string zeros = name.Substring(NameWithWidth.Length);
            bool valid = zeros.Length >= 1 && zeros.Length <= MaxWidth && zeros.All(c => c == '0');
            return new NumberPart(valid ? zeros.Length : 0, valid);
        }

        public override string Render(NumberContext context)
        {
            return context.Number.ToString(CultureInfo.InvariantCulture).PadLeft(Width, '0');
        }
    }
}
