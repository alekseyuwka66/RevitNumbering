using System.Windows.Controls;

namespace RevitNumbering.UI.Views
{
    public partial class ElementsTable : UserControl
    {
        public ElementsTable()
        {
            InitializeComponent();
        }

        // The table only shows data: nothing stays selected.
        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ((DataGrid)sender).UnselectAll();
        }
    }
}
