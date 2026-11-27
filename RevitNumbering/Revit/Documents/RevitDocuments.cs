using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Model;

namespace RevitNumbering.Revit.Documents
{
    // The only place that subscribes to Revit events. It tracks the active document, gives every
    // document that becomes active its DocumentId, and finds the Document by that id.
    internal sealed class RevitDocuments : IDocuments
    {
        private static readonly DocumentInfo NoDocument = new DocumentInfo { Kind = DocumentKind.None };

        private readonly UIControlledApplication _application;
        private readonly FileLog _log;
        private readonly string _numberingTransactionName;

        // Documents that have been active, with their ids.
        private readonly List<KnownDocument> _known = new List<KnownDocument>();

        private int _lastId;

        // From the last runner call or window opening. Needed to pick and show elements.
        private UIApplication _uiApplication;

        public RevitDocuments(UIControlledApplication application, FileLog log, string numberingTransactionName)
        {
            _application = application;
            _log = log;
            _numberingTransactionName = numberingTransactionName;
            Active = NoDocument;

            _application.ViewActivated += OnViewActivated;
            _application.ApplicationClosing += OnApplicationClosing;
            _application.ControlledApplication.DocumentClosed += OnDocumentClosed;
            _application.ControlledApplication.DocumentChanged += OnDocumentChanged;
        }

        public DocumentInfo Active { get; private set; }

        public event Action ActiveChanged;

        public event Action<HistoryChange> NumberingHistoryChanged;

        // Revit is closing: the window must close.
        public event Action ApplicationClosing;

        public void Unsubscribe()
        {
            _application.ViewActivated -= OnViewActivated;
            _application.ApplicationClosing -= OnApplicationClosing;
            _application.ControlledApplication.DocumentClosed -= OnDocumentClosed;
            _application.ControlledApplication.DocumentChanged -= OnDocumentChanged;
        }

        // Syncs the active document with Revit, in case a view activation was missed. API context only. Never throws.
        public void Refresh(UIApplication application)
        {
            _uiApplication = application;
            SafeRunner.Run(_log, "Refresh", () => SetActive(application.ActiveUIDocument?.Document));
        }

        // Returns the open document if it is the active project. Otherwise refuses with DocumentUnavailableException.
        public Document RequireActive(DocumentId id)
        {
            KnownDocument known = _known.Find(k => k.Id.Equals(id));
            if (known == null || !known.Document.IsValidObject || !id.Equals(Active.Id)
                || known.Document.IsFamilyDocument)
            {
                throw new DocumentUnavailableException();
            }

            return known.Document;
        }

        // The UI document of the active project. Needed to pick and show elements.
        public UIDocument RequireActiveUi(DocumentId id)
        {
            Document active = RequireActive(id);
            UIDocument uiDocument = _uiApplication?.ActiveUIDocument;
            if (uiDocument == null || !uiDocument.Document.Equals(active))
            {
                throw new DocumentUnavailableException();
            }

            return uiDocument;
        }

        // A document gets its id when it becomes active for the first time.
        private DocumentId IdOf(Document document)
        {
            KnownDocument known = _known.Find(k => k.Document.IsValidObject && k.Document.Equals(document));
            if (known == null)
            {
                known = new KnownDocument { Document = document, Id = new DocumentId(++_lastId) };
                _known.Add(known);
            }

            return known.Id;
        }

        private void SetActive(Document document)
        {
            DocumentInfo next = document == null
                ? NoDocument
                : new DocumentInfo
                {
                    Id = IdOf(document),
                    Kind = document.IsFamilyDocument ? DocumentKind.Family : DocumentKind.Project,
                };

            bool changed = next.Kind != Active.Kind || !Equals(next.Id, Active.Id);
            Active = next;
            if (changed)
            {
                ActiveChanged?.Invoke();
            }
        }

        // Revit event handlers: a named method does the work, a failure is only logged.
        private void OnViewActivated(object sender, ViewActivatedEventArgs e)
        {
            SafeRunner.Run(_log, "ViewActivated", () => SetActive(e.Document));
        }

        private void OnDocumentClosed(object sender, DocumentClosedEventArgs e)
        {
            SafeRunner.Run(_log, "DocumentClosed", HandleDocumentClosed);
        }

        private void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            SafeRunner.Run(_log, "DocumentChanged", () => HandleDocumentChanged(e));
        }

        private void OnApplicationClosing(object sender, ApplicationClosingEventArgs e)
        {
            SafeRunner.Run(_log, "ApplicationClosing", () => ApplicationClosing?.Invoke());
        }

        // DocumentChanged fires on every transaction. Only undo and redo of the numbering step of the active document matter.
        private void HandleDocumentChanged(DocumentChangedEventArgs e)
        {
            UndoOperation operation = e.Operation;
            if (operation != UndoOperation.TransactionUndone && operation != UndoOperation.TransactionRedone)
            {
                return;
            }

            if (!e.GetTransactionNames().Contains(_numberingTransactionName))
            {
                return;
            }

            KnownDocument active = _known.Find(k => k.Id.Equals(Active.Id));
            if (active == null || !active.Document.Equals(e.GetDocument()))
            {
                return;
            }

            NumberingHistoryChanged?.Invoke(
                operation == UndoOperation.TransactionUndone ? HistoryChange.Undone : HistoryChange.Redone);
        }

        // A closed document is no longer valid, so it is forgotten. If it was the active one,
        // there is no active document until Revit activates a view of another one.
        private void HandleDocumentClosed()
        {
            _known.RemoveAll(k => !k.Document.IsValidObject);
            if (Active.Id != null && !_known.Exists(k => k.Id.Equals(Active.Id)))
            {
                SetActive(null);
            }
        }

        private sealed class KnownDocument
        {
            public Document Document { get; set; }

            public DocumentId Id { get; set; }
        }
    }
}
