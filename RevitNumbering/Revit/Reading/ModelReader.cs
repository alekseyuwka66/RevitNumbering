using Autodesk.Revit.DB;
using RevitNumbering.Core.Model;
using RevitNumbering.Revit.Categories;
using RevitNumbering.Revit.Common;

namespace RevitNumbering.Revit.Reading
{
    /// <summary>
    /// Reads elements of a category into NumberingItem. Knows no particular category: every difference
    /// is asked from the RevitCategory. API context only.
    /// </summary>
    internal sealed class ModelReader
    {
        /// <summary>All elements of the category in one call, and the levels of the document.</summary>
        public CategoryData Read(Document document, RevitCategory category)
        {
            var data = new CategoryData { Levels = ReadLevels(document) };
            bool workshared = document.IsWorkshared;
            foreach (Element element in new FilteredElementCollector(document)
                .OfCategory(category.Category)
                .WhereElementIsNotElementType())
            {
                if (!IsIgnored(element, category))
                {
                    data.Items.Add(ReadItem(document, category, element, workshared));
                }
            }

            return data;
        }

        /// <summary>The given elements that belong to the category and are not ignored (for the live update).</summary>
        public List<NumberingItem> ReadItems(Document document, RevitCategory category, List<long> ids)
        {
            var items = new List<NumberingItem>();
            bool workshared = document.IsWorkshared;
            foreach (long id in ids)
            {
                Element element = document.GetElement(RevitIds.ToElementId(id));
                if (IsOfCategory(element, category) && !IsIgnored(element, category))
                {
                    items.Add(ReadItem(document, category, element, workshared));
                }
            }

            return items;
        }

        public List<LevelInfo> ReadLevels(Document document)
        {
            return new FilteredElementCollector(document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .Select(level => new LevelInfo
                {
                    Id = RevitIds.ToLong(level.Id),
                    Name = level.Name,
                    ElevationMm = RevitUnits.ToMm(level.ProjectElevation),
                })
                .ToList();
        }

        /// <summary>An instance of the category (not a type).</summary>
        public static bool IsOfCategory(Element element, RevitCategory category)
        {
            return element != null
                && element.IsValidObject
                && !(element is ElementType)
                && element.Category != null
                && element.Category.Id == new ElementId(category.Category);
        }

        /// <summary>Ignored completely: secondary design options and what the category excludes.</summary>
        public static bool IsIgnored(Element element, RevitCategory category)
        {
            return ElementFacts.IsInSecondaryOption(element) || category.IsExcluded(element);
        }

        private static NumberingItem ReadItem(Document document, RevitCategory category, Element element,
            bool workshared)
        {
            Parameter parameter = element.get_Parameter(category.NumberParameter);
            ElementId levelId = category.GetLevelId(element);
            XYZ point = category.GetPoint(element);
            var item = new NumberingItem
            {
                Id = RevitIds.ToLong(element.Id),
                Name = category.GetName(element) ?? string.Empty,
                LevelId = RevitIds.IsValid(levelId) ? RevitIds.ToLong(levelId) : (long?)null,
                X = point == null ? (double?)null : RevitUnits.ToMm(point.X),
                Y = point == null ? (double?)null : RevitUnits.ToMm(point.Y),
                Value = parameter?.AsString() ?? string.Empty,
                ConflictGroup = category.GetConflictGroup(element),
                Lock = category.GetLock(element),
            };

            // Reasons common to every category win over the category's own.
            if (parameter == null || parameter.IsReadOnly)
            {
                item.Lock = LockReason.ReadOnly;
            }

            if (workshared
                && WorksharingUtils.GetCheckoutStatus(document, element.Id, out string owner)
                    == CheckoutStatus.OwnedByOtherUser)
            {
                item.Lock = LockReason.OwnedByOther;
                item.LockedBy = owner;
            }

            return item;
        }
    }
}
