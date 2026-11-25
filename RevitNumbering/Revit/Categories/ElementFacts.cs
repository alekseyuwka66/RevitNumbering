using Autodesk.Revit.DB;

namespace RevitNumbering.Revit.Categories
{
    /// <summary>
    /// Facts shared by several categories (06 §3.4, §3.5). Empty data is not an error: "" / InvalidElementId / null.
    /// </summary>
    internal static class ElementFacts
    {
        /// <summary>LIT-M06-01: "Family : Type".</summary>
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

        /// <summary>Parent family for a nested one, otherwise the host (curtain wall for a panel).</summary>
        public static Element GetBase(Element element)
        {
            if (element is FamilyInstance instance)
            {
                return instance.SuperComponent ?? instance.Host;
            }

            return null;
        }

        /// <summary>Own level, otherwise the first level along the chain of bases (AA-65).</summary>
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

        /// <summary>Own insertion point, otherwise the point of the first base that has one (AA-65).</summary>
        public static XYZ InsertionPointOrBase(Element element)
        {
            if (element.Location is LocationPoint own)
            {
                return own.Point;
            }

            var visited = new HashSet<ElementId> { element.Id };
            for (Element current = GetBase(element); current != null && visited.Add(current.Id); current = GetBase(current))
            {
                XYZ point = (current.Location as LocationPoint)?.Point ?? CurveMidpoint(current);
                if (point != null)
                {
                    return point;
                }
            }

            return null;
        }

        /// <summary>Middle of the location line (F6-07 for walls).</summary>
        public static XYZ CurveMidpoint(Element element)
        {
            Curve curve = (element.Location as LocationCurve)?.Curve;
            return curve?.Evaluate(0.5, true);
        }

        /// <summary>Center of the bounding box (F6-07 for rooms).</summary>
        public static XYZ BoundingBoxCenter(Element element)
        {
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null)
            {
                return null;
            }

            return box.Transform.OfPoint((box.Min + box.Max) / 2);
        }
    }
}
