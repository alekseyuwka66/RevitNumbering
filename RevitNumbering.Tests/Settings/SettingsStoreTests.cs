using RevitNumbering.Core.Model;
using RevitNumbering.Core.Orderings;
using RevitNumbering.Core.Settings;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Tests.Settings
{
    public class SettingsStoreTests : IDisposable
    {
        private static readonly string[] Keys = { "Walls", "Doors", "Windows", "Rooms" };

        private const string CustomFile = @"{
  ""schemaVersion"": 1,
  ""lastCategory"": ""Rooms"",
  ""window"": { ""left"": 10, ""top"": 20, ""width"": 900, ""height"": 600 },
  ""categories"": [
    { ""categoryKey"": ""Walls"", ""template"": ""W-{N}"",
      ""levelCode"": { ""part"": ""Whole"", ""wordNumber"": 1 },
      ""ordering"": { ""type"": ""Grid"", ""start"": ""TopLeft"", ""byColumns"": false, ""rowToleranceMm"": 700 } },
    { ""categoryKey"": ""Doors"", ""template"": ""D-{Level}-{N:00}"",
      ""levelCode"": { ""part"": ""LastWord"", ""wordNumber"": 1 },
      ""ordering"": { ""type"": ""Grid"", ""start"": ""BottomRight"", ""byColumns"": true, ""rowToleranceMm"": 250 } },
    { ""categoryKey"": ""Windows"", ""template"": ""O-{N}"",
      ""levelCode"": { ""part"": ""Word"", ""wordNumber"": 2 },
      ""ordering"": { ""type"": ""Grid"", ""start"": ""TopRight"", ""byColumns"": false, ""rowToleranceMm"": 500 } },
    { ""categoryKey"": ""Rooms"", ""template"": ""{N:000}"",
      ""levelCode"": { ""part"": ""Whole"", ""wordNumber"": 1 },
      ""ordering"": { ""type"": ""Grid"", ""start"": ""TopLeft"", ""byColumns"": false, ""rowToleranceMm"": 3000 } }
  ]
}";

        private readonly string _folder =
            Path.Combine(Path.GetTempPath(), "RevitNumberingTests", Guid.NewGuid().ToString("N"));

        private readonly FakeLog _log = new FakeLog();
        private readonly SettingsStore _store;

        public SettingsStoreTests()
        {
            _store = new SettingsStore(Path.Combine(_folder, "settings.json"), _log);
        }

        public void Dispose()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }

        private void WriteFile(string text)
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(_store.FilePath, text);
        }

        [Fact]
        public void EmbeddedDefaults_AreReadable_AndEveryRecordIsValid()
        {
            Assert.Empty(SettingsStore.CheckDefaults(Keys));

            NumberingSettings defaults = SettingsStore.ReadDefaults();

            Assert.Equal("Doors", defaults.LastCategory);
            Assert.Equal(Keys, defaults.Categories.Select(c => c.CategoryKey));
            Assert.All(defaults.Categories, category => Assert.True(category.IsValid()));
            Assert.Equal("Д-{Уровень}-{N:000}", defaults.Find("Doors").Template);
            Assert.Equal(1000, ((GridOrdering)defaults.Find("Walls").Ordering).RowToleranceMm);
            Assert.Equal(3000, ((GridOrdering)defaults.Find("Rooms").Ordering).RowToleranceMm);
        }

        [Fact]
        public void CheckDefaults_ReportsCategoryWithoutRecord()
        {
            Problem problem = Assert.Single(SettingsStore.CheckDefaults(new[] { "Doors", "Stairs" }));

            Assert.Equal(ProblemCode.DefaultsCategoryMissing, problem.Code);
            Assert.Equal(new[] { "Stairs" }, problem.Args);
        }

        [Fact]
        public void NoFile_GivesDefaults_WithoutNotices()
        {
            SettingsLoadResult result = _store.Load(Keys);

            Assert.Empty(result.Notices);
            Assert.Equal(SettingsStore.Serialize(SettingsStore.ReadDefaults()), SettingsStore.Serialize(result.Settings));
        }

        [Fact]
        public void ValidFile_IsRead()
        {
            WriteFile(CustomFile);

            SettingsLoadResult result = _store.Load(Keys);

            Assert.Empty(result.Notices);
            Assert.Equal("Rooms", result.Settings.LastCategory);
            Assert.Equal(900, result.Settings.Window.Width);
            CategorySettings doors = result.Settings.Find("Doors");
            Assert.Equal("D-{Level}-{N:00}", doors.Template);
            Assert.Equal(LevelCodePart.LastWord, doors.LevelCode.Part);
            var grid = Assert.IsType<GridOrdering>(doors.Ordering);
            Assert.Equal(Corner.BottomRight, grid.Start);
            Assert.True(grid.ByColumns);
            Assert.Equal(250, grid.RowToleranceMm);
            Assert.Equal(2, result.Settings.Find("Windows").LevelCode.WordNumber);
        }

        [Fact]
        public void CorruptedFile_IsCopiedToBad_DefaultsAreUsed_UserIsNotified()
        {
            WriteFile("{ \"categories\": [ oops");

            SettingsLoadResult result = _store.Load(Keys);

            SettingsNotice notice = Assert.Single(result.Notices);
            Assert.Equal(SettingsNoticeKind.FileCorrupted, notice.Kind);
            Assert.True(File.Exists(_store.BadFilePath));
            Assert.Equal("Д-{Уровень}-{N:000}", result.Settings.Find("Doors").Template);
            Assert.Single(_log.Warnings);
        }

        [Fact]
        public void InvalidCategoryRecord_IsReplacedByItsDefault()
        {
            WriteFile(CustomFile.Replace("\"type\": \"Grid\", \"start\": \"BottomRight\"",
                "\"type\": \"Spiral\", \"start\": \"BottomRight\""));

            SettingsLoadResult result = _store.Load(Keys);

            SettingsNotice notice = Assert.Single(result.Notices);
            Assert.Equal(SettingsNoticeKind.CategoriesReset, notice.Kind);
            Assert.Equal(new[] { "Doors" }, notice.Args);
            Assert.Equal("Д-{Уровень}-{N:000}", result.Settings.Find("Doors").Template);
            Assert.Equal("W-{N}", result.Settings.Find("Walls").Template);
            Assert.False(File.Exists(_store.BadFilePath));
        }

        [Fact]
        public void Save_WritesTheWholeFile_ThroughTemporaryFile()
        {
            NumberingSettings settings = _store.Load(Keys).Settings;
            settings.LastCategory = "Walls";
            settings.Find("Rooms").Template = "R-{N}";

            Assert.True(_store.Save(settings));
            settings.Find("Doors").Template = "D-{N}";
            Assert.True(_store.Save(settings));

            Assert.Equal(SettingsStore.Serialize(settings), File.ReadAllText(_store.FilePath));
            Assert.Equal(new[] { _store.FilePath }, Directory.GetFiles(_folder));
            NumberingSettings loaded = _store.Load(Keys).Settings;
            Assert.Equal("Walls", loaded.LastCategory);
            Assert.Equal("R-{N}", loaded.Find("Rooms").Template);
            Assert.Equal("D-{N}", loaded.Find("Doors").Template);
        }

        [Fact]
        public void Ordering_IsWrittenWithItsType_EnumsAsStrings_AndReadBack()
        {
            NumberingSettings settings = _store.Load(Keys).Settings;
            CategorySettings doors = settings.Find("Doors");
            doors.Ordering = new GridOrdering { Start = Corner.BottomLeft, ByColumns = true, RowToleranceMm = 1234 };
            doors.LevelCode = new LevelCode { Part = LevelCodePart.Word, WordNumber = 3 };

            _store.Save(settings);
            string text = File.ReadAllText(_store.FilePath);
            var grid = Assert.IsType<GridOrdering>(_store.Load(Keys).Settings.Find("Doors").Ordering);

            Assert.Contains("\"type\": \"Grid\"", text);
            Assert.Contains("\"start\": \"BottomLeft\"", text);
            Assert.Contains("\"part\": \"Word\"", text);
            Assert.Contains("\"template\": \"Д-{Уровень}-{N:000}\"", text);
            Assert.Equal(Corner.BottomLeft, grid.Start);
            Assert.True(grid.ByColumns);
            Assert.Equal(1234, grid.RowToleranceMm);
        }

        [Fact]
        public void NewerSchema_IsRead_ButNeverOverwritten()
        {
            string newer = CustomFile.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99");
            WriteFile(newer);

            SettingsLoadResult result = _store.Load(Keys);

            Assert.Equal(SettingsNoticeKind.NewerSchema, Assert.Single(result.Notices).Kind);
            Assert.Equal("W-{N}", result.Settings.Find("Walls").Template);
            Assert.False(_store.Save(result.Settings));
            Assert.Equal(newer, File.ReadAllText(_store.FilePath));
        }

        [Fact]
        public void InvalidImport_ChangesNothing()
        {
            _store.Save(_store.Load(Keys).Settings);
            string before = File.ReadAllText(_store.FilePath);
            string invalidRecord = Path.Combine(_folder, "invalid.json");
            File.WriteAllText(invalidRecord, CustomFile.Replace("\"template\": \"W-{N}\"", "\"template\": \"W-{X}\""));
            string garbage = Path.Combine(_folder, "garbage.json");
            File.WriteAllText(garbage, "not json");

            Assert.Equal(ImportResult.Invalid, _store.Import(invalidRecord, Keys, out NumberingSettings first));
            Assert.Equal(ImportResult.Invalid, _store.Import(garbage, Keys, out NumberingSettings second));
            Assert.Equal(ImportResult.Unreadable,
                _store.Import(Path.Combine(_folder, "missing.json"), Keys, out NumberingSettings third));

            Assert.Null(first);
            Assert.Null(second);
            Assert.Null(third);
            Assert.Equal(before, File.ReadAllText(_store.FilePath));
            Assert.False(File.Exists(_store.BackupFilePath));
        }

        [Fact]
        public void ValidImport_ReplacesTheFile_AndKeepsThePreviousOneAsBackup()
        {
            _store.Save(_store.Load(Keys).Settings);
            string before = File.ReadAllText(_store.FilePath);
            string source = Path.Combine(_folder, "import.json");
            File.WriteAllText(source, CustomFile);

            ImportResult result = _store.Import(source, Keys, out NumberingSettings imported);

            Assert.Equal(ImportResult.Imported, result);
            Assert.Equal("D-{Level}-{N:00}", imported.Find("Doors").Template);
            Assert.Equal(before, File.ReadAllText(_store.BackupFilePath));
            Assert.Equal("D-{Level}-{N:00}", _store.Load(Keys).Settings.Find("Doors").Template);
        }
    }
}
