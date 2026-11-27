namespace RevitNumbering.Core.Model
{
    // One element of any category. Categories differ only in how the Revit layer fills it.
    public sealed class NumberingItem
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public long? LevelId { get; set; }

        // Position in millimeters, in the model coordinates. Null when the element has no point.
        public double? X { get; set; }

        public double? Y { get; set; }

        // Current value of the number parameter. Empty string when it is empty.
        public string Value { get; set; }

        // A number must be unique inside this group (the phase for rooms). Null: the whole category.
        public string ConflictGroup { get; set; }

        // Why the Revit layer does not allow changing the element. None: it can be changed.
        public LockReason Lock { get; set; }

        // The user who owns the element in a workshared model.
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
