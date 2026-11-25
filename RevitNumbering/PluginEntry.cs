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
            Log = FileLog.Create(application.ControlledApplication.VersionNumber);
            Log.Info("Plugin started");
        }

        public static void Shutdown(UIControlledApplication application)
        {
            Log?.Info("Plugin stopped");
            Log = null;
        }
    }
}
