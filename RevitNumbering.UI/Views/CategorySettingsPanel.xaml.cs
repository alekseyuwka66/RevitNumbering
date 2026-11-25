using System.Windows.Controls;
using System.Windows.Data;
using RevitNumbering.UI.ViewModels;

namespace RevitNumbering.UI.Views
{
    /// <summary>Settings of the current category: template, level code, ordering strategy.</summary>
    public partial class CategorySettingsPanel : UserControl
    {
        public CategorySettingsPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// One handler for every strategy editor: their bindings notify when a value reaches
        /// the strategy object, and the numbers are calculated again.
        /// </summary>
        private void OnEditorSourceUpdated(object sender, DataTransferEventArgs e)
        {
            (DataContext as CategorySettingsViewModel)?.NotifyEditorChanged();
        }
    }
}
