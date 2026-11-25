using Autodesk.Revit.UI;
using RevitNumbering.Core.Logging;

namespace RevitNumbering
{
    /// <summary>
    /// Composition root: created in OnStartup, lives until OnShutdown.
    /// </summary>
    public static class PluginEntry
    {
        internal static ILog Log { get; private set; }

        public static void Initialize(UIControlledApplication application)
        {
            string version = application.ControlledApplication.VersionNumber;
            string dataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RevitNumbering", version);
            Log = new FileLog(Path.Combine(dataFolder, "Logs"));
            Log.Info("[Startup] plugin started, Revit " + version);
        }

        public static void Shutdown(UIControlledApplication application)
        {
            Log?.Info("[Shutdown] plugin stopped");
            Log = null;
        }
    }
}
