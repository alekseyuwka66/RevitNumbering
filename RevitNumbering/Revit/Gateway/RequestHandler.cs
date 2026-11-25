using Autodesk.Revit.UI;
using RevitNumbering.Core.Gateway;
using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Presentation;
using RevitNumbering.Revit.Documents;

namespace RevitNumbering.Revit.Gateway
{
    /// <summary>
    /// The UIApplication of the running ExternalEvent call; null outside of it.
    /// Request work (scopes, writing) takes the application from here.
    /// </summary>
    public sealed class ApiContext
    {
        public UIApplication Application { get; internal set; }

        public UIApplication RequireApplication()
        {
            return Application ?? throw new InvalidOperationException("Revit API is available only inside a request.");
        }
    }

    /// <summary>
    /// ExternalEvent handler: reconciles the active document and runs one queued request (07 §3.2 p. 4, ADR-04).
    /// </summary>
    internal sealed class RequestHandler : IExternalEventHandler
    {
        private readonly RequestQueue _queue;
        private readonly RevitActiveDocumentService _documents;
        private readonly ApiContext _context;
        private readonly ILog _log;
        private readonly Action<string> _showError;

        public RequestHandler(RequestQueue queue, RevitActiveDocumentService documents, ApiContext context, ILog log,
            Action<string> showError)
        {
            _queue = queue;
            _documents = documents;
            _context = context;
            _log = log;
            _showError = showError;
        }

        public void Execute(UIApplication app)
        {
            SafeRunner.Run(_log, "ExternalEvent", () =>
            {
                _context.Application = app;
                try
                {
                    _documents.SyncActive(app);
                    _queue.ExecutePending();
                }
                finally
                {
                    _context.Application = null;
                }
            }, _showError);
        }

        public string GetName()
        {
            return "RevitNumbering";
        }
    }

    /// <summary>
    /// Request states for the window and the host lifecycle (IRequestService); events are posted to the window thread.
    /// </summary>
    public sealed class RevitRequestService : IRequestService
    {
        private readonly UIControlledApplication _application;
        private readonly RequestQueue _queue;
        private readonly IUiDispatcher _dispatcher;
        private readonly ILog _log;
        private bool _subscribed;

        public RevitRequestService(UIControlledApplication application, RequestQueue queue, IUiDispatcher dispatcher,
            ILog log)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _log = log ?? throw new ArgumentNullException(nameof(log));

            _queue.StateChanged += OnStateChanged;
            _application.ApplicationClosing += OnApplicationClosing;
            _subscribed = true;
        }

        public event EventHandler<RequestStateEventArgs> RequestStateChanged;

        public event EventHandler ApplicationClosing;

        public bool CancelPending()
        {
            return _queue.CancelPending();
        }

        internal void Shutdown()
        {
            if (!_subscribed)
            {
                return;
            }

            _subscribed = false;
            _queue.StateChanged -= OnStateChanged;
            _application.ApplicationClosing -= OnApplicationClosing;
        }

        private void OnStateChanged(object sender, RequestStateEventArgs e)
        {
            _dispatcher.Post(() => RequestStateChanged?.Invoke(this, e), UiPriority.Normal);
        }

        private void OnApplicationClosing(object sender, Autodesk.Revit.UI.Events.ApplicationClosingEventArgs e)
        {
            SafeRunner.Run(_log, "ApplicationClosing", () =>
            {
                _queue.CancelAll();
                _dispatcher.Post(() => ApplicationClosing?.Invoke(this, EventArgs.Empty), UiPriority.Normal);
            }, null);
        }
    }
}
