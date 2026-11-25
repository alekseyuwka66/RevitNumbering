namespace RevitNumbering.Core.Domain.Elements
{
    public sealed class DoorElement : NumberableElement
    {
        public DoorElement(ElementCommon common)
            : base(common)
        {
        }
    }

    public sealed class WindowElement : NumberableElement
    {
        public WindowElement(ElementCommon common)
            : base(common)
        {
        }
    }

    public sealed class WallElement : NumberableElement
    {
        public WallElement(ElementCommon common)
            : base(common)
        {
        }
    }

    /// <summary>
    /// A room: spatial states (F6-08) and conflicts within its phase (F2-09).
    /// </summary>
    public sealed class RoomElement : NumberableElement
    {
        public static readonly FixReason NotPlaced = new FixReason("room.notPlaced", "Не размещено");

        public static readonly FixReason NotEnclosed = new FixReason("room.notEnclosed", "Не окружено");

        public static readonly FixReason Redundant = new FixReason("room.redundant", "Избыточное");

        public RoomElement(ElementCommon common, bool isPlaced, double areaSqM, bool hasBoundaries, string phaseKey)
            : base(common)
        {
            IsPlaced = isPlaced;
            AreaSqM = areaSqM;
            HasBoundaries = hasBoundaries;
            PhaseKey = phaseKey;
        }

        public bool IsPlaced { get; }

        public double AreaSqM { get; }

        public bool HasBoundaries { get; }

        public string PhaseKey { get; }

        public override string ConflictGroup => PhaseKey;

        protected override FixReason ClassifySpecific()
        {
            if (!IsPlaced)
            {
                return NotPlaced;
            }

            // Exact comparison with zero.
            if (AreaSqM == 0)
            {
                return HasBoundaries ? Redundant : NotEnclosed;
            }

            return null;
        }
    }
}
