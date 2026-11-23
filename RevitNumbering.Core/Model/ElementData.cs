namespace RevitNumbering.Core.Model
{
    /// <summary>
    /// Raw facts about an element. The adapter makes no decisions.
    /// </summary>
    public sealed class ElementData
    {
        public ElementData(long id, string name, string levelKey, string levelName, string currentValue,
            double? xmm, double? ymm, string phaseKey, bool isParameterReadOnly, bool isOwnedByOtherUser,
            string ownerName, bool hasLocation, double areaSqM, bool hasBoundaries)
        {
            Id = id;
            Name = name ?? string.Empty;
            LevelKey = levelKey;
            LevelName = levelName;
            CurrentValue = currentValue ?? string.Empty;
            Xmm = xmm;
            Ymm = ymm;
            PhaseKey = phaseKey;
            IsParameterReadOnly = isParameterReadOnly;
            IsOwnedByOtherUser = isOwnedByOtherUser;
            OwnerName = ownerName;
            HasLocation = hasLocation;
            AreaSqM = areaSqM;
            HasBoundaries = hasBoundaries;
        }

        public long Id { get; }

        public string Name { get; }

        /// <summary>null: the element has no level.</summary>
        public string LevelKey { get; }

        public string LevelName { get; }

        public string CurrentValue { get; }

        /// <summary>Element point in mm; null: no point.</summary>
        public double? Xmm { get; }

        public double? Ymm { get; }

        /// <summary>Set for ConflictScope.Phase; otherwise null.</summary>
        public string PhaseKey { get; }

        public bool IsParameterReadOnly { get; }

        public bool IsOwnedByOtherUser { get; }

        public string OwnerName { get; }

        public bool HasLocation { get; }

        public double AreaSqM { get; }

        public bool HasBoundaries { get; }
    }
}
