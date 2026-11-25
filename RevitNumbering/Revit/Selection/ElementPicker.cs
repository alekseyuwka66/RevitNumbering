using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RevitNumbering.Revit.Categories;
using RevitNumbering.Revit.Common;
using RevitNumbering.Revit.Reading;

namespace RevitNumbering.Revit.Selection
{
    /// <summary>
    /// One filter for every category: lets pick elements of the category that are not ignored,
    /// and also groups and assemblies (their members are read afterwards).
    /// </summary>
    internal sealed class CategorySelectionFilter : ISelectionFilter
    {
        private readonly RevitCategory _category;

        public CategorySelectionFilter(RevitCategory category)
        {
            _category = category;
        }

        public bool AllowElement(Element element)
        {
            return element is Group || element is AssemblyInstance || IsCategoryElement(element);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }

        public bool IsCategoryElement(Element element)
        {
            return ModelReader.IsOfCategory(element, _category) && !ModelReader.IsIgnored(element, _category);
        }
    }

    /// <summary>Picking elements in the model and showing an element. Only inside a runner call.</summary>
    internal sealed class ElementPicker
    {
        /// <summary>
        /// Starts the selection mode of Revit with the current selection preselected.
        /// Members of picked groups and assemblies are read (nested groups too); nothing is ungrouped.
        /// null: the user pressed Esc.
        /// </summary>
        public List<long> Pick(UIDocument uiDocument, RevitCategory category, string prompt)
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
                Collect(document.GetElement(reference), filter, visited, ids);
            }

            return ids;
        }

        /// <summary>Zooms to the element and selects it.</summary>
        public void Show(UIDocument uiDocument, long id)
        {
            ElementId elementId = RevitIds.ToElementId(id);
            if (uiDocument.Document.GetElement(elementId) == null)
            {
                return;
            }

            uiDocument.ShowElements(elementId);
            uiDocument.Selection.SetElementIds(new List<ElementId> { elementId });
        }

        private static void Collect(Element element, CategorySelectionFilter filter, HashSet<ElementId> visited,
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
                    Collect(element.Document.GetElement(memberId), filter, visited, ids);
                }
            }
            else if (filter.IsCategoryElement(element))
            {
                ids.Add(RevitIds.ToLong(element.Id));
            }
        }
    }
}
