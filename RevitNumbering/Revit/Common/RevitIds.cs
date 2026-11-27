using Autodesk.Revit.DB;

namespace RevitNumbering.Revit.Common
{
    // The only place where ElementId differs between Revit 2022 and 2024+. The core uses long.
    internal static class RevitIds
    {
        public static long ToLong(ElementId id)
        {
#if REVIT2024_OR_GREATER
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }

        public static ElementId ToElementId(long value)
        {
#if REVIT2024_OR_GREATER
            return new ElementId(value);
#else
            return new ElementId(checked((int)value));
#endif
        }

        public static bool IsValid(ElementId id)
        {
            return id != null && id != ElementId.InvalidElementId;
        }
    }

    // Internal units (feet) to the units of the core (mm).
    internal static class RevitUnits
    {
        public static double ToMm(double feet)
        {
            return UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);
        }
    }
}
