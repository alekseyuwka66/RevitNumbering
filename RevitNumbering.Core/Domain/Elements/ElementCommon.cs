namespace RevitNumbering.Core.Domain.Elements
{
    /// <summary>Point in plan, mm. Computed by the adapter when reading, not by the element.</summary>
    public readonly struct PlanPoint
    {
        public PlanPoint(double xMm, double yMm)
        {
            XMm = xMm;
            YMm = yMm;
        }

        public double XMm { get; }

        public double YMm { get; }
    }

    /// <summary>Editability under worksharing. Every element of every category has it.</summary>
    public sealed class EditState
    {
        public static readonly EditState Free = new EditState(false, null, false);

        public EditState(bool isOwnedByOtherUser, string ownerName, bool isParameterReadOnly)
        {
            IsOwnedByOtherUser = isOwnedByOtherUser;
            OwnerName = ownerName;
            IsParameterReadOnly = isParameterReadOnly;
        }

        public bool IsOwnedByOtherUser { get; }

        public string OwnerName { get; }

        public bool IsParameterReadOnly { get; }
    }

    /// <summary>
    /// Fields common to all categories in one object, so that constructors of the element classes stay short.
    /// </summary>
    public sealed class ElementCommon
    {
        public ElementCommon(long id, string name, string levelKey, string levelName, string currentValue,
            PlanPoint? location, EditState edit)
        {
            Id = id;
            Name = name ?? string.Empty;
            LevelKey = levelKey;
            LevelName = levelName;
            CurrentValue = currentValue ?? string.Empty;
            Location = location;
            Edit = edit ?? EditState.Free;
        }

        public long Id { get; }

        public string Name { get; }

        /// <summary>null: the element has no level.</summary>
        public string LevelKey { get; }

        public string LevelName { get; }

        public string CurrentValue { get; }

        /// <summary>null: the element has no point.</summary>
        public PlanPoint? Location { get; }

        public EditState Edit { get; }
    }
}
