using System.Collections.Specialized;
using System.IO;
using System.Windows;
using RevitNumbering.UI.Resources;

namespace RevitNumbering.UI.Views
{
    // The window of an unexpected error. "Copy log" puts the log file itself on the clipboard,
    // so the user can attach it to a mail or a message.
    public partial class ErrorWindow : Window
    {
        private readonly string _logFilePath;

        public ErrorWindow(string logFilePath)
        {
            InitializeComponent();
            _logFilePath = logFilePath;
        }

        private void OnCopyLogClick(object sender, RoutedEventArgs e)
        {
            try
            {
                // Copy the file: the log file itself may be written at any moment.
                string folder = Path.Combine(Path.GetTempPath(), "RevitNumbering");
                Directory.CreateDirectory(folder);
                string copy = Path.Combine(folder, Path.GetFileName(_logFilePath));
                File.Copy(_logFilePath, copy, true);
                Clipboard.SetFileDropList(new StringCollection { copy });
                CopyResult.Text = Texts.ErrorLogCopied;
            }
            catch (Exception)
            {
                // No log, no access or a busy clipboard: the user still gets the path of the log.
                CopyResult.Text = string.Format(Texts.ErrorLogNotCopied, _logFilePath);
            }
        }
    }
}
