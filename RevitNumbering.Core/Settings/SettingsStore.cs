using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Settings
{
    public enum SettingsLoadStatus
    {
        Ok,
        Missing,
        Corrupted,
        NewerSchemaReadOnly
    }

    public sealed class SettingsLoadResult
    {
        public SettingsLoadResult(NumberingSettings settings, SettingsLoadStatus status)
        {
            Settings = settings;
            Status = status;
        }

        public NumberingSettings Settings { get; }

        public SettingsLoadStatus Status { get; }
    }

    /// <summary>
    /// JSON settings file of one Revit version.
    /// </summary>
    public sealed class SettingsStore
    {
        public SettingsStore(string filePath, ILog log)
        {
            throw new NotImplementedException();
        }

        public string BadFilePath { get; }

        public string BackupFilePath { get; }

        public bool IsReadOnly { get; }

        public SettingsLoadResult Load()
        {
            throw new NotImplementedException();
        }

        /// <summary>Re-reads the file, replaces own category and changed general fields, writes via a temp file.</summary>
        public void Save(string categoryKey, CategorySettings category, GeneralSettingsChange general)
        {
            throw new NotImplementedException();
        }

        public void Export(string targetPath)
        {
            throw new NotImplementedException();
        }

        public SettingsLoadResult Import(string sourcePath)
        {
            throw new NotImplementedException();
        }

        /// <summary>Category defaults only from CategoryInfo data and the general defaults.</summary>
        public static CategorySettings Defaults(CategoryInfo category)
        {
            throw new NotImplementedException();
        }
    }
}
