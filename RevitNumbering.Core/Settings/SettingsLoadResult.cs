namespace RevitNumbering.Core.Settings
{
    // The settings for the registered categories, and what the user must be told about the file.
    public sealed class SettingsLoadResult
    {
        public NumberingSettings Settings { get; set; }

        // The file exists but cannot be read. Defaults are used, and the file is never overwritten,
        // so the user can fix or delete it.
        public bool FileUnreadable { get; set; }

        // Categories whose records were missing or invalid. Their defaults are used.
        public List<string> ResetCategories { get; } = new List<string>();
    }
}
