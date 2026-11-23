namespace RevitNumbering.Core.Model
{
    /// <summary>
    /// Why a scope element is fixed. Value order is the priority.
    /// </summary>
    public enum FixReason
    {
        None = 0,
        OwnedByOtherUser = 1,
        ReadOnlyParameter = 2,
        RoomNotPlaced = 3,
        RoomNotEnclosed = 4,
        RoomRedundant = 5,
        NoLevel = 6
    }
}
