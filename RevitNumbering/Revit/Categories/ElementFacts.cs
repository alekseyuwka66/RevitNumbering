using Autodesk.Revit.DB;

namespace RevitNumbering.Revit.Categories
{
    /// <summary>
    /// Facts shared by several categories: the defaults of RevitCategory.
    /// Empty data is not an error: "" / InvalidElementId / null.
    /// </summary>
    internal static class ElementFacts
    {
        private const string FamilyAndTypeFormat = "{0} : {1}";

        public static string TypeName(Element element)
        {
            ElementId typeId = element.GetTypeId();
            if (typeId == null || typeId == ElementId.InvalidElementId)
            {
                return string.Empty;
            }

            return element.Document.GetElement(typeId)?.Name ?? string.Empty;
        }

        /// <summary>"Family : Type" for a family instance, otherwise the type name.</summary>
        public static string FamilyAndTypeName(Element element)
        {
            if (element is FamilyInstance instance && instance.Symbol != null)
            {
                string family = instance.Symbol.Family?.Name ?? string.Empty;
                return string.Format(System.Globalization.CultureInfo.InvariantCulture, FamilyAndTypeFormat,
                    family, instance.Symbol.Name);
            }

            return TypeName(element);
        }

        /// <summary>Parent family for a nested one, otherwise the host (a curtain wall for its panel).</summary>
        public static Element GetBase(Element element)
        {
            if (element is FamilyInstance instance)
            {
                return instance.SuperComponent ?? instance.Host;
            }

            return null;
        }

        /// <summary>Own level, otherwise the first level along the chain of bases.</summary>
        public static ElementId LevelOrBase(Element element)
        {
            var visited = new HashSet<ElementId>();
            for (Element current = element; current != null && visited.Add(current.Id); current = GetBase(current))
            {
                if (current.LevelId != null && current.LevelId != ElementId.InvalidElementId)
                {
                    return current.LevelId;
                }
            }

            return ElementId.InvalidElementId;
        }

        /// <summary>
        /// Insertion point, otherwise the middle of the location line; the same along the chain of bases;
        /// the center of the bounding box as the last resort. null: no point.
        /// </summary>
        public static XYZ PointOrBase(Element element)
        {
            var visited = new HashSet<ElementId>();
            for (Element current = element; current != null && visited.Add(current.Id); current = GetBase(current))
            {
                XYZ point = (current.Location as LocationPoint)?.Point ?? CurveMidpoint(current);
                if (point != null)
                {
                    return point;
                }
            }

            return BoundingBoxCenter(element);
        }

        public static XYZ CurveMidpoint(Element element)
        {
            Curve curve = (element.Location as LocationCurve)?.Curve;
            return curve?.Evaluate(0.5, true);
        }

        public static XYZ BoundingBoxCenter(Element element)
        {
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null)
            {
                return null;
            }

            return box.Transform.OfPoint((box.Min + box.Max) / 2);
        }

        public static bool IsInPlaceFamily(Element element)
        {
            return element is FamilyInstance instance && instance.Symbol?.Family?.IsInPlace == true;
        }

        /// <summary>Elements of secondary design options are ignored everywhere.</summary>
        public static bool IsInSecondaryOption(Element element)
        {
            DesignOption option = element.DesignOption;
            return option != null && !option.IsPrimary;
        }
    }
}
