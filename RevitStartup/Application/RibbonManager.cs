using Autodesk.Revit.UI;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace RevitStartup.Application
{
    /// <summary>
    /// Class for generating Revit tab and panels
    /// </summary>
    public class RibbonManager
    {
        // Texts of the ribbon.
        const string TabName = "RevitNumbering";
        const string PanelName = "Нумерация";
        const string ButtonText = "Нумерация";
        const string ButtonToolTip = "Нумерация элементов: стены, двери, окна, помещения";

        // The command lives in the plugin module assembly, deployed next to RevitStartup.dll.
        const string ModuleAssemblyName = "RevitNumbering.dll";
        const string OpenWindowCommandClass = "RevitNumbering.Commands.OpenWindowCommand";

        private static string ModuleAssemblyPath
        {
            get
            {
                string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                return Path.Combine(directory, ModuleAssemblyName);
            }
        }

        public static Result OnStartup(UIControlledApplication application)
        {
            try
            {
                application.CreateRibbonTab(TabName);
            }
            catch (Exception ex)
            {
                // The tab may already exist
                Debug.WriteLine(ex.Message);
            }

            RibbonPanel panel = application.CreateRibbonPanel(TabName, PanelName);

            var button = new PushButtonData("OpenWindowButton", ButtonText, ModuleAssemblyPath, OpenWindowCommandClass)
            {
                ToolTip = ButtonToolTip
            };
            panel.AddItem(button);

            return Result.Succeeded;
        }
    }
}
