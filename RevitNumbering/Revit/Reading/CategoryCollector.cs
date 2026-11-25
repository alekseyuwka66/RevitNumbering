using Autodesk.Revit.DB;
using RevitNumbering.Revit.Categories;

namespace RevitNumbering.Revit.Reading
{
    /// <summary>
    /// Collecting elements of any category. Rules common to all categories are applied here once
    /// (F2-09 secondary design options, F2-07 (b) in-place families); then the category narrows the
    /// collector and applies its own exclusions. The collector never leaves the call (01 §4 p. 28).
    /// </summary>
    internal sealed class CategoryCollector
    {
        private static readonly IReadOnlyList<IElementExclusion> CommonExclusions =
            new IElementExclusion[] { new InPlaceFamilyExclusion() };

        public IList<ElementId> CollectIds(Document document, IRevitCategory category)
        {
            var collector = new FilteredElementCollector(document);

            // F2-09, AA-61: elements without an option and of primary options only.
            foreach (DesignOption option in new FilteredElementCollector(document).OfClass(typeof(DesignOption))
                .Cast<DesignOption>())
            {
                if (!option.IsPrimary)
                {
                    collector = collector.WherePasses(new ElementDesignOptionFilter(option.Id, true));
                }
            }

            return category.Filter(collector).ToElementIds().ToList();
        }

        /// <summary>Excluded elements are not read and take part nowhere (Q-10).</summary>
        public bool IsExcluded(Element element, IRevitCategory category)
        {
            foreach (IElementExclusion exclusion in CommonExclusions)
            {
                if (exclusion.IsExcluded(element))
                {
                    return true;
                }
            }

            foreach (IElementExclusion exclusion in category.Exclusions)
            {
                if (exclusion.IsExcluded(element))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
