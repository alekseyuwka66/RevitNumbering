using Autodesk.Revit.DB;
using RevitNumbering.Core.Domain.Elements;
using RevitNumbering.Revit.Common;

namespace RevitNumbering.Revit.Categories
{
    /// <summary>
    /// Base of a category: reads the facts common to every category (id, name, level, value,
    /// editability, point) and lets the descendant create its own element type.
    /// </summary>
    public abstract class RevitCategory<TElement> : IRevitCategory
        where TElement : NumberableElement
    {
        private static readonly IReadOnlyList<IElementExclusion> NoExclusions = Array.Empty<IElementExclusion>();

        public abstract string Key { get; }

        public abstract string DisplayName { get; }

        public abstract string DefaultTemplate { get; }

        public abstract BuiltInCategory BuiltInCategory { get; }

        public abstract BuiltInParameter NumberParameter { get; }

        public virtual IReadOnlyList<IElementExclusion> Exclusions => NoExclusions;

        public virtual FilteredElementCollector Filter(FilteredElementCollector source)
        {
            return source.OfCategory(BuiltInCategory).WhereElementIsNotElementType();
        }

        public NumberableElement Read(Element element, ReadContext context)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Parameter parameter = element.get_Parameter(NumberParameter);
            context.ReadLevel(GetLevelId(element), out string levelKey, out string levelName);
            var common = new ElementCommon(
                RevitIds.ToLong(element.Id),
                GetName(element) ?? string.Empty,
                levelKey,
                levelName,
                parameter?.AsString() ?? string.Empty,
                ReadContext.ToPlanPoint(GetPoint(element)),
                context.ReadEditState(element, parameter));
            return Create(common, element, context);
        }

        /// <summary>Name for the table (F3-07); empty data gives "".</summary>
        protected abstract string GetName(Element element);

        /// <summary>InvalidElementId: no level.</summary>
        protected abstract ElementId GetLevelId(Element element);

        /// <summary>Point in internal units (F6-07); null: no point.</summary>
        protected abstract XYZ GetPoint(Element element);

        protected abstract TElement Create(ElementCommon common, Element element, ReadContext context);
    }
}
