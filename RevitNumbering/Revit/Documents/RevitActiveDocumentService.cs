using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using RevitNumbering.Core.Documents;
using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Presentation;
using RevitNumbering.Revit.Common;

namespace RevitNumbering.Revit.Documents
{
    /// <summary>
    /// Tracks and checks the active document: keys, the active-document cache, generations and significant
    /// changes (07 §3.2, §3.9). Knows nothing about numbering; categories only as "what the last load read"
    /// (the snapshot scope), to decide whether a model change matters.
    /// All Revit event subscriptions of the plugin are here and in RevitRequestService.
    /// </summary>
    public sealed class RevitActiveDocumentService : IActiveDocumentService
    {
        private const string KeyPrefix = "doc-"; // AA-74

        private readonly UIControlledApplication _application;
        private readonly IUiDispatcher _dispatcher;
        private readonly ILog _log;
        private readonly Action<string> _showError;

        private readonly List<KeyValuePair<Document, DocumentKey>> _keys = new List<KeyValuePair<Document, DocumentKey>>();
        private readonly Dictionary<DocumentKey, long> _generations = new Dictionary<DocumentKey, long>();
        private readonly Dictionary<DocumentKey, SnapshotScope> _scopes = new Dictionary<DocumentKey, SnapshotScope>();

        private int _keyCounter;
        private int _ownWriteDepth;
        private DocumentKey _closingKey;
        private bool _subscribed;

        public RevitActiveDocumentService(UIControlledApplication application, IUiDispatcher dispatcher, ILog log,
            Action<string> showError)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _showError = showError;
            Current = ActiveDocumentInfo.None;

            _application.ViewActivated += OnViewActivated;
            _application.ControlledApplication.DocumentChanged += OnDocumentChanged;
            _application.ControlledApplication.DocumentClosing += OnDocumentClosing;
            _application.ControlledApplication.DocumentClosed += OnDocumentClosed;
            _subscribed = true;
        }

        public ActiveDocumentInfo Current { get; private set; }

        public event EventHandler<DocumentEventArgs> ActiveChanged;

        public event EventHandler<DocumentEventArgs> Closed;

        public event EventHandler<DocumentEventArgs> ModelChanged;

        /// <summary>Raised synchronously in the API context when a document starts closing (queue cancellation).</summary>
        internal event Action<DocumentKey> Closing;

        public bool IsActive(DocumentKey document)
        {
            return document != null && Current.IsAvailable && document.Equals(Current.Key);
        }

        public long GetGeneration(DocumentKey document)
        {
            return document != null && _generations.TryGetValue(document, out long generation) ? generation : 0;
        }

        /// <summary>API context: the document of the key if it is still the active project; otherwise null.</summary>
        internal Document GetActiveDocument(DocumentKey document)
        {
            return IsActive(document) ? FindDocument(document) : null;
        }

        /// <summary>API context: key of a document; issued on first use.</summary>
        internal DocumentKey KeyOf(Document document)
        {
            if (document == null)
            {
                return null;
            }

            foreach (KeyValuePair<Document, DocumentKey> pair in _keys)
            {
                if (pair.Key.IsValidObject && pair.Key.Equals(document))
                {
                    return pair.Value;
                }
            }

            var key = new DocumentKey(KeyPrefix + (++_keyCounter).ToString(System.Globalization.CultureInfo.InvariantCulture));
            _keys.Add(new KeyValuePair<Document, DocumentKey>(document, key));
            _generations[key] = 0;
            return key;
        }

        /// <summary>
        /// API context (ExternalEvent): reconciles the cache with the really active document (AA-79),
        /// in case ViewActivated was missed.
        /// </summary>
        internal void SyncActive(UIApplication application)
        {
            UpdateActive(application?.ActiveUIDocument?.Document);
        }

        /// <summary>What the last load read: changes of these ids, of the category, levels and phases matter.</summary>
        internal void SetSnapshotScope(DocumentKey document, BuiltInCategory category, IEnumerable<long> knownIds)
        {
            if (document != null)
            {
                _scopes[document] = new SnapshotScope(category, knownIds);
            }
        }

        /// <summary>Own write: DocumentChanged inside is skipped (01 §4 p. 6, AA-71).</summary>
        internal IDisposable BeginOwnWrite()
        {
            _ownWriteDepth++;
            return new OwnWrite(this);
        }

        /// <summary>After a committed own write: +1 without ModelChanged (AA-70).</summary>
        internal void MarkWritten(DocumentKey document)
        {
            if (document != null && _generations.ContainsKey(document))
            {
                _generations[document]++;
            }
        }

        internal void Shutdown()
        {
            if (!_subscribed)
            {
                return;
            }

            _subscribed = false;
            _application.ViewActivated -= OnViewActivated;
            _application.ControlledApplication.DocumentChanged -= OnDocumentChanged;
            _application.ControlledApplication.DocumentClosing -= OnDocumentClosing;
            _application.ControlledApplication.DocumentClosed -= OnDocumentClosed;
        }

        private Document FindDocument(DocumentKey key)
        {
            foreach (KeyValuePair<Document, DocumentKey> pair in _keys)
            {
                if (pair.Value.Equals(key) && pair.Key.IsValidObject)
                {
                    return pair.Key;
                }
            }

            return null;
        }

        private void OnViewActivated(object sender, ViewActivatedEventArgs e)
        {
            SafeRunner.Run(_log, "ViewActivated", () => UpdateActive(e.Document), _showError);
        }

        private void UpdateActive(Document document)
        {
            ActiveDocumentInfo next = Describe(document);
            bool keyChanged = !Equals(next.Key, Current.Key) || next.Availability != Current.Availability;
            Current = next;
            if (keyChanged)
            {
                Post(ActiveChanged, next.Key);
            }
        }

        private ActiveDocumentInfo Describe(Document document)
        {
            if (document == null || !document.IsValidObject)
            {
                return ActiveDocumentInfo.None;
            }

            if (document.IsFamilyDocument)
            {
                return new ActiveDocumentInfo(DocumentAvailability.FamilyDocument, null, document.Title);
            }

            return new ActiveDocumentInfo(DocumentAvailability.Ok, KeyOf(document), document.Title);
        }

        private void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            SafeRunner.Run(_log, "DocumentChanged", () =>
            {
                DocumentKey key = FindKey(e.GetDocument());
                if (key == null || _ownWriteDepth > 0 || !IsSignificant(key, e))
                {
                    return;
                }

                _generations[key] = GetGeneration(key) + 1;
                Post(ModelChanged, key);
            }, _showError);
        }

        /// <summary>07 §3.9: the first matching row.</summary>
        private bool IsSignificant(DocumentKey key, DocumentChangedEventArgs e)
        {
            UndoOperation operation = e.Operation;
            if (operation == UndoOperation.TransactionUndone || operation == UndoOperation.TransactionRedone)
            {
                return true;
            }

            Document document = e.GetDocument();
            if (!_scopes.TryGetValue(key, out SnapshotScope scope))
            {
                return e.GetAddedElementIds().Concat(e.GetModifiedElementIds())
                    .Any(id => IsLevelOrPhase(document.GetElement(id)));
            }

            if (e.GetDeletedElementIds().Any(id => scope.Known.Contains(RevitIds.ToLong(id))))
            {
                return true;
            }

            foreach (ElementId id in e.GetAddedElementIds().Concat(e.GetModifiedElementIds()))
            {
                if (scope.Known.Contains(RevitIds.ToLong(id)))
                {
                    return true;
                }

                Element element = document.GetElement(id);
                if (IsLevelOrPhase(element)
                    || (element?.Category != null && RevitIds.ToLong(element.Category.Id) == (long)scope.Category))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsLevelOrPhase(Element element)
        {
            return element is Level || element is Phase;
        }

        private DocumentKey FindKey(Document document)
        {
            if (document == null)
            {
                return null;
            }

            foreach (KeyValuePair<Document, DocumentKey> pair in _keys)
            {
                if (pair.Key.IsValidObject && pair.Key.Equals(document))
                {
                    return pair.Value;
                }
            }

            return null;
        }

        private void OnDocumentClosing(object sender, DocumentClosingEventArgs e)
        {
            SafeRunner.Run(_log, "DocumentClosing", () =>
            {
                _closingKey = FindKey(e.Document);
                if (_closingKey != null)
                {
                    Closing?.Invoke(_closingKey);
                }
            }, null);
        }

        private void OnDocumentClosed(object sender, DocumentClosedEventArgs e)
        {
            SafeRunner.Run(_log, "DocumentClosed", () =>
            {
                DocumentKey key = _closingKey;
                _closingKey = null;
                if (key == null || e.Status != RevitAPIEventStatus.Succeeded)
                {
                    return;
                }

                _keys.RemoveAll(pair => pair.Value.Equals(key));
                _scopes.Remove(key);
                _generations.Remove(key);
                if (key.Equals(Current.Key))
                {
                    Current = ActiveDocumentInfo.None;
                    Post(ActiveChanged, null);
                }

                Post(Closed, key);
            }, null);
        }

        private void Post(EventHandler<DocumentEventArgs> handler, DocumentKey key)
        {
            _dispatcher.Post(() => handler?.Invoke(this, new DocumentEventArgs(key)), UiPriority.Normal);
        }

        private sealed class SnapshotScope
        {
            public SnapshotScope(BuiltInCategory category, IEnumerable<long> known)
            {
                Category = category;
                Known = new HashSet<long>(known ?? Enumerable.Empty<long>());
            }

            public BuiltInCategory Category { get; }

            public HashSet<long> Known { get; }
        }

        private sealed class OwnWrite : IDisposable
        {
            private RevitActiveDocumentService _owner;

            public OwnWrite(RevitActiveDocumentService owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                if (_owner != null)
                {
                    _owner._ownWriteDepth--;
                    _owner = null;
                }
            }
        }
    }
}
