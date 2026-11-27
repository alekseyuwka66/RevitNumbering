using System.IO;
using System.Reflection;
using System.Windows;
using Autodesk.Revit.UI;
using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Session;
using RevitNumbering.Core.Settings;
using RevitNumbering.Revit;
using RevitNumbering.Revit.Categories;
using RevitNumbering.Revit.Documents;
using RevitNumbering.Revit.Runner;
using RevitNumbering.UI.Resources;
using RevitNumbering.UI.ViewModels;
using RevitNumbering.UI.Views;

namespace RevitNumbering
{
    // Composition root and the entry points called by Revit: start, the ribbon command, shutdown.
    // OnStartup builds the objects, and they live until OnShutdown. It is the only place that knows
    // the list of categories. No entry point lets an exception reach Revit.
    public static class PluginEntry
    {
        private const string DataFolderName = "RevitNumbering";
        private const string LogFolderName = "Logs";
        private const string SettingsFileName = "settings.json";

        private static FileLog _log;
        private static bool _isStarted;
        private static string _settingsPath;
        private static RevitDocuments _documents;
        private static RevitRunner _runner;
        private static RevitModel _model;
        private static MainWindow _window;

        public static void Initialize(UIControlledApplication application)
        {
            // Settings and logs are per Revit version: %LOCALAPPDATA%\RevitNumbering\<version>\
            string dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                DataFolderName, application.ControlledApplication.VersionNumber);
            _log = new FileLog(Path.Combine(dataFolder, LogFolderName));

            try
            {
                _log.DeleteOldFiles();

                // A new category is one line here. The order is the order in the window.
                var categories = new List<RevitCategory>
                {
                    new WallsCategory(),
                    new DoorsCategory(),
                    new WindowsCategory(),
                    new RoomsCategory(),
                };

                // Before subscribing to Revit: every category needs a record in the embedded defaults.
                SettingsStore.CheckDefaults(categories.Select(category => category.Key).ToList());

                _settingsPath = Path.Combine(dataFolder, SettingsFileName);
                _documents = new RevitDocuments(application, _log, Texts.TransactionName);
                _documents.ApplicationClosing += CloseWindow;

                // ExternalEvent must be created here, in the OnStartup context.
                _runner = new RevitRunner(_documents, _log);
                _model = new RevitModel(categories, _documents);

                _isStarted = true;
                _log.Info("[Startup] Revit " + application.ControlledApplication.VersionName + " build "
                    + application.ControlledApplication.VersionBuild + ", plugin "
                    + Assembly.GetExecutingAssembly().GetName().Version);
            }
            catch (Exception ex)
            {
                _log.Error("[Startup] the plugin did not start", ex);
            }
        }

        public static void OpenWindow(UIApplication application)
        {
            if (!_isStarted)
            {
                TaskDialog.Show(Texts.WindowTitle, Texts.PluginNotStarted);
                return;
            }

            try
            {
                ShowWindow(application);
            }
            catch (Exception ex)
            {
                _log.Error("[OpenWindow] unhandled exception", ex);
                TaskDialog.Show(Texts.WindowTitle, string.Format(Texts.OpenFailed, _log.FilePath));
            }
        }

        public static void Shutdown()
        {
            if (_log == null)
            {
                return;
            }

            // Every step is wrapped: a failure in one does not stop the others.
            SafeRunner.Run(_log, "Shutdown.Window", CloseWindow);
            SafeRunner.Run(_log, "Shutdown.Documents", () => _documents?.Unsubscribe());
            SafeRunner.Run(_log, "Shutdown.Runner", () => _runner?.Dispose());
            _isStarted = false;
            _log.Info("[Shutdown] plugin stopped");
        }

        // For the loader: a failure outside the plugin (the ribbon) goes to the same log. Never throws.
        public static void LogError(string operation, Exception exception)
        {
            _log?.Error("[" + operation + "] failed", exception);
        }

        // One window at a time: a second call restores and activates the open window.
        private static void ShowWindow(UIApplication application)
        {
            if (_window != null)
            {
                if (_window.WindowState == WindowState.Minimized)
                {
                    _window.WindowState = WindowState.Normal;
                }

                _window.Activate();
                return;
            }

            _documents.Refresh(application);
            var session = new NumberingSession(_runner, _model, _documents, new SettingsStore(_settingsPath, _log), _log);
            _window = new MainWindow(new MainViewModel(session, _log), application.MainWindowHandle);
            _window.Closed += (sender, e) => _window = null;
            _window.Show();
        }

        // Closing the window also saves the settings.
        private static void CloseWindow()
        {
            _window?.Close();
        }
    }
}
