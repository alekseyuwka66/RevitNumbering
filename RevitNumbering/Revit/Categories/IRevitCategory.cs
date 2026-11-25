using Autodesk.Revit.DB;
using RevitNumbering.Core.Domain.Categories;
using RevitNumbering.Core.Domain.Elements;

namespace RevitNumbering.Revit.Categories
{
    /// <summary>
    /// What the adapter knows about a category: which Revit elements, which parameter, how to read one element.
    /// The core sees the same object as INumberingCategory. A new category = one class + one registration line
    /// in PluginEntry (F2-05).
    /// </summary>
    public interface IRevitCategory : INumberingCategory
    {
        BuiltInCategory BuiltInCategory { get; }

        /// <summary>Target parameter, only by BuiltInParameter (F2-02).</summary>
        BuiltInParameter NumberParameter { get; }

        /// <summary>Rules of this category only; rules common to all categories live in CategoryCollector.</summary>
        IReadOnlyList<IElementExclusion> Exclusions { get; }

        /// <summary>Narrows the prepared collector to the elements of the category.</summary>
        FilteredElementCollector Filter(FilteredElementCollector source);

        /// <summary>Raw facts of one element; decisions are made by the core.</summary>
        NumberableElement Read(Element element, ReadContext context);
    }

    /// <summary>
    /// An exclusion rule (F2-07, Q-10): an excluded element is not read and takes part nowhere.
    /// </summary>
    public interface IElementExclusion
    {
        string Key { get; }

        /// <summary>false for elements of other types; never throws for them.</summary>
        bool IsExcluded(Element element);
    }

    /// <summary>F2-07 (b): in-place families are excluded in every category.</summary>
    public sealed class InPlaceFamilyExclusion : IElementExclusion
    {
        public string Key => "InPlaceFamily";

        public bool IsExcluded(Element element)
        {
            return element is FamilyInstance instance && instance.Symbol?.Family?.IsInPlace == true;
        }
    }

    /// <summary>F2-07 (a): a stacked wall is excluded together with all its members.</summary>
    public sealed class StackedWallExclusion : IElementExclusion
    {
        public string Key => "StackedWall";

        public bool IsExcluded(Element element)
        {
            return element is Wall wall && (wall.IsStackedWall || wall.IsStackedWallMember);
        }
    }
}
