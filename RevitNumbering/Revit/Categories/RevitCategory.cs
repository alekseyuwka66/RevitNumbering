using Autodesk.Revit.DB;
using RevitNumbering.Core.Model;

namespace RevitNumbering.Revit.Categories
{
    /// <summary>
    /// A numbered category: which elements, which parameter and how one element is read.
    /// The defaults fit most categories; a category overrides only what differs.
    /// A new category = one class + one line in the list of PluginEntry (+ its record in defaults.json
    /// and its name in Texts.resx).
    /// </summary>
    public abstract class RevitCategory
    {
        /// <summary>The key in the settings and in defaults.json, for example "Doors".</summary>
        public abstract string Key { get; }

        public abstract BuiltInCategory Category { get; }

        /// <summary>The parameter that gets the number.</summary>
        public abstract BuiltInParameter NumberParameter { get; }

        /// <summary>"Family : Type", otherwise the type name.</summary>
        public virtual string GetName(Element element)
        {
            return ElementFacts.FamilyAndTypeName(element);
        }

        /// <summary>Own level, otherwise the level of the base (parent family or host).</summary>
        public virtual ElementId GetLevelId(Element element)
        {
            return ElementFacts.LevelOrBase(element);
        }

        /// <summary>Insertion point, the middle of the line, the base, the center of the bounding box.</summary>
        public virtual XYZ GetPoint(Element element)
        {
            return ElementFacts.PointOrBase(element);
        }

        /// <summary>Excluded elements are not read, shown or numbered, and their numbers are not occupied.</summary>
        public virtual bool IsExcluded(Element element)
        {
            return ElementFacts.IsInPlaceFamily(element);
        }

        /// <summary>The category's own reason why the element cannot be numbered.</summary>
        public virtual LockReason GetLock(Element element)
        {
            return LockReason.None;
        }

        /// <summary>The category's own area of the search for occupied numbers; null: the whole category.</summary>
        public virtual string GetConflictGroup(Element element)
        {
            return null;
        }
    }
}
