using Autodesk.Revit.DB;
using RevitNumbering.Core.Model;
using RevitNumbering.Revit.Categories;
using RevitNumbering.Revit.Common;

namespace RevitNumbering.Revit.Reading
{
    // Reads elements of a category into NumberingItem. It knows no particular category:
    // every difference comes from RevitCategory. API context only.
    internal static class ModelReader
    {
        public static CategoryData Read(Document document, RevitCategory category)
        {
            var data = new CategoryData { Levels = ReadLevels(document) };
            bool workshared = document.IsWorkshared;
            foreach (Element element in new FilteredElementCollector(document)
                .OfCategory(category.Category)
                .WhereElementIsNotElementType())
            {
                if (category.Accepts(element))
                {
                    data.Items.Add(ReadItem(document, category, element, workshared));
                }
            }

            return data;
        }

        private static List<LevelInfo> ReadLevels(Document document)
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
