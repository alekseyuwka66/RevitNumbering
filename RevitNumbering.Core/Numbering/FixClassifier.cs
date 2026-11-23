using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Numbering
{
    /// <summary>
    /// Fix classification by raw facts; the order of checks is the FixReason value order.
    /// </summary>
    public static class FixClassifier
    {
        public static FixReason Classify(ElementData element, CategoryInfo category, bool templateUsesLevel)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (category == null)
            {
                throw new ArgumentNullException(nameof(category));
            }

            if (element.IsOwnedByOtherUser)
            {
                return FixReason.OwnedByOtherUser;
            }

            if (element.IsParameterReadOnly)
            {
                return FixReason.ReadOnlyParameter;
            }

            // Spatial facts are read only for categories with spatial states (rooms).
            if (category.HasSpatialStates)
            {
                if (!element.HasLocation)
                {
                    return FixReason.RoomNotPlaced;
                }

                // Exact comparison with zero.
                if (element.AreaSqM == 0)
                {
                    return element.HasBoundaries ? FixReason.RoomRedundant : FixReason.RoomNotEnclosed;
                }
            }

            if (templateUsesLevel && element.LevelKey == null)
            {
                return FixReason.NoLevel;
            }

            return FixReason.None;
        }
    }
}
