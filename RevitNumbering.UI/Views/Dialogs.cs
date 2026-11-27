using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using RevitNumbering.UI.Resources;
using RevitNumbering.UI.ViewModels;

namespace RevitNumbering.UI.Views
{
    public sealed class Dialogs
    {
        private readonly Window _owner;

        public Dialogs(Window owner)
        {
            _owner = owner;
        }

        // Hides (not minimizes) the window, so Revit is free for picking in the model.
        public void HideWindow()
        {
            _owner.Hide();
        }

        public void RestoreWindow()
        {
            _owner.Show();
            _owner.WindowState = WindowState.Normal;
            _owner.Activate();
        }

        // Minimizes the window, so the shown element is visible in Revit.
        public void MinimizeWindow()
        {
            _owner.WindowState = WindowState.Minimized;
        }

        public void ShowMessage(string text)
        {
            MessageBox.Show(_owner, text, Texts.WindowTitle, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowSummary(SummaryData summary)
        {
            new SummaryWindow { Owner = _owner, DataContext = summary }.ShowDialog();
        }

        public void ShowError(string logFilePath)
        {
            new ErrorWindow(logFilePath) { Owner = _owner }.ShowDialog();
        }
    }

    // Checks a whole number in a range. An invalid value does not reach the settings.
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
