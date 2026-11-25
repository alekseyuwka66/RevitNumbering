using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Settings
{
    /// <summary>
    /// The JSON settings file of one Revit version and the embedded defaults of the same format.
    /// The file is always written whole: the last save wins.
    /// </summary>
    public sealed class SettingsStore
    {
        private const string DefaultsResource = "RevitNumbering.Core.Settings.defaults.json";
        private const string TempSuffix = ".tmp";
        private const int SaveAttempts = 3;
        private const int SaveRetryDelayMs = 100;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            // Templates are Cyrillic: keep them readable in the file.
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly ILog _log;

        public SettingsStore(string filePath, ILog log)
        {
            FilePath = filePath;
            _log = log;
        }

        public string FilePath { get; }

        /// <summary>Where a corrupted file is copied to.</summary>
        public string BadFilePath => FilePath + ".bad";

        /// <summary>Where the previous file is kept after an import.</summary>
        public string BackupFilePath => FilePath + ".bak";

        /// <summary>true: the file has a newer schema and is never overwritten.</summary>
        public bool IsReadOnly { get; private set; }

        public static string Serialize(NumberingSettings settings)
        {
            return JsonSerializer.Serialize(settings, Options);
        }

        /// <summary>
        /// Strict defaults: every registered category must have a valid record in the embedded defaults.
        /// An empty list: everything is in place.
        /// </summary>
        public static List<Problem> CheckDefaults(IReadOnlyList<string> categoryKeys)
        {
            var problems = new List<Problem>();
            ParsedFile defaults;
            try
            {
                defaults = Parse(ReadDefaultsText());
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException)
            {
                problems.Add(new Problem(ProblemCode.DefaultsUnreadable));
                return problems;
            }

            foreach (string key in categoryKeys)
            {
                if (defaults.InvalidKeys.Contains(key))
                {
                    problems.Add(new Problem(ProblemCode.DefaultsCategoryInvalid, key));
                }
                else if (defaults.Settings.Find(key) == null)
                {
                    problems.Add(new Problem(ProblemCode.DefaultsCategoryMissing, key));
                }
            }

            return problems;
        }

        /// <summary>The embedded defaults: a new object on every call.</summary>
        public static NumberingSettings ReadDefaults()
        {
            return Parse(ReadDefaultsText()).Settings;
        }

        /// <summary>
        /// Settings for the registered categories. A missing file gives the defaults silently; a corrupted file
        /// is copied to .bad; a missing or invalid category record is replaced by its default record. Never throws.
        /// </summary>
        public SettingsLoadResult Load(IReadOnlyList<string> categoryKeys)
        {
            var result = new SettingsLoadResult();
            IsReadOnly = false;

            ParsedFile parsed = null;
            bool fileExists = false;
            try
            {
                fileExists = File.Exists(FilePath);
                if (fileExists)
                {
                    parsed = Parse(File.ReadAllText(FilePath));
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
            {
                _log.Warning("[Settings] file is unreadable, defaults are used: " + FilePath + " (" + ex.Message + ")");
                KeepBadCopy();
                result.Notices.Add(new SettingsNotice(SettingsNoticeKind.FileCorrupted, BadFilePath));
            }

            if (parsed != null && parsed.Settings.SchemaVersion > NumberingSettings.CurrentSchemaVersion)
            {
                IsReadOnly = true;
                _log.Warning("[Settings] file has a newer schema and will not be overwritten: " + FilePath);
                result.Notices.Add(new SettingsNotice(SettingsNoticeKind.NewerSchema));
            }

            var reset = new List<string>();
            result.Settings = Complete(parsed, categoryKeys, reset);
            if (parsed != null && reset.Count > 0)
            {
                _log.Warning("[Settings] default records are used for categories: " + string.Join(", ", reset));
                result.Notices.Add(new SettingsNotice(SettingsNoticeKind.CategoriesReset, reset.ToArray()));
            }

            return result;
        }

        /// <summary>Writes the whole file through a temporary file. false: not saved (see the log).</summary>
        public bool Save(NumberingSettings settings)
        {
            if (IsReadOnly)
            {
                _log.Warning("[Settings] not saved, the file has a newer schema: " + FilePath);
                return false;
            }

            return Write(FilePath, Serialize(settings), null);
        }

        /// <summary>Writes the settings to the chosen file.</summary>
        public bool Export(NumberingSettings settings, string path)
        {
            return Write(path, Serialize(settings), null);
        }

        /// <summary>
        /// Checks the file and, when it is valid, replaces the settings file with it (the previous one
        /// becomes .bak). An invalid file changes nothing.
        /// </summary>
        public ImportResult Import(string path, IReadOnlyList<string> categoryKeys, out NumberingSettings imported)
        {
            imported = null;
            ParsedFile parsed;
            try
            {
                parsed = Parse(File.ReadAllText(path));
            }
            catch (JsonException)
            {
                _log.Warning("[Settings] import refused, not a settings file: " + path);
                return ImportResult.Invalid;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException
                || ex is ArgumentException || ex is NotSupportedException)
            {
                _log.Warning("[Settings] import refused, the file is unreadable: " + path + " (" + ex.Message + ")");
                return ImportResult.Unreadable;
            }

            if (parsed.Settings.SchemaVersion > NumberingSettings.CurrentSchemaVersion)
            {
                _log.Warning("[Settings] import refused, the file has a newer schema: " + path);
                return ImportResult.NewerSchema;
            }

            if (parsed.InvalidKeys.Count > 0)
            {
                _log.Warning("[Settings] import refused, invalid category records: "
                    + string.Join(", ", parsed.InvalidKeys));
                return ImportResult.Invalid;
            }

            NumberingSettings settings = Complete(parsed, categoryKeys, new List<string>());
            if (!Write(FilePath, Serialize(settings), BackupFilePath))
            {
                return ImportResult.SaveFailed;
            }

            IsReadOnly = false;
            imported = settings;
            _log.Info("[Settings] imported from " + path);
            return ImportResult.Imported;
        }

        private static string ReadDefaultsText()
        {
            using (Stream stream = typeof(SettingsStore).Assembly.GetManifestResourceStream(DefaultsResource))
            {
                if (stream == null)
                {
                    throw new IOException("Embedded resource is missing: " + DefaultsResource);
                }

                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Reads the text. Category records are read one by one, so that one invalid record does not
        /// spoil the others. Throws JsonException when the text is not a settings file at all.
        /// </summary>
        private static ParsedFile Parse(string json)
        {
            RawSettings raw = JsonSerializer.Deserialize<RawSettings>(json, Options)
                ?? throw new JsonException("The settings file is empty.");

            var parsed = new ParsedFile();
            parsed.Settings.SchemaVersion = raw.SchemaVersion;
            parsed.Settings.LastCategory = raw.LastCategory;
            parsed.Settings.Window = IsValid(raw.Window) ? raw.Window : null;

            foreach (JsonElement element in raw.Categories ?? new List<JsonElement>())
            {
                CategorySettings category = null;
                try
                {
                    category = element.Deserialize<CategorySettings>(Options);
                }
                catch (Exception ex) when (ex is JsonException || ex is NotSupportedException)
                {
                    // An unknown ordering type, a wrong enum value and the like: the record is invalid.
                }

                if (category != null && !string.IsNullOrEmpty(category.CategoryKey) && category.IsValid())
                {
                    parsed.Settings.Categories.Add(category);
                }
                else
                {
                    parsed.InvalidKeys.Add(category?.CategoryKey ?? ReadKey(element));
                }
            }

            return parsed;
        }

        private static string ReadKey(JsonElement element)
        {
            return element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty("categoryKey", out JsonElement key)
                && key.ValueKind == JsonValueKind.String
                ? key.GetString()
                : string.Empty;
        }

        private static bool IsValid(WindowPlacement window)
        {
            return window != null
                && IsFinite(window.Left) && IsFinite(window.Top)
                && IsFinite(window.Width) && IsFinite(window.Height)
                && window.Width > 0 && window.Height > 0;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        /// <summary>
        /// Settings with a record for every registered category, in the registration order:
        /// the record of the file, otherwise the default record (its key is added to reset).
        /// </summary>
        private static NumberingSettings Complete(ParsedFile parsed, IReadOnlyList<string> categoryKeys,
            List<string> reset)
        {
            NumberingSettings defaults = ReadDefaults();
            NumberingSettings source = parsed?.Settings ?? defaults;
            var settings = new NumberingSettings
            {
                LastCategory = categoryKeys.Contains(source.LastCategory) ? source.LastCategory : defaults.LastCategory,
                Window = source.Window,
            };

            foreach (string key in categoryKeys)
            {
                CategorySettings category = source.Find(key);
                if (category == null)
                {
                    category = defaults.Find(key);
                    reset.Add(key);
                }

                if (category != null)
                {
                    settings.Categories.Add(category);
                }
            }

            return settings;
        }

        /// <summary>Temporary file, then replace. IOException: up to three attempts.</summary>
        private bool Write(string path, string json, string backupPath)
        {
            string tempPath = path + TempSuffix;
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    string directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllText(tempPath, json, new UTF8Encoding(false));
                    if (File.Exists(path))
                    {
                        File.Replace(tempPath, path, backupPath);
                    }
                    else
                    {
                        File.Move(tempPath, path);
                    }

                    return true;
                }
                catch (IOException) when (attempt < SaveAttempts)
                {
                    Thread.Sleep(SaveRetryDelayMs);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException
                    || ex is ArgumentException || ex is NotSupportedException)
                {
                    _log.Warning("[Settings] cannot write " + path + ": " + ex.Message);
                    TryDelete(tempPath);
                    return false;
                }
            }
        }

        private void KeepBadCopy()
        {
            try
            {
                File.Copy(FilePath, BadFilePath, true);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _log.Warning("[Settings] cannot copy the corrupted file to " + BadFilePath + ": " + ex.Message);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException
                || ex is ArgumentException || ex is NotSupportedException)
            {
                // The temporary file stays; the next save overwrites it.
            }
        }

        /// <summary>The file as written: category records are kept raw to read them one by one.</summary>
        private sealed class RawSettings
        {
            public int SchemaVersion { get; set; }

            public string LastCategory { get; set; }

            public WindowPlacement Window { get; set; }

            public List<JsonElement> Categories { get; set; }
        }

        private sealed class ParsedFile
        {
            /// <summary>Only valid category records.</summary>
            public NumberingSettings Settings { get; } = new NumberingSettings { Categories = new List<CategorySettings>() };

            /// <summary>Keys of invalid records ("" when the key is unreadable).</summary>
            public List<string> InvalidKeys { get; } = new List<string>();
        }
    }
}
