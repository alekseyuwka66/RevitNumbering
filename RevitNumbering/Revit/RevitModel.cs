using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Model;
using RevitNumbering.Revit.Categories;
using RevitNumbering.Revit.Documents;
using RevitNumbering.Revit.Reading;
using RevitNumbering.Revit.Runner;
using RevitNumbering.Revit.Selection;
using RevitNumbering.Revit.Writing;
using RevitNumbering.UI.Resources;

namespace RevitNumbering.Revit
{
    /// <summary>
    /// The model for the core: a thin class over the reader, the picker and the writer.
    /// The Document comes from RevitDocuments, so reading works both inside a runner call
    /// and inside a handler of a Revit event.
    /// </summary>
    internal sealed class RevitModel : IRevitModel
    {
        private readonly List<RevitCategory> _categories;
        private readonly RevitDocuments _documents;
        private readonly RevitRunner _runner;
        private readonly ModelReader _reader = new ModelReader();
        private readonly ElementPicker _picker = new ElementPicker();
        private readonly ModelWriter _writer;

        public RevitModel(List<RevitCategory> categories, RevitDocuments documents, RevitRunner runner, ILog log)
        {
            _categories = categories;
            _documents = documents;
            _runner = runner;
            _writer = new ModelWriter(log);
            CategoryKeys = categories.Select(category => category.Key).ToList();
        }

        public IReadOnlyList<string> CategoryKeys { get; }

        public CategoryData Read(DocumentId document, string categoryKey)
        {
            return _reader.Read(_documents.RequireActive(document), Find(categoryKey));
        }

        public List<NumberingItem> ReadItems(DocumentId document, string categoryKey, List<long> ids)
        {
            return _reader.ReadItems(_documents.RequireOpen(document), Find(categoryKey), ids);
        }

        public List<LevelInfo> ReadLevels(DocumentId document)
        {
            return _reader.ReadLevels(_documents.RequireOpen(document));
        }

        public List<long> PickElements(DocumentId document, string categoryKey)
        {
            UIDocument uiDocument = RequireActiveUi(document);
            string prompt = string.Format(Texts.PickPrompt, TextLookup.Category(categoryKey));
            return _picker.Pick(uiDocument, Find(categoryKey), prompt);
        }

        public void ShowElement(DocumentId document, long id)
        {
            _picker.Show(RequireActiveUi(document), id);
        }

        public WriteResult Write(DocumentId document, string categoryKey, List<WriteItem> items)
        {
            return _writer.Write(_documents.RequireActive(document), Find(categoryKey), items);
        }

        private RevitCategory Find(string categoryKey)
        {
            return _categories.Find(category => category.Key == categoryKey)
                ?? throw new ArgumentException("Unknown category: " + categoryKey, nameof(categoryKey));
        }

        /// <summary>The UI document of the running call, only when it is the requested active document.</summary>
        private UIDocument RequireActiveUi(DocumentId document)
        {
            Document active = _documents.RequireActive(document);
            UIDocument uiDocument = _runner.Application?.ActiveUIDocument;
            if (uiDocument == null || !uiDocument.Document.Equals(active))
            {
                throw new DocumentUnavailableException();
            }

            return uiDocument;
        }
    }
}
