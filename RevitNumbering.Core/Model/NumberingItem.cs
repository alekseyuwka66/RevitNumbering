namespace RevitNumbering.Core.Model
{
    /// <summary>
    /// One element of any category. Categories differ only in how the Revit layer fills it.
    /// </summary>
    public sealed class NumberingItem
    {
        public long Id { get; set; }

        public string Name { get; set; }

        /// <summary>null: no level.</summary>
        public long? LevelId { get; set; }

        /// <summary>Millimeters, internal coordinate system of the model; null: no point.</summary>
        public double? X { get; set; }

        public double? Y { get; set; }

        /// <summary>Current value of the target parameter ("" when empty).</summary>
        public string Value { get; set; }

        /// <summary>Area of the search for occupied numbers (the phase of a room); null: the whole category.</summary>
        public string ConflictGroup { get; set; }

        /// <summary>Why the Revit layer forbids changing the element; None: it can be changed.</summary>
        public LockReason Lock { get; set; }

        /// <summary>Who owns the element under worksharing.</summary>
        public string LockedBy { get; set; }
    }

    public enum LockReason
    {
        None,
        OwnedByOther,
        ReadOnly,
        NoLevel,
        RoomNotPlaced,
        RoomNotEnclosed,
        RoomRedundant,
        NotOrderable,
    }
}
