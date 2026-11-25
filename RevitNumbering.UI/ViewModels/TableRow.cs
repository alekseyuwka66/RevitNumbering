using System.Globalization;

namespace RevitNumbering.UI.ViewModels
{
    /// <summary>One row of the table with ready texts. Built from the preview after every recalculation.</summary>
    public sealed class TableRow
    {
        public long Id { get; set; }

        public string Name { get; set; }

        /// <summary>"Без уровня" when the element has no level.</summary>
        public string Level { get; set; }

        public string Current { get; set; }

        public string New { get; set; }

        public string Status { get; set; }
    }

    /// <summary>
    /// The search of the table: case-insensitive, by a part of the text, over the shown values of
    /// ID, Name, Level and Current value. The new value does not take part.
    /// </summary>
    public static class RowFilter
    {
        public static bool Matches(TableRow row, string text)
        {
            text = text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }

            return Contains(row.Id.ToString(CultureInfo.InvariantCulture), text)
                || Contains(row.Name, text)
                || Contains(row.Level, text)
                || Contains(row.Current, text);
        }

        private static bool Contains(string value, string text)
        {
            return value != null && value.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>An item of a list in the window: a value and its text.</summary>
    public sealed class Option<T>
    {
        public Option(T value, string title)
        {
            Value = value;
            Title = title;
        }

        public T Value { get; }

        public string Title { get; }
    }

    /// <summary>What the summary window shows after writing.</summary>
    public sealed class SummaryData
    {
        public string WrittenText { get; set; }

        public string NotWrittenText { get; set; }

        /// <summary>"ID, name — reason" for every element that was not written.</summary>
        public List<string> Failures { get; set; } = new List<string>();

        public bool HasFailures => Failures.Count > 0;
    }
}
