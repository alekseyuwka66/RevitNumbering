using System.Runtime.Serialization;

namespace RevitNumbering.Core.Settings
{
    /// <summary>
    /// JSON schema version 1. Enumerations are stored as strings; member order is the file order.
    /// </summary>
    [DataContract]
    public sealed class TraversalSettings
    {
        [DataMember(Order = 1)]
        public string StrategyKey { get; set; }

        [DataMember(Order = 2)]
        public IDictionary<string, string> Parameters { get; set; }
    }

    [DataContract]
    public sealed class CategorySettings
    {
        [DataMember(Order = 1)]
        public string Template { get; set; }

        /// <summary>"LevelName" | "LevelParameter".</summary>
        [DataMember(Order = 2)]
        public string LevelCodeSource { get; set; }

        [DataMember(Order = 3)]
        public string LevelParameterName { get; set; }

        /// <summary>"Whole" | "WordN" | "LastWord".</summary>
        [DataMember(Order = 4)]
        public string LevelCodePart { get; set; }

        [DataMember(Order = 5)]
        public int LevelWordNumber { get; set; }

        /// <summary>"SkipOccupied" | "AllowDuplicates".</summary>
        [DataMember(Order = 6)]
        public string ConflictStrategy { get; set; }

        [DataMember(Order = 7)]
        public bool AppendMode { get; set; }

        /// <summary>
        /// Traversal of the category: strategy key + parameter values (the row tolerance lives here).
        /// In this version the key always equals the general traversal key and the general
        /// corner / direction win (F6-01: one order for all categories); per-category order (F9-04)
        /// = stop ignoring the key. See TraversalResolver.
        /// </summary>
        [DataMember(Order = 8)]
        public TraversalSettings Traversal { get; set; }
    }

    [DataContract]
    public sealed class WindowPlacement
    {
        [DataMember(Order = 1)]
        public double Left { get; set; }

        [DataMember(Order = 2)]
        public double Top { get; set; }

        [DataMember(Order = 3)]
        public double Width { get; set; }

        [DataMember(Order = 4)]
        public double Height { get; set; }
    }

    [DataContract]
    public sealed class NumberingSettings
    {
        [DataMember(Order = 1)]
        public int SchemaVersion { get; set; }

        [DataMember(Order = 2)]
        public TraversalSettings Traversal { get; set; }

        [DataMember(Order = 3)]
        public string LastCategoryKey { get; set; }

        [DataMember(Order = 4)]
        public WindowPlacement Window { get; set; }

        [DataMember(Order = 5)]
        public IDictionary<string, CategorySettings> Categories { get; set; }
    }

    /// <summary>
    /// General settings changed during a window session; null means the field was not changed.
    /// </summary>
    public sealed class GeneralSettingsChange
    {
        public TraversalSettings Traversal { get; set; }

        public string LastCategoryKey { get; set; }

        public WindowPlacement Window { get; set; }
    }
}
