using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitNumbering.Commands
{
    // Ribbon command: shows the plugin window, or activates the one that is already open.
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class OpenWindowCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Failures are caught, logged and shown inside.
            PluginEntry.OpenWindow(commandData.Application);
            return Result.Succeeded;
        }
    }
}
