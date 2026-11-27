using Autodesk.Revit.UI;
using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Logging;
using RevitNumbering.Revit.Documents;

namespace RevitNumbering.Revit.Runner
{
    // Runs window actions in the Revit API context through one ExternalEvent.
    // Revit runs the action when it is free. One posted action at a time.
    internal sealed class RevitRunner : IRevitRunner, IExternalEventHandler
    {
        private readonly RevitDocuments _documents;
        private readonly FileLog _log;
        private readonly ExternalEvent _event;

        // The posted action. Null: nothing is posted.
        private Action _pending;

        // Create it in OnStartup: ExternalEvent can be created only in the API context.
        public RevitRunner(RevitDocuments documents, FileLog log)
        {
            _documents = documents;
            _log = log;
            _event = ExternalEvent.Create(this);
        }

        public bool Run(Action action)
        {
            _pending = action;

            // Pending: the event of a dropped action has not run yet, so it will run this action.
            ExternalEventRequest request = _event.Raise();
            if (request == ExternalEventRequest.Accepted || request == ExternalEventRequest.Pending)
            {
                return true;
            }

            // The session logs the refusal together with the operation name.
            _pending = null;
            return false;
        }

        public bool CancelPending()
        {
            bool posted = _pending != null;
            _pending = null;
            return posted;
        }

        // Called by Revit when it is free.
        public void Execute(UIApplication app)
        {
            Action action = _pending;
            _pending = null;
            if (action == null)
            {
                // The action was dropped before Revit got to it.
                return;
            }

            // A view activation may have been missed while the action waited.
            _documents.Refresh(app);
            SafeRunner.Run(_log, "RevitCall", action);
        }

        public string GetName()
        {
            return "RevitNumbering";
        }

        public void Dispose()
        {
            _pending = null;
            _event.Dispose();
        }
    }
}
