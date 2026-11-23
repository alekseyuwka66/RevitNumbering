namespace RevitNumbering.Core.Model
{
    public enum ConflictScope
    {
        Document,
        Phase
    }

    /// <summary>
    /// Limits of the row tolerance; used for descriptor data and for user input checks.
    /// </summary>
    public static class RowToleranceLimits
    {
        public const double MinMm = 1;

        public const double MaxMm = 100000;
    }

    /// <summary>
    /// Category differences needed by the core and the UI. Everything category-specific comes as data
    /// of the category descriptor; branching on Key is forbidden.
    /// </summary>
    public sealed class CategoryInfo
    {
        /// <summary>
        /// Arguments are checked here so that a descriptor mistake shows at plugin startup.
        /// The default template is not parsed: the core checks it like any entered text.
        /// </summary>
        public CategoryInfo(string key, string displayName, string defaultTemplate, double defaultRowToleranceMm,
            ConflictScope conflictScope, bool hasSpatialStates)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Category key must not be empty.", nameof(key));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Category display name must not be empty.", nameof(displayName));
            }

            if (defaultTemplate == null)
            {
                throw new ArgumentNullException(nameof(defaultTemplate));
            }

            if (double.IsNaN(defaultRowToleranceMm)
                || defaultRowToleranceMm < RowToleranceLimits.MinMm
                || defaultRowToleranceMm > RowToleranceLimits.MaxMm)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultRowToleranceMm));
            }

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
