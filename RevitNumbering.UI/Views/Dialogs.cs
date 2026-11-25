using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RevitNumbering.UI.Resources;
using RevitNumbering.UI.ViewModels;

namespace RevitNumbering.UI.Views
{
    /// <summary>Dialogs opened from the main window: messages, file choice, the summary and the error window.</summary>
    public sealed class Dialogs
    {
        private readonly Window _owner;

        public Dialogs(Window owner)
        {
            _owner = owner;
        }

        public void ShowMessage(string text)
        {
            MessageBox.Show(_owner, text, Texts.WindowTitle, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>true: save; false: do not save; null: cancel.</summary>
        public bool? AskSave()
        {
            switch (MessageBox.Show(_owner, Texts.AskSave, Texts.WindowTitle, MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question))
            {
                case MessageBoxResult.Yes:
                    return true;
                case MessageBoxResult.No:
                    return false;
                default:
                    return null;
            }
        }

        /// <summary>null: the user cancelled.</summary>
        public string PickExportPath()
        {
            var dialog = new SaveFileDialog { Filter = Texts.JsonFilter, FileName = Texts.ExportFileName };
            return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
        }

        /// <summary>null: the user cancelled.</summary>
        public string PickImportPath()
        {
            var dialog = new OpenFileDialog { Filter = Texts.JsonFilter };
            return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
        }

        public void ShowSummary(SummaryData summary)
        {
            new SummaryWindow { Owner = _owner, DataContext = summary }.ShowDialog();
        }

        /// <summary>The window of an unexpected error; the plugin window stays usable.</summary>
        public void ShowError(string logFilePath)
        {
            new ErrorWindow(logFilePath) { Owner = _owner }.ShowDialog();
        }
    }

    /// <summary>Input check of a whole number within a range; an invalid value does not reach the settings.</summary>
    public sealed class IntRangeRule : ValidationRule
    {
        public int Min { get; set; }

        public int Max { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            return int.TryParse(value as string, NumberStyles.Integer, cultureInfo, out int number)
                && number >= Min && number <= Max
                ? ValidationResult.ValidResult
                : new ValidationResult(false, string.Format(Texts.ValidationRange, Min, Max));
        }
    }
}
