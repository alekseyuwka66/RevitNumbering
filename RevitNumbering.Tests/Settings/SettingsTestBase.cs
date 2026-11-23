using System.Text;
using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Settings;

namespace RevitNumbering.Tests.Settings
{
    /// <summary>Fake log that counts calls.</summary>
    internal sealed class CountingLog : ILog
    {
        private int _infoCount;
        private int _errorCount;

        public int InfoCount => _infoCount;

        public int ErrorCount => _errorCount;

        public void Info(string message)
        {
            Interlocked.Increment(ref _infoCount);
        }

        public void Error(string message, Exception exception)
        {
            Interlocked.Increment(ref _errorCount);
        }
    }

    /// <summary>
    /// Common part of the settings tests: a new temporary folder per test (removed afterwards), the fake log,
    /// test categories and the reference files.
    /// </summary>
    public abstract class SettingsTestBase : IDisposable
    {
        protected const string DoorsRecord =
            "{\"Template\":\"Д-{Уровень}-{N:000}\",\"LevelCodeSource\":\"LevelName\",\"LevelParameterName\":\"\","
            + "\"LevelCodePart\":\"Whole\",\"LevelWordNumber\":1,\"RowToleranceMm\":500,"
            + "\"ConflictStrategy\":\"SkipOccupied\",\"AppendMode\":false,\"Traversal\":null}";

        protected const string F0 =
            "{\"SchemaVersion\":1,\"Traversal\":{\"StrategyKey\":\"grid\",\"Parameters\":{\"corner\":\"TopLeft\","
            + "\"direction\":\"Rows\"}},\"LastCategoryKey\":\"Doors\",\"Window\":{\"Left\":100,\"Top\":100,"
            + "\"Width\":1100,\"Height\":700},\"Categories\":{\"Doors\":" + DoorsRecord + "}}";

        protected const string D =
            "{\"SchemaVersion\":1,\"Traversal\":{\"StrategyKey\":\"grid\",\"Parameters\":{\"corner\":\"TopLeft\","
            + "\"direction\":\"Rows\"}},\"LastCategoryKey\":\"Doors\",\"Window\":null,\"Categories\":{}}";

        protected const string F1 =
            "{\"SchemaVersion\":1,\"Traversal\":{\"StrategyKey\":\"grid\",\"Parameters\":{\"corner\":\"BottomLeft\","
            + "\"direction\":\"Rows\"}},\"LastCategoryKey\":\"Rooms\",\"Window\":{\"Left\":200,\"Top\":150,"
            + "\"Width\":1100,\"Height\":700},\"Categories\":{\"Doors\":{\"Template\":\"Д{Уровень}.{N:00}\","
            + "\"LevelCodeSource\":\"LevelName\",\"LevelParameterName\":\"\",\"LevelCodePart\":\"Whole\","
            + "\"LevelWordNumber\":1,\"RowToleranceMm\":500,\"ConflictStrategy\":\"AllowDuplicates\","
            + "\"AppendMode\":false,\"Traversal\":null},\"Rooms\":{\"Template\":\"{Уровень}-{N:00}\","
            + "\"LevelCodeSource\":\"LevelName\",\"LevelParameterName\":\"\",\"LevelCodePart\":\"Whole\","
            + "\"LevelWordNumber\":1,\"RowToleranceMm\":3000,\"ConflictStrategy\":\"SkipOccupied\","
            + "\"AppendMode\":false,\"Traversal\":null}}}";

        protected const string N2 =
            "{\"SchemaVersion\":2,\"LastCategoryKey\":\"Rooms\",\"Categories\":{\"Rooms\":{\"Template\":"
            + "\"П-{Уровень}-{N:00}\",\"LevelCodeSource\":\"LevelName\",\"LevelParameterName\":\"\","
            + "\"LevelCodePart\":\"Whole\",\"LevelWordNumber\":1,\"RowToleranceMm\":3000,"
            + "\"ConflictStrategy\":\"SkipOccupied\",\"AppendMode\":false,\"Traversal\":{\"StrategyKey\":\"grid\","
            + "\"Parameters\":{\"corner\":\"TopRight\",\"direction\":\"Columns\"}}}},\"Profiles\":[]}";

        protected const string G =
            "{\"SchemaVersion\":1,\"Traversal\":{\"StrategyKey\":\"grid\",\"Parameters\":{\"corner\":\"TopLeft\","
            + "\"direction\":\"Rows\"}},\"LastCategoryKey\":\"Doors\",\"Window\":{\"Left\":100,\"Top\":100.5,"
            + "\"Width\":1100,\"Height\":700},\"Categories\":{\"Doors\":" + DoorsRecord + ",\"Rooms\":{"
            + "\"Template\":\"{Уровень}{N:00}\",\"LevelCodeSource\":\"LevelParameter\","
            + "\"LevelParameterName\":\"Код\\/этажа \\\"А\\\"\",\"LevelCodePart\":\"WordN\",\"LevelWordNumber\":2,"
            + "\"RowToleranceMm\":3000,\"ConflictStrategy\":\"AllowDuplicates\",\"AppendMode\":true,"
            + "\"Traversal\":null}}}";

        protected static readonly CategoryInfo Doors =
            new CategoryInfo("Doors", "Двери", "Д-{Уровень}-{N:000}", 500, ConflictScope.Document, false);

        protected static readonly CategoryInfo Rooms =
            new CategoryInfo("Rooms", "Помещения", "{Уровень}{N:00}", 3000, ConflictScope.Phase, true);

        protected static readonly CategoryInfo Windows =
            new CategoryInfo("Windows", "Окна", "О-{Уровень}-{N:000}", 500, ConflictScope.Document, false);

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly List<string> _directories = new List<string>();

        protected SettingsTestBase()
        {
            Dir = NewDirectory();
            FilePath = Path.Combine(Dir, "settings.json");
            Log = new CountingLog();
        }

        protected string Dir { get; }

        protected string FilePath { get; }

        internal CountingLog Log { get; }

        protected string BadPath => FilePath + ".bad";

        protected string BackupPath => FilePath + ".bak";

        protected string TempPath => FilePath + ".tmp";

        public void Dispose()
        {
            foreach (string directory in _directories)
            {
                try
                {
                    if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, true);
                    }
                }
                catch (IOException)
                {
                    // A leftover temporary folder must not fail the test.
                }
            }
        }

        protected string NewDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), "RevitNumbering.Tests." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            _directories.Add(directory);
            return directory;
        }

        protected SettingsStore NewStore()
        {
            return new SettingsStore(FilePath, Log);
        }

        protected static byte[] Bytes(string text)
        {
            return Utf8NoBom.GetBytes(text);
        }

        protected static void WriteText(string path, string text)
        {
            File.WriteAllBytes(path, Bytes(text));
        }

        protected static string ReadText(string path)
        {
            return Utf8NoBom.GetString(File.ReadAllBytes(path));
        }

        /// <summary>Category record as JSON; every argument is a ready JSON token.</summary>
        protected static string CategoryJson(string template = "\"О-{Уровень}-{N:000}\"",
            string levelCodeSource = "\"LevelName\"", string levelCodePart = "\"Whole\"",
            string levelWordNumber = "1", string rowToleranceMm = "500",
            string conflictStrategy = "\"SkipOccupied\"")
        {
            return "{\"Template\":" + template + ",\"LevelCodeSource\":" + levelCodeSource
                + ",\"LevelParameterName\":\"\",\"LevelCodePart\":" + levelCodePart
                + ",\"LevelWordNumber\":" + levelWordNumber + ",\"RowToleranceMm\":" + rowToleranceMm
                + ",\"ConflictStrategy\":" + conflictStrategy + ",\"AppendMode\":false,\"Traversal\":null}";
        }

        /// <summary>Appends a category record to the end of "Categories" of a settings file text.</summary>
        protected static string WithCategory(string file, string key, string recordJson)
        {
            Assert.EndsWith("}}}", file, StringComparison.Ordinal);
            return file.Substring(0, file.Length - 2) + ",\"" + key + "\":" + recordJson + "}}";
        }

        protected static CategorySettings WithTemplate(CategorySettings settings, string template)
        {
            settings.Template = template;
            return settings;
        }

        protected static void AssertGeneralDefaults(NumberingSettings settings)
        {
            Assert.NotNull(settings);
            Assert.Equal(1, settings.SchemaVersion);
            AssertDefaultTraversal(settings.Traversal);
            Assert.Equal("Doors", settings.LastCategoryKey);
            Assert.Null(settings.Window);
            Assert.NotNull(settings.Categories);
            Assert.Empty(settings.Categories);
        }

        protected static void AssertDefaultTraversal(TraversalSettings traversal)
        {
            Assert.NotNull(traversal);
            Assert.Equal("grid", traversal.StrategyKey);
            Assert.Equal(new[] { "corner", "direction" }, traversal.Parameters.Keys.ToArray());
            Assert.Equal("TopLeft", traversal.Parameters["corner"]);
            Assert.Equal("Rows", traversal.Parameters["direction"]);
        }

        protected static void AssertCategory(CategorySettings actual, string template, string levelCodeSource,
            string levelParameterName, string levelCodePart, int levelWordNumber, double rowToleranceMm,
            string conflictStrategy, bool appendMode)
        {
            Assert.NotNull(actual);
            Assert.Equal(template, actual.Template);
            Assert.Equal(levelCodeSource, actual.LevelCodeSource);
            Assert.Equal(levelParameterName, actual.LevelParameterName);
            Assert.Equal(levelCodePart, actual.LevelCodePart);
            Assert.Equal(levelWordNumber, actual.LevelWordNumber);
            Assert.Equal(rowToleranceMm, actual.RowToleranceMm);
            Assert.Equal(conflictStrategy, actual.ConflictStrategy);
            Assert.Equal(appendMode, actual.AppendMode);
            Assert.Null(actual.Traversal);
        }

        /// <summary>Values of the reference file F0.</summary>
        protected static void AssertF0(NumberingSettings settings)
        {
            Assert.Equal(1, settings.SchemaVersion);
            AssertDefaultTraversal(settings.Traversal);
            Assert.Equal("Doors", settings.LastCategoryKey);
            AssertWindow(settings.Window, 100, 100, 1100, 700);
            Assert.Equal(new[] { "Doors" }, settings.Categories.Keys.ToArray());
            AssertCategory(settings.Categories["Doors"], "Д-{Уровень}-{N:000}", "LevelName", "", "Whole", 1, 500,
                "SkipOccupied", false);
        }

        protected static void AssertWindow(WindowPlacement window, double left, double top, double width,
            double height)
        {
            Assert.NotNull(window);
            Assert.Equal(left, window.Left);
            Assert.Equal(top, window.Top);
            Assert.Equal(width, window.Width);
            Assert.Equal(height, window.Height);
        }

        /// <summary>Equality of settings by all fields.</summary>
        protected static void AssertSameSettings(NumberingSettings expected, NumberingSettings actual)
        {
            Assert.Equal(SettingsJson.Serialize(expected), SettingsJson.Serialize(actual));
        }
    }
}
