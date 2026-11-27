using Autodesk.Revit.DB;
using RevitNumbering.Core.Model;

namespace RevitNumbering.Revit.Categories
{
    // A numbered category: which elements, which parameter, and how one element is read.
    // The defaults fit most categories. A category overrides only what is different.
    // Missing data is not an error: "", InvalidElementId or null.
    // A new category needs: one class, one line in PluginEntry, a record in defaults.json,
    // and a name in Texts.resx.
    public abstract class RevitCategory
    {
        // A nested family or a curtain panel reaches its level in a step or two. The limit only protects the loop.
        private const int MaxBaseDepth = 10;

        public abstract string Key { get; }

        public abstract BuiltInCategory Category { get; }

        public abstract BuiltInParameter NumberParameter { get; }

        // An element that is read, shown, picked and numbered: an instance of the category,
        // not in a secondary design option, and not excluded by the category.
        public bool Accepts(Element element)
        {
            return element != null
                && !(element is ElementType)
                && element.Category != null
                && element.Category.Id == new ElementId(Category)
                && (element.DesignOption == null || element.DesignOption.IsPrimary)
                && !IsExcluded(element);
        }

        // "Family : Type" for a family instance, otherwise the type name.
        public virtual string GetName(Element element)
        {
            if (element is FamilyInstance instance && instance.Symbol != null)
            {
                return (instance.Symbol.Family?.Name ?? string.Empty) + " : " + instance.Symbol.Name;
            }

            ElementId typeId = element.GetTypeId();
            return typeId == ElementId.InvalidElementId
                ? string.Empty
                : element.Document.GetElement(typeId)?.Name ?? string.Empty;
        }

        // Own level, otherwise the level of the base: the parent family, or the host (a curtain wall for its panel).
        public virtual ElementId GetLevelId(Element element)
        {
            Element current = element;
            for (int depth = 0; current != null && depth < MaxBaseDepth; depth++)
            {
                if (current.LevelId != null && current.LevelId != ElementId.InvalidElementId)
                {
                    return current.LevelId;
                }

                current = current is FamilyInstance instance ? instance.SuperComponent ?? instance.Host : null;
            }

            return ElementId.InvalidElementId;
        }

        // The insertion point, otherwise the middle of the location line, otherwise the center of the bounding box.
        // Returns null when there is no point.
        public virtual XYZ GetPoint(Element element)
        {
            return (element.Location as LocationPoint)?.Point
                ?? (element.Location as LocationCurve)?.Curve?.Evaluate(0.5, true)
                ?? BoundingBoxCenter(element);
        }

        // Excluded elements are not read, shown or numbered, and their numbers are not treated as used.
        // By default: in-place families.
        public virtual bool IsExcluded(Element element)
        {
            return element is FamilyInstance instance && instance.Symbol?.Family?.IsInPlace == true;
        }

        // The category's own reason why the element cannot be numbered.
        public virtual LockReason GetLock(Element element)
        {
            return LockReason.None;
        }

        // The category's own area for checking used numbers. Null: the whole category.
        public virtual string GetConflictGroup(Element element)
        {
            return null;
        }

        protected static XYZ BoundingBoxCenter(Element element)
        {
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            return box == null ? null : box.Transform.OfPoint((box.Min + box.Max) / 2);
        }
    }
}
