namespace RevitNumbering.Core.Settings
{
    public enum SettingsNoticeKind
    {
        /// <summary>The file was unreadable or corrupted: defaults are used. Args: the path of the kept copy.</summary>
        FileCorrupted,

        /// <summary>Records of some categories were missing or invalid: their defaults are used. Args: category keys.</summary>
        CategoriesReset,

        /// <summary>The file was written by a newer version: it is read but never overwritten.</summary>
        NewerSchema,

        /// <summary>The file could not be written. Args: the path.</summary>
        SaveFailed,
    }

    /// <summary>Something about the settings file the user must be told; the text is built by the UI.</summary>
    public sealed class SettingsNotice
    {
        public SettingsNotice(SettingsNoticeKind kind, params string[] args)
        {
            Kind = kind;
            Args = args.ToList();
        }

        public SettingsNoticeKind Kind { get; }

        public List<string> Args { get; }
    }

    public sealed class SettingsLoadResult
    {
        public NumberingSettings Settings { get; set; }

        public List<SettingsNotice> Notices { get; set; } = new List<SettingsNotice>();
    }

    public enum ImportResult
    {
        Imported,

        /// <summary>The file cannot be read.</summary>
        Unreadable,

        /// <summary>Not a settings file, or a category record in it is invalid.</summary>
        Invalid,

        /// <summary>The file was written by a newer version.</summary>
        NewerSchema,

        /// <summary>The settings file could not be replaced.</summary>
        SaveFailed,
    }
}
