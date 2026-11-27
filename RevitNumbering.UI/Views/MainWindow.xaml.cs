using System.Windows;
using System.Windows.Interop;
using RevitNumbering.UI.ViewModels;

namespace RevitNumbering.UI.Views
{
    // The plugin window. Not modal, owned by the main window of Revit.
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel, IntPtr ownerHandle)
        {
            InitializeComponent();
            viewModel.Dialogs = new Dialogs(this);
            DataContext = viewModel;
            new WindowInteropHelper(this).Owner = ownerHandle;

            Loaded += (sender, e) => viewModel.OpenCommand.Execute(null);

            // The settings are saved when the window closes, also when Revit closes it.
            Closed += (sender, e) => viewModel.Close();
        }
    }
}
