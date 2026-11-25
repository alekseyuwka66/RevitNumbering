using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using RevitNumbering.Core.Domain.Elements;
using RevitNumbering.Revit.Common;

namespace RevitNumbering.Revit.Categories
{
    public sealed class WallsCategory : RevitCategory<WallElement>
    {
        private const string LitCatWalls = "Стены";                     // LIT-CAT-WALLS
        private const string PrmTemplateWalls = "С-{Уровень}-{N:000}";  // PRM-TEMPLATE-WALLS

        private static readonly IReadOnlyList<IElementExclusion> OwnExclusions =
            new IElementExclusion[] { new StackedWallExclusion() };     // F2-07 (a)

        public override string Key => "Walls";

        public override string DisplayName => LitCatWalls;

        public override string DefaultTemplate => PrmTemplateWalls;

        public override BuiltInCategory BuiltInCategory => BuiltInCategory.OST_Walls;

        public override BuiltInParameter NumberParameter => BuiltInParameter.ALL_MODEL_MARK;

        public override IReadOnlyList<IElementExclusion> Exclusions => OwnExclusions;

        protected override string GetName(Element element) => ElementFacts.TypeName(element);

        protected override ElementId GetLevelId(Element element) =>
            element is Wall wall ? wall.LevelId : element.LevelId;

        protected override XYZ GetPoint(Element element) => ElementFacts.CurveMidpoint(element);

        protected override WallElement Create(ElementCommon common, Element element, ReadContext context) =>
            new WallElement(common);
    }

    public sealed class DoorsCategory : RevitCategory<DoorElement>
    {
        private const string LitCatDoors = "Двери";                     // LIT-CAT-DOORS
        private const string PrmTemplateDoors = "Д-{Уровень}-{N:000}";  // PRM-TEMPLATE-DOORS

        public override string Key => "Doors";

        public override string DisplayName => LitCatDoors;

        public override string DefaultTemplate => PrmTemplateDoors;

        public override BuiltInCategory BuiltInCategory => BuiltInCategory.OST_Doors;

        public override BuiltInParameter NumberParameter => BuiltInParameter.ALL_MODEL_MARK;

        protected override string GetName(Element element) => ElementFacts.FamilyAndTypeName(element);

        protected override ElementId GetLevelId(Element element) => ElementFacts.LevelOrBase(element);

        protected override XYZ GetPoint(Element element) => ElementFacts.InsertionPointOrBase(element);

        protected override DoorElement Create(ElementCommon common, Element element, ReadContext context) =>
            new DoorElement(common);
    }

    public sealed class WindowsCategory : RevitCategory<WindowElement>
    {
        private const string LitCatWindows = "Окна";                    // LIT-CAT-WINDOWS
        private const string PrmTemplateWindows = "О-{Уровень}-{N:000}"; // PRM-TEMPLATE-WINDOWS

        public override string Key => "Windows";

        public override string DisplayName => LitCatWindows;

        public override string DefaultTemplate => PrmTemplateWindows;

        public override BuiltInCategory BuiltInCategory => BuiltInCategory.OST_Windows;

        public override BuiltInParameter NumberParameter => BuiltInParameter.ALL_MODEL_MARK;

        protected override string GetName(Element element) => ElementFacts.FamilyAndTypeName(element);

        protected override ElementId GetLevelId(Element element) => ElementFacts.LevelOrBase(element);

        protected override XYZ GetPoint(Element element) => ElementFacts.InsertionPointOrBase(element);

        protected override WindowElement Create(ElementCommon common, Element element, ReadContext context) =>
            new WindowElement(common);
    }

    public sealed class RoomsCategory : RevitCategory<RoomElement>
    {
        private const string LitCatRooms = "Помещения";                 // LIT-CAT-ROOMS
        private const string PrmTemplateRooms = "{Уровень}{N:00}";      // PRM-TEMPLATE-ROOMS

        public override string Key => "Rooms";

        public override string DisplayName => LitCatRooms;

        public override string DefaultTemplate => PrmTemplateRooms;

        public override BuiltInCategory BuiltInCategory => BuiltInCategory.OST_Rooms;

        public override BuiltInParameter NumberParameter => BuiltInParameter.ROOM_NUMBER;

        /// <summary>ROOM_NAME, not Element.Name: Room.Name contains the number (06 §3.4).</summary>
        protected override string GetName(Element element) =>
            element.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? string.Empty;

        protected override ElementId GetLevelId(Element element) => element.LevelId;

        protected override XYZ GetPoint(Element element) => ElementFacts.BoundingBoxCenter(element);

        protected override RoomElement Create(ElementCommon common, Element element, ReadContext context)
        {
            var room = element as Room;
            if (room == null)
            {
                return new RoomElement(common, true, 0, false, null);
            }

            // F6-08 facts; AA-63: boundaries are queried only when the area is zero.
            bool isPlaced = room.Location != null;
            double areaSqFt = room.Area;
            bool hasBoundaries = false;
            if (isPlaced)
            {
                if (areaSqFt > 0)
                {
                    hasBoundaries = true;
                }
                else
                {
                    IList<IList<BoundarySegment>> segments =
                        room.GetBoundarySegments(new SpatialElementBoundaryOptions());
                    hasBoundaries = segments != null && segments.Count > 0;
                }
            }

            ElementId phaseId = room.get_Parameter(BuiltInParameter.ROOM_PHASE)?.AsElementId();
            return new RoomElement(common, isPlaced, RevitUnits.ToSquareMeters(areaSqFt), hasBoundaries,
                ReadContext.PhaseKey(phaseId));
        }
    }
}
