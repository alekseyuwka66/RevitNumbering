using Autodesk.Revit.DB;
using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Model;
using RevitNumbering.Revit.Categories;
using RevitNumbering.Revit.Documents;
using RevitNumbering.Revit.Reading;
using RevitNumbering.Revit.Selection;
using RevitNumbering.Revit.Writing;
using RevitNumbering.UI.Resources;

namespace RevitNumbering.Revit
{
    // The model for the core: a thin class over the reader, the picker and the writer.
    // The Document comes from RevitDocuments, so it works both in a runner call and in a Revit event handler.
    internal sealed class RevitModel : IRevitModel
    {
        private readonly List<RevitCategory> _categories;
        private readonly RevitDocuments _documents;

        public RevitModel(List<RevitCategory> categories, RevitDocuments documents)
        {
            _categories = categories;
            _documents = documents;
            CategoryKeys = categories.Select(category => category.Key).ToList();
        }

        public IReadOnlyList<string> CategoryKeys { get; }

        public CategoryData Read(DocumentId document, string categoryKey)
        {
            return ModelReader.Read(_documents.RequireActive(document), Find(categoryKey));
        }

        public List<long> PickElements(DocumentId document, string categoryKey)
        {
            string prompt = string.Format(Texts.PickPrompt, TextLookup.Category(categoryKey));
            return ElementPicker.Pick(_documents.RequireActiveUi(document), Find(categoryKey), prompt);
        }

        public void ShowElement(DocumentId document, long id)
        {
            ElementPicker.Show(_documents.RequireActiveUi(document), id);
        }

        public WriteResult Write(DocumentId document, string categoryKey, List<WriteItem> items,
            IWriteProgress progress)
        {
            return ModelWriter.Write(_documents.RequireActive(document), Find(categoryKey), items, progress);
        }

        private RevitCategory Find(string categoryKey)
        {
            return _categories.Find(category => category.Key == categoryKey)
                ?? throw new ArgumentException("Unknown category: " + categoryKey, nameof(categoryKey));
        }
    }
}
