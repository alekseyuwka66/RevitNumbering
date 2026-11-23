using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitNumbering.UI.Windows.MainWindow;

namespace RevitNumbering.Commands
{
    /// <summary>
    /// Ribbon command: opens the plugin window.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class OpenWindowCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var viewModel = new MainWindowViewModel();
            var window = new MainWindow(viewModel);
            window.Show();
            return Result.Succeeded;
        }
    }
}
