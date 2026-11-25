using System.Windows.Threading;
using Autodesk.Revit.UI;
using RevitNumbering.Core.Domain.Ordering;
using RevitNumbering.Core.Gateway;
using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Reading;
using RevitNumbering.Core.Settings;
using RevitNumbering.Core.Texts;
using RevitNumbering.Core.Domain.Categories;
using RevitNumbering.Revit.Categories;
using RevitNumbering.Revit.Documents;
using RevitNumbering.Revit.Gateway;
using RevitNumbering.Revit.Reading;
using RevitNumbering.Revit.Scopes;
using RevitNumbering.Revit.Writing;
using RevitNumbering.UI.Services;

namespace RevitNumbering
{
    /// <summary>
    /// Composition root: the only place that knows every category, strategy and scope and links them.
    /// The only long-lived state of the plugin (01 §4 p. 34): created in OnStartup, lives until OnShutdown.
    /// </summary>
    public static class PluginEntry
    {
        private const string SettingsFolder = "RevitNumbering";
        private const string SettingsFileName = "settings.json";

        private static ExternalEvent _externalEvent;

        /// <summary>null until Initialize succeeded.</summary>
        internal static PluginServices Services { get; private set; }

        public static void Initialize(UIControlledApplication application)
        {
            if (application == null)
            {
                throw new ArgumentNullException(nameof(application));
            }

            string version = application.ControlledApplication.VersionNumber;
            FileLog log = FileLog.Create(version);
            try
            {
                Services = Compose(application, version, log);
                log.Info("Плагин запущен, Revit " + version);
            }
            catch (Exception ex)
            {
                log.Error("Сбой инициализации плагина", ex);
                Services = null;
                throw;
            }
        }

        public static void Shutdown(UIControlledApplication application)
        {
            PluginServices services = Services;
            if (services == null)
            {
                return;
            }

            ILog log = services.Log;
            SafeRunner.Run(log, "Shutdown.Queue", () => services.Queue.CancelAll(), null);
            SafeRunner.Run(log, "Shutdown.Documents", () => services.Documents.Shutdown(), null);
            SafeRunner.Run(log, "Shutdown.Requests", () => services.RequestService.Shutdown(), null);
            SafeRunner.Run(log, "Shutdown.ExternalEvent", () => _externalEvent?.Dispose(), null);
            log.Info("Плагин завершён");
            _externalEvent = null;
            Services = null;
        }

        private static PluginServices Compose(UIControlledApplication application, string version, ILog log)
        {
            Action<string> showError = message => TaskDialog.Show(TextCatalog.ErrorDialogTitle, message);
            var dispatcher = new WpfUiDispatcher(Dispatcher.CurrentDispatcher, log);

            // Categories: a new category = one line here (F2-05). The order is the order of the window list.
            var categories = new CategoryRegistry<IRevitCategory>(new IRevitCategory[]
            {
                new WallsCategory(),
                new DoorsCategory(),
                new WindowsCategory(),
                new RoomsCategory(),
            }, defaultKey: "Doors"); // Q-09: the default category

            // Traversal strategies: a new strategy = one line here (F6-01).
            var grid = new GridTraversal();
            var strategies = new TraversalRegistry(new ITraversalStrategy[] { grid });

            // The only link between categories and strategy parameters (F6-05: PRM-ROW-TOL-*).
            var strategyDefaults = new CategoryStrategyDefaults()
                .Set("Walls", grid.Key, grid.RowTolerance.Key, "1000")
                .Set("Doors", grid.Key, grid.RowTolerance.Key, "500")
                .Set("Windows", grid.Key, grid.RowTolerance.Key, "500")
                .Set("Rooms", grid.Key, grid.RowTolerance.Key, "3000");
            strategyDefaults.Validate(categories, strategies);

            // Scopes of numbering: a new scope = one line here.
            var scopes = new IElementScope[]
            {
                new WholeDocumentScope(),
                new SelectionScope(),
                new ActiveViewScope(),
            };

            string settingsPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk", "Revit", "Addins", version, SettingsFolder, SettingsFileName);
            var settings = new SettingsStore(settingsPath, log);

            var context = new ApiContext();
            var documents = new RevitActiveDocumentService(application, dispatcher, log, showError);

            // ExternalEvent.Create only here, in the OnStartup context (01 §4 p. 25).
            var queue = new RequestQueue(() =>
            {
                ExternalEventRequest result = _externalEvent.Raise();
                if (result != ExternalEventRequest.Accepted)
                {
                    log.Info("ExternalEvent.Raise: " + result);
                }

                return result == ExternalEventRequest.Accepted;
            });
            _externalEvent = ExternalEvent.Create(new RequestHandler(queue, documents, context, log, showError));
            documents.Closing += key => queue.CancelForDocument(key);

            var requestService = new RevitRequestService(application, queue, dispatcher, log);
            var collector = new CategoryCollector();
            var reader = new CategoryLoader(queue, new ModelReader(documents, categories, collector, log),
                GatewayParameters.LoadMaxRestarts);
            var scopeService = new RevitScopeService(scopes, queue, documents, context);
            var writer = new RevitModelWriter(queue, new ModelWriter(categories, log), documents, context);

            return new PluginServices(log, dispatcher, categories, strategies, strategyDefaults, settings, documents,
                queue, requestService, reader, scopeService, writer);
        }
    }

    /// <summary>Services created by the composition root; the window gets them from here.</summary>
    internal sealed class PluginServices
    {
        public PluginServices(ILog log, WpfUiDispatcher dispatcher, CategoryRegistry<IRevitCategory> categories,
            TraversalRegistry strategies, CategoryStrategyDefaults strategyDefaults, SettingsStore settings,
            RevitActiveDocumentService documents, RequestQueue queue, RevitRequestService requestService,
            IElementReader reader, IScopeService scopes, RevitModelWriter writer)
        {
            Log = log;
            Dispatcher = dispatcher;
            Categories = categories;
            Strategies = strategies;
            StrategyDefaults = strategyDefaults;
            Settings = settings;
            Documents = documents;
            Queue = queue;
            RequestService = requestService;
            Reader = reader;
            Scopes = scopes;
            Writer = writer;
        }

        public ILog Log { get; }

        public WpfUiDispatcher Dispatcher { get; }

        public CategoryRegistry<IRevitCategory> Categories { get; }

        public TraversalRegistry Strategies { get; }

        public CategoryStrategyDefaults StrategyDefaults { get; }

        public SettingsStore Settings { get; }

        public RevitActiveDocumentService Documents { get; }

        public RequestQueue Queue { get; }

        public RevitRequestService RequestService { get; }

        public IElementReader Reader { get; }

        public IScopeService Scopes { get; }

        public RevitModelWriter Writer { get; }
    }
}
