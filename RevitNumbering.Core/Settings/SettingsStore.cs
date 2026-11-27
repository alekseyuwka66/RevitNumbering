using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using RevitNumbering.Core.Logging;

namespace RevitNumbering.Core.Settings
{
    // The JSON settings file of one Revit version, and the defaults embedded in the assembly (same format).
    // The file is always written whole: the last save wins.
    public sealed class SettingsStore
    {
        private const string DefaultsResource = "RevitNumbering.Core.Settings.defaults.json";

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            // Templates are Cyrillic. Keep them readable in the file.
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly FileLog _log;

        public SettingsStore(string filePath, FileLog log)
        {
            FilePath = filePath;
            _log = log;
        }

        public string FilePath { get; }

        public static string Serialize(NumberingSettings settings)
        {
            return JsonSerializer.Serialize(settings, Options);
        }

        // Every registered category needs a valid record in the embedded defaults.
        // If not, the plugin is built wrong and must not start: this throws with the details.
        public static void CheckDefaults(IReadOnlyList<string> categoryKeys)
        {
            ParsedFile defaults = Parse(ReadDefaultsText());
            List<string> broken = categoryKeys
                .Where(key => defaults.InvalidKeys.Contains(key) || defaults.Settings.Find(key) == null)
                .ToList();
            if (broken.Count > 0)
            {
                throw new InvalidOperationException(
                    "The embedded defaults have no valid record for the categories: " + string.Join(", ", broken));
            }
        }

        // Returns a new object on every call.
        public static NumberingSettings ReadDefaults()
        {
            return Parse(ReadDefaultsText()).Settings;
        }

        // No file: defaults. Unreadable file: defaults, and the file is left as it is.
        // Missing or invalid category record: the default record of that category.
        public SettingsLoadResult Load(IReadOnlyList<string> categoryKeys)
        {
            var result = new SettingsLoadResult();
            ParsedFile parsed = null;
            try
            {
                if (File.Exists(FilePath))
                {
                    parsed = Parse(File.ReadAllText(FilePath));
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
            {
                _log.Warning("[Settings] file is unreadable, defaults are used: " + FilePath + " (" + ex.Message + ")");
                result.FileUnreadable = true;
            }

            result.Settings = Complete(parsed, categoryKeys, parsed == null ? null : result.ResetCategories);
            if (result.ResetCategories.Count > 0)
            {
                _log.Warning("[Settings] default records are used for categories: "
                    + string.Join(", ", result.ResetCategories));
            }

            return result;
        }

        public bool Save(NumberingSettings settings)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, Serialize(settings), new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _log.Warning("[Settings] cannot write " + FilePath + ": " + ex.Message);
                return false;
            }
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

        // Category records are read one by one, so one invalid record does not spoil the others.
        // Throws JsonException when the text is not a settings file at all.
        private static ParsedFile Parse(string json)
        {
            RawSettings raw = JsonSerializer.Deserialize<RawSettings>(json, Options)
                ?? throw new JsonException("The settings file is empty.");

            var parsed = new ParsedFile();
            parsed.Settings.LastCategory = raw.LastCategory;

            foreach (JsonElement element in raw.Categories ?? new List<JsonElement>())
            {
                // An unknown ordering type or a wrong enum value makes the record invalid.
                CategorySettings category = null;
                try
                {
                    category = element.Deserialize<CategorySettings>(Options);
                }
                catch (Exception ex) when (ex is JsonException || ex is NotSupportedException)
                {
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

        // Returns settings with a record for every registered category, in the registration order:
        // the record from the file, or the default one (its key is added to reset).
        private static NumberingSettings Complete(ParsedFile parsed, IReadOnlyList<string> categoryKeys,
            List<string> reset)
        {
            NumberingSettings defaults = ReadDefaults();
            NumberingSettings source = parsed?.Settings ?? defaults;
            var settings = new NumberingSettings
            {
                LastCategory = categoryKeys.Contains(source.LastCategory) ? source.LastCategory : defaults.LastCategory,
            };

            foreach (string key in categoryKeys)
            {
                CategorySettings category = source.Find(key);
                if (category == null)
                {
                    category = defaults.Find(key);
                    reset?.Add(key);
                }

                settings.Categories.Add(category);
            }

            return settings;
        }

        // Category records stay raw JSON, so they can be read one by one.
        private sealed class RawSettings
        {
            public string LastCategory { get; set; }

            public List<JsonElement> Categories { get; set; }
        }

        private sealed class ParsedFile
        {
            public NumberingSettings Settings { get; } = new NumberingSettings { Categories = new List<CategorySettings>() };

            // Keys of invalid records. Empty string when the key cannot be read.
            public List<string> InvalidKeys { get; } = new List<string>();
        }
    }
}
