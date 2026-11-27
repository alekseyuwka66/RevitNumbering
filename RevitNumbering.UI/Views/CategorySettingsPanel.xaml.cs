using System.Windows.Controls;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using RevitNumbering.UI.ViewModels;

namespace RevitNumbering.UI.Views
{
    public partial class CategorySettingsPanel : UserControl
    {
        public CategorySettingsPanel()
        {
            InitializeComponent();
        }

        // The width is one digit from 1 to 9. A typed digit replaces the old one. Anything else is ignored.
        private void OnWidthTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = true;
            if (e.Text.Length == 1 && e.Text[0] >= '1' && e.Text[0] <= '9')
            {
                var box = (TextBox)sender;
                box.Text = e.Text;
                box.CaretIndex = box.Text.Length;
            }
        }

        // Pasting is not allowed: only typed digits.
        private void OnWidthPasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
        }

        // One handler for all strategy editors. Their bindings use NotifyOnSourceUpdated,
        // so every value that reaches the strategy object comes here, and the numbers are calculated again.
        private void OnEditorSourceUpdated(object sender, DataTransferEventArgs e)
        {
            (DataContext as CategorySettingsViewModel)?.NotifyEditorChanged();
        }
    }
}
