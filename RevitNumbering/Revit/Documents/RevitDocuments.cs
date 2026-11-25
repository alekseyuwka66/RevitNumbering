using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Model;
using RevitNumbering.Revit.Common;

namespace RevitNumbering.Revit.Documents
{
    /// <summary>
    /// The only place that subscribes to Revit events. Tracks the active document, gives every open
    /// document its DocumentId and finds the Document by it.
    /// </summary>
    internal sealed class RevitDocuments : IDocuments
    {
        private static readonly DocumentInfo NoDocument = new DocumentInfo { Kind = DocumentKind.None };

        private readonly UIControlledApplication _application;
        private readonly ILog _log;

        /// <summary>Open documents and the ids issued to them; documents are matched by Document.Equals.</summary>
        private readonly List<KnownDocument> _known = new List<KnownDocument>();

        /// <summary>Documents that are being closed: the id of the closing event → our id.</summary>
        private readonly List<KeyValuePair<int, DocumentId>> _closing = new List<KeyValuePair<int, DocumentId>>();

        private int _lastId;

        public RevitDocuments(UIControlledApplication application, ILog log)
        {
            _application = application;
            _log = log;
            Active = NoDocument;

            _application.ViewActivated += OnViewActivated;
            _application.ApplicationClosing += OnApplicationClosing;
            _application.ControlledApplication.DocumentChanged += OnDocumentChanged;
            _application.ControlledApplication.DocumentClosing += OnDocumentClosing;
            _application.ControlledApplication.DocumentClosed += OnDocumentClosed;
        }

        public DocumentInfo Active { get; private set; }

        public event Action ActiveChanged;

        public event Action<DocumentId> Closed;

        public event Action<DocumentId, ModelChanges> Changed;

        /// <summary>Revit is closing: the window must close without questions.</summary>
        public event Action ApplicationClosing;

        public void Unsubscribe()
        {
            _application.ViewActivated -= OnViewActivated;
            _application.ApplicationClosing -= OnApplicationClosing;
            _application.ControlledApplication.DocumentChanged -= OnDocumentChanged;
            _application.ControlledApplication.DocumentClosing -= OnDocumentClosing;
            _application.ControlledApplication.DocumentClosed -= OnDocumentClosed;
        }

        /// <summary>Reconciles the active document with Revit, in case a view activation was missed.</summary>
        public void Refresh(UIApplication application)
        {
            SetActive(application.ActiveUIDocument?.Document);
        }

        /// <summary>The open document of the id that is the active project; otherwise the call is refused.</summary>
        public Document RequireActive(DocumentId id)
        {
            Document document = RequireOpen(id);
            if (!id.Equals(Active.Id) || document.IsFamilyDocument)
            {
                throw new DocumentUnavailableException();
            }

            return document;
        }

        /// <summary>The open document of the id, active or not.</summary>
        public Document RequireOpen(DocumentId id)
        {
            KnownDocument known = _known.Find(k => k.Id.Equals(id));
            if (known == null || !known.Document.IsValidObject)
            {
                throw new DocumentUnavailableException();
            }

            return known.Document;
        }

        private DocumentId IdOf(Document document)
        {
            _known.RemoveAll(k => !k.Document.IsValidObject);
            KnownDocument known = _known.Find(k => k.Document.Equals(document));
            if (known == null)
            {
                known = new KnownDocument { Document = document, Id = new DocumentId(++_lastId) };
                _known.Add(known);
            }

            return known.Id;
        }

        private void SetActive(Document document)
        {
            DocumentInfo next = document == null || !document.IsValidObject
                ? NoDocument
                : new DocumentInfo
                {
                    Id = IdOf(document),
                    Kind = document.IsFamilyDocument ? DocumentKind.Family : DocumentKind.Project,
                    Title = document.Title,
                };

            bool changed = next.Kind != Active.Kind || !Equals(next.Id, Active.Id);
            Active = next;
            if (changed)
            {
                ActiveChanged?.Invoke();
            }
        }

        private void OnViewActivated(object sender, ViewActivatedEventArgs e)
        {
            SafeRunner.Run(_log, "ViewActivated", () => SetActive(e.Document), null);
        }

        private void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            if (Changed == null)
            {
                return;
            }

            SafeRunner.Run(_log, "DocumentChanged", () =>
            {
                Document document = e.GetDocument();
                ICollection<ElementId> added = e.GetAddedElementIds();
                ICollection<ElementId> modified = e.GetModifiedElementIds();
                var changes = new ModelChanges
                {
                    Added = added.Select(RevitIds.ToLong).ToList(),
                    Modified = modified.Select(RevitIds.ToLong).ToList(),
                    Deleted = e.GetDeletedElementIds().Select(RevitIds.ToLong).ToList(),
                    // Deleted levels are recognized by the listener: it knows the ids of the levels it has read.
                    LevelsChanged = added.Concat(modified).Any(id => document.GetElement(id) is Level),
                };
                Changed?.Invoke(IdOf(document), changes);
            }, null);
        }

        private void OnDocumentClosing(object sender, DocumentClosingEventArgs e)
        {
            SafeRunner.Run(_log, "DocumentClosing", () =>
            {
                KnownDocument known = _known.Find(k => k.Document.IsValidObject && k.Document.Equals(e.Document));
                if (known != null)
                {
                    _closing.Add(new KeyValuePair<int, DocumentId>(e.DocumentId, known.Id));
                }
            }, null);
        }

        private void OnDocumentClosed(object sender, DocumentClosedEventArgs e)
        {
            SafeRunner.Run(_log, "DocumentClosed", () =>
            {
                int index = _closing.FindIndex(pair => pair.Key == e.DocumentId);
                if (index < 0)
                {
                    return;
                }

                DocumentId id = _closing[index].Value;
                _closing.RemoveAt(index);
                if (e.Status != RevitAPIEventStatus.Succeeded)
                {
                    // The closing was cancelled: the document stays open.
                    return;
                }

                _known.RemoveAll(k => k.Id.Equals(id));
                if (id.Equals(Active.Id))
                {
                    Active = NoDocument;
                }

                Closed?.Invoke(id);
            }, null);
        }

        private void OnApplicationClosing(object sender, ApplicationClosingEventArgs e)
        {
            SafeRunner.Run(_log, "ApplicationClosing", () => ApplicationClosing?.Invoke(), null);
        }

        private sealed class KnownDocument
        {
            public Document Document { get; set; }

            public DocumentId Id { get; set; }
        }
    }
}
