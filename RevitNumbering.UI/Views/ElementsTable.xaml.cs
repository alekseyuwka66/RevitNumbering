using System.Windows.Controls;
using System.Windows.Input;
using RevitNumbering.UI.ViewModels;

namespace RevitNumbering.UI.Views
{
    /// <summary>The table of elements with its empty states, counters and the hint about skipped numbers.</summary>
    public partial class ElementsTable : UserControl
    {
        public ElementsTable()
        {
            InitializeComponent();
        }

        /// <summary>A mouse click on a row: the command zooms to the element in Revit.</summary>
        private void OnRowMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row && row.Item is TableRow tableRow && DataContext is MainViewModel viewModel)
            {
                viewModel.ShowElementCommand.Execute(tableRow);
            }
        }
    }
}
