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
    /// <summary>
    /// Composition root: builds the objects of the plugin in OnStartup and keeps them until OnShutdown.
    /// The only place that knows the list of categories.
    /// </summary>
    public static class PluginEntry
    {
        private const string DataFolderName = "RevitNumbering";
        private const string LogFolderName = "Logs";
        private const string SettingsFileName = "settings.json";

        private static ILog _log;
        private static bool _isStarted;
        private static string _settingsPath;
        private static RevitDocuments _documents;
        private static RevitRunner _runner;
        private static RevitModel _model;
        private static MainWindow _window;

        public static void Initialize(UIControlledApplication application)
        {
            // Settings and logs of one Revit version: %LOCALAPPDATA%\RevitNumbering\<version>\
            string version = application.ControlledApplication.VersionNumber;
            string dataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DataFolderName, version);
            var log = new FileLog(Path.Combine(dataFolder, LogFolderName));
            _log = log;

            SafeRunner.Run(log, "Startup", () =>
            {
                log.DeleteOldFiles();

                // Categories: a new category = one line here. The order is the order in the window.
                var categories = new List<RevitCategory>
                {
                    new WallsCategory(),
                    new DoorsCategory(),
                    new WindowsCategory(),
                    new RoomsCategory(),
                };

                _settingsPath = Path.Combine(dataFolder, SettingsFileName);
                _documents = new RevitDocuments(application, log);
                _documents.ApplicationClosing += CloseWindowWithoutQuestion;

                // ExternalEvent is created here, in the OnStartup context.
                _runner = new RevitRunner { BeforeAction = _documents.Refresh };
                _runner.Initialize();
                _model = new RevitModel(categories, _documents, _runner, log);

                _isStarted = true;
                log.Info("[Startup] Revit " + application.ControlledApplication.VersionName + " build "
                    + application.ControlledApplication.VersionBuild + ", plugin "
                    + Assembly.GetExecutingAssembly().GetName().Version);
            }, null);
        }

        /// <summary>One window per session: the second call restores and activates the open window.</summary>
        public static void OpenWindow(UIApplication application)
        {
            if (!_isStarted)
            {
                TaskDialog.Show(Texts.WindowTitle, Texts.PluginNotStarted);
                return;
            }

            SafeRunner.Run(_log, "OpenWindow", () =>
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
                var session = new NumberingSession(_runner, _model, _documents,
                    new SettingsStore(_settingsPath, _log), _log);
                var window = new MainWindow(new MainViewModel(session, _log), application.MainWindowHandle);
                window.Closed += (sender, e) => _window = null;
                _window = window;
                window.Show();
            }, exception => TaskDialog.Show(Texts.WindowTitle, string.Format(Texts.OpenFailed, _log.FilePath)));
        }

        public static void Shutdown(UIControlledApplication application)
        {
            if (_log == null)
            {
                return;
            }

            // Every step is wrapped: a failure of one does not stop the others.
            SafeRunner.Run(_log, "Shutdown.Window", CloseWindowWithoutQuestion, null);
            SafeRunner.Run(_log, "Shutdown.Documents", () => _documents?.Unsubscribe(), null);
            SafeRunner.Run(_log, "Shutdown.Runner", () => _runner?.Dispose(), null);
            _isStarted = false;
            _log.Info("[Shutdown] plugin stopped");
        }

        /// <summary>Revit is closing: the window closes without the question about unsaved settings.</summary>
        private static void CloseWindowWithoutQuestion()
        {
            _window?.CloseWithoutQuestion();
        }
    }
}
