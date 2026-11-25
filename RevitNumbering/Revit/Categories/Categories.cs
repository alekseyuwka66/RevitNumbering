using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using RevitNumbering.Core.Model;
using RevitNumbering.Revit.Common;

namespace RevitNumbering.Revit.Categories
{
    public sealed class WallsCategory : RevitCategory
    {
        public override string Key => "Walls";

        public override BuiltInCategory Category => BuiltInCategory.OST_Walls;

        public override BuiltInParameter NumberParameter => BuiltInParameter.ALL_MODEL_MARK;

        /// <summary>A stacked wall is excluded together with its members.</summary>
        public override bool IsExcluded(Element element)
        {
            return base.IsExcluded(element)
                || (element is Wall wall && (wall.IsStackedWall || wall.IsStackedWallMember));
        }
    }

    public sealed class DoorsCategory : RevitCategory
    {
        public override string Key => "Doors";

        public override BuiltInCategory Category => BuiltInCategory.OST_Doors;

        public override BuiltInParameter NumberParameter => BuiltInParameter.ALL_MODEL_MARK;
    }

    public sealed class WindowsCategory : RevitCategory
    {
        public override string Key => "Windows";

        public override BuiltInCategory Category => BuiltInCategory.OST_Windows;

        public override BuiltInParameter NumberParameter => BuiltInParameter.ALL_MODEL_MARK;
    }

    public sealed class RoomsCategory : RevitCategory
    {
        public override string Key => "Rooms";

        public override BuiltInCategory Category => BuiltInCategory.OST_Rooms;

        public override BuiltInParameter NumberParameter => BuiltInParameter.ROOM_NUMBER;

        /// <summary>ROOM_NAME, not Element.Name: the name of a room element contains its number.</summary>
        public override string GetName(Element element)
        {
            return element.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? string.Empty;
        }

        public override XYZ GetPoint(Element element)
        {
            return ElementFacts.BoundingBoxCenter(element);
        }

        /// <summary>Not placed, redundant and not enclosed rooms keep their numbers.</summary>
        public override LockReason GetLock(Element element)
        {
            if (!(element is Room room))
            {
                return LockReason.None;
            }

            if (room.Location == null)
            {
                return LockReason.RoomNotPlaced;
            }

            if (room.Area > 0)
            {
                return LockReason.None;
            }

            // Boundaries are queried only when the area is zero.
            IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
            return boundaries != null && boundaries.Count > 0
                ? LockReason.RoomRedundant
                : LockReason.RoomNotEnclosed;
        }

        /// <summary>Room numbers repeat only inside one phase.</summary>
        public override string GetConflictGroup(Element element)
        {
            ElementId phaseId = element.get_Parameter(BuiltInParameter.ROOM_PHASE)?.AsElementId();
            return RevitIds.IsValid(phaseId)
                ? RevitIds.ToLong(phaseId).ToString(System.Globalization.CultureInfo.InvariantCulture)
                : null;
        }
    }
}
