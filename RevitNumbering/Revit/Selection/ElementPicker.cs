using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RevitNumbering.Revit.Categories;
using RevitNumbering.Revit.Common;

namespace RevitNumbering.Revit.Selection
{
    // One filter for every category: it allows elements the category accepts,
    // and also groups and assemblies (their members are read afterwards).
    internal sealed class CategorySelectionFilter : ISelectionFilter
    {
        private readonly RevitCategory _category;

        public CategorySelectionFilter(RevitCategory category)
        {
            _category = category;
        }

        public bool AllowElement(Element element)
        {
            return element is Group || element is AssemblyInstance || _category.Accepts(element);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }

    // Picking elements in the model and showing an element. API context only.
    internal static class ElementPicker
    {
        // Starts the Revit selection mode with the current selection preselected.
        // Members of picked groups and assemblies are read (nested ones too). Nothing is ungrouped.
        // Returns null when the user presses Esc.
        public static List<long> Pick(UIDocument uiDocument, RevitCategory category, string prompt)
        {
            Document document = uiDocument.Document;
            var filter = new CategorySelectionFilter(category);
            List<Reference> preselected = uiDocument.Selection.GetElementIds()
                .Select(id => document.GetElement(id))
                .Where(element => element != null && filter.AllowElement(element))
                .Select(element => new Reference(element))
                .ToList();

            IList<Reference> picked;
            try
            {
                picked = uiDocument.Selection.PickObjects(ObjectType.Element, filter, prompt, preselected);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }

            var ids = new List<long>();
            var visited = new HashSet<ElementId>();
            foreach (Reference reference in picked)
            {
                Collect(document.GetElement(reference), category, visited, ids);
            }

            return ids;
        }

        public static void Show(UIDocument uiDocument, long id)
        {
            ElementId elementId = RevitIds.ToElementId(id);
            if (uiDocument.Document.GetElement(elementId) == null)
            {
                return;
            }

            uiDocument.ShowElements(elementId);
            uiDocument.Selection.SetElementIds(new List<ElementId> { elementId });
        }

        // visited: an element picked alone and inside a group is taken once.
        private static void Collect(Element element, RevitCategory category, HashSet<ElementId> visited,
            List<long> ids)
        {
            if (element == null || !visited.Add(element.Id))
            {
                return;
            }

            ICollection<ElementId> members = (element as Group)?.GetMemberIds()
                ?? (element as AssemblyInstance)?.GetMemberIds();
            if (members != null)
            {
                foreach (ElementId memberId in members)
                {
                    Collect(element.Document.GetElement(memberId), category, visited, ids);
                }
            }
            else if (category.Accepts(element))
            {
                ids.Add(RevitIds.ToLong(element.Id));
            }
        }
    }
}
