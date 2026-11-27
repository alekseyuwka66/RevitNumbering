using System.Globalization;

namespace RevitNumbering.UI.ViewModels
{
    // One table row with ready texts. It is built from the preview after every recalculation.
    public sealed class TableRow
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public string Level { get; set; }

        public string Current { get; set; }

        public string New { get; set; }

        public string Status { get; set; }
    }

    public enum SearchColumn
    {
        Id,
        Name,
        Level,
        Current,
    }

    // The search of the table: ignores case and finds a part of the text in ID, name, level or current value.
    // The new value and the status are not searched.
    public static class RowFilter
    {
        private static readonly SearchColumn[] AllColumns =
            { SearchColumn.Id, SearchColumn.Name, SearchColumn.Level, SearchColumn.Current };

        public static bool Matches(TableRow row, string text, SearchColumn? column = null)
        {
            text = text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }

            if (column != null)
            {
                return Contains(ValueOf(row, column.Value), text);
            }

            return AllColumns.Any(c => Contains(ValueOf(row, c), text));
        }

        private static string ValueOf(TableRow row, SearchColumn column)
        {
            switch (column)
            {
                case SearchColumn.Id:
                    return row.Id.ToString(CultureInfo.InvariantCulture);
                case SearchColumn.Name:
                    return row.Name;
                case SearchColumn.Level:
                    return row.Level;
                default:
                    return row.Current;
            }
        }

        private static bool Contains(string value, string text)
        {
            return value != null && value.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    // An item of a list in the window: a value and its title.
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

    // What the summary window shows after writing.
    public sealed class SummaryData
    {
        public string WrittenText { get; set; }

        public string NotWrittenText { get; set; }

        // "ID, name - reason" for every element that was not written.
        public List<string> Failures { get; set; } = new List<string>();

        public bool HasFailures => Failures.Count > 0;
    }
}
