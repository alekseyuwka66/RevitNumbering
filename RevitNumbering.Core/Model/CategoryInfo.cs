namespace RevitNumbering.Core.Model
{
    public enum ConflictScope
    {
        Document,
        Phase
    }

    /// <summary>
    /// Category differences needed by the core and the UI. Everything category-specific comes as data
    /// of the category descriptor; branching on Key is forbidden.
    /// </summary>
    public sealed class CategoryInfo
    {
        public CategoryInfo(string key, string displayName, string defaultTemplate, double defaultRowToleranceMm,
            ConflictScope conflictScope, bool hasSpatialStates)
        {
            Key = key;
            DisplayName = displayName;
            DefaultTemplate = defaultTemplate;
            DefaultRowToleranceMm = defaultRowToleranceMm;
            ConflictScope = conflictScope;
            HasSpatialStates = hasSpatialStates;
        }

        public string Key { get; }

        public string DisplayName { get; }

        public string DefaultTemplate { get; }

        public double DefaultRowToleranceMm { get; }

        public ConflictScope ConflictScope { get; }

        public bool HasSpatialStates { get; }
    }
}
