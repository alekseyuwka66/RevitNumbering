using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitNumbering.Core.Documents;
using RevitNumbering.Core.Gateway;
using RevitNumbering.Core.Reading;
using RevitNumbering.Revit.Common;
using RevitNumbering.Revit.Documents;
using RevitNumbering.Revit.Gateway;

namespace RevitNumbering.Revit.Scopes
{
    /// <summary>
    /// A scope of numbering. Independent of categories: it returns ids of any elements, the core intersects them
    /// with the loaded category. A new scope = one class + one line in the list of PluginEntry.
    /// </summary>
    public interface IElementScope : IScopeInfo
    {
        /// <summary>API context. null: no limit (the whole document).</summary>
        IReadOnlyCollection<long> TakeIds(UIDocument uiDocument, out bool hadGroupsOrAssemblies);
    }

    public sealed class WholeDocumentScope : IElementScope
    {
        public const string ScopeKey = ScopeKeys.Document;

        public string Key => ScopeKey;

        public string DisplayName => "Весь документ";

        public IReadOnlyCollection<long> TakeIds(UIDocument uiDocument, out bool hadGroupsOrAssemblies)
        {
            hadGroupsOrAssemblies = false;
            return null;
        }
    }

    /// <summary>F5-02: the selection snapshot by the button; all selected ids, filtering by category is in the core.</summary>
    public sealed class SelectionScope : IElementScope
    {
        public const string ScopeKey = ScopeKeys.Selection;

        public string Key => ScopeKey;

        public string DisplayName => "Выбранные в модели";

        public IReadOnlyCollection<long> TakeIds(UIDocument uiDocument, out bool hadGroupsOrAssemblies)
        {
            Document document = uiDocument.Document;
            var ids = new List<long>();
            hadGroupsOrAssemblies = false;
            foreach (ElementId id in uiDocument.Selection.GetElementIds())
            {
                Element element = document.GetElement(id);
                if (element is Group || element is AssemblyInstance)
                {
                    // AA-80: their ids stay, members are not expanded.
                    hadGroupsOrAssemblies = true;
                }

                ids.Add(RevitIds.ToLong(id));
            }

            ids.Sort();
            return ids;
        }
    }

    /// <summary>Elements visible on the active view (decision of the user; shown in the window — open question).</summary>
    public sealed class ActiveViewScope : IElementScope
    {
        public const string ScopeKey = ScopeKeys.ActiveView;

        public string Key => ScopeKey;

        public string DisplayName => "На активном виде";

        public IReadOnlyCollection<long> TakeIds(UIDocument uiDocument, out bool hadGroupsOrAssemblies)
        {
            hadGroupsOrAssemblies = false;
            View view = uiDocument.ActiveView;
            if (view == null)
            {
                return Array.Empty<long>();
            }

            return new FilteredElementCollector(uiDocument.Document, view.Id)
                .WhereElementIsNotElementType()
                .ToElementIds()
                .Select(RevitIds.ToLong)
                .OrderBy(id => id)
                .ToArray();
        }
    }

    /// <summary>Takes the ids of a scope through the request queue; knows no categories.</summary>
    public sealed class RevitScopeService : IScopeService
    {
        private readonly RequestQueue _queue;
        private readonly RevitActiveDocumentService _documents;
        private readonly ApiContext _context;
        private readonly Dictionary<string, IElementScope> _byKey;

        public RevitScopeService(IEnumerable<IElementScope> scopes, RequestQueue queue,
            RevitActiveDocumentService documents, ApiContext context)
        {
            IElementScope[] all = (scopes ?? throw new ArgumentNullException(nameof(scopes))).ToArray();
            _byKey = all.ToDictionary(scope => scope.Key, StringComparer.Ordinal);
            Scopes = all;
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public IReadOnlyList<IScopeInfo> Scopes { get; }

        public Task<ScopeSnapshot> TakeAsync(DocumentKey document, string scopeKey, CancellationToken cancellationToken)
        {
            if (scopeKey == null || !_byKey.TryGetValue(scopeKey, out IElementScope scope))
            {
                var failed = new TaskCompletionSource<ScopeSnapshot>();
                failed.SetException(new ArgumentException("Unknown scope: " + scopeKey, nameof(scopeKey)));
                return failed.Task;
            }

            return _queue.Enqueue(RequestNames.Selection, document, () => Take(document, scope), cancellationToken);
        }

        private ScopeSnapshot Take(DocumentKey document, IElementScope scope)
        {
            UIDocument uiDocument = _context.RequireApplication().ActiveUIDocument;
            if (uiDocument == null || _documents.GetActiveDocument(document) == null
                || !uiDocument.Document.Equals(_documents.GetActiveDocument(document)))
            {
                throw new OperationCanceledException("The document of the scope is not active.");
            }

            IReadOnlyCollection<long> ids = scope.TakeIds(uiDocument, out bool hadGroupsOrAssemblies);
            return new ScopeSnapshot(document, scope.Key, ids, hadGroupsOrAssemblies);
        }
    }
}
