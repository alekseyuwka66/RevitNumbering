using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Orderings;
using RevitNumbering.Core.Settings;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Tests.Settings
{
    public class SettingsStoreTests : IDisposable
    {
        private static readonly string[] Keys = { "Walls", "Doors", "Windows", "Rooms" };

        private const string CustomFile = @"{
  ""lastCategory"": ""Rooms"",
  ""categories"": [
    { ""categoryKey"": ""Walls"", ""prefix"": ""W-"", ""includeLevel"": false, ""levelFirst"": true, ""separator"": """", ""width"": 1,
      ""ordering"": { ""type"": ""Grid"", ""start"": ""TopLeft"", ""byColumns"": false, ""rowToleranceMm"": 700 } },
    { ""categoryKey"": ""Doors"", ""prefix"": ""D-"", ""includeLevel"": true, ""levelFirst"": true, ""separator"": ""-"", ""width"": 2,
      ""ordering"": { ""type"": ""Grid"", ""start"": ""BottomRight"", ""byColumns"": true, ""rowToleranceMm"": 250 } },
    { ""categoryKey"": ""Windows"", ""prefix"": ""O-"", ""includeLevel"": false, ""levelFirst"": true, ""separator"": """", ""width"": 1,
      ""ordering"": { ""type"": ""Grid"", ""start"": ""TopRight"", ""byColumns"": false, ""rowToleranceMm"": 500 } },
    { ""categoryKey"": ""Rooms"", ""prefix"": """", ""includeLevel"": false, ""levelFirst"": true, ""separator"": """", ""width"": 3,
      ""ordering"": { ""type"": ""Grid"", ""start"": ""TopLeft"", ""byColumns"": false, ""rowToleranceMm"": 3000 } }
  ]
}";

        // The number the settings give for level "01" and counter 7.
        private static string Sample(CategorySettings category)
        {
            return Template.From(category).Format("01", 7);
        }

        private readonly string _folder =
            Path.Combine(Path.GetTempPath(), "RevitNumberingTests", Guid.NewGuid().ToString("N"));

        private readonly SettingsStore _store;

        public SettingsStoreTests()
        {
            _store = new SettingsStore(Path.Combine(_folder, "settings.json"),
                new FileLog(Path.Combine(_folder, "Logs")));
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
            SettingsStore.CheckDefaults(Keys);

            NumberingSettings defaults = SettingsStore.ReadDefaults();

            Assert.Equal("Doors", defaults.LastCategory);
            Assert.Equal(Keys, defaults.Categories.Select(c => c.CategoryKey));
            Assert.All(defaults.Categories, category => Assert.True(category.IsValid()));
            Assert.Equal("Д-01-007", Sample(defaults.Find("Doors")));
            Assert.Equal(1000, ((GridOrdering)defaults.Find("Walls").Ordering).RowToleranceMm);
            Assert.Equal(3000, ((GridOrdering)defaults.Find("Rooms").Ordering).RowToleranceMm);
        }

        [Fact]
        public void CheckDefaults_ThrowsForCategoryWithoutRecord()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => SettingsStore.CheckDefaults(new[] { "Doors", "Stairs" }));

            Assert.EndsWith(": Stairs", exception.Message);
        }

        [Fact]
        public void NoFile_GivesDefaults_WithoutNotices()
        {
            SettingsLoadResult result = _store.Load(Keys);

            Assert.False(result.FileUnreadable);
            Assert.Empty(result.ResetCategories);
            Assert.Equal(SettingsStore.Serialize(SettingsStore.ReadDefaults()), SettingsStore.Serialize(result.Settings));
        }

        [Fact]
        public void ValidFile_IsRead()
        {
            WriteFile(CustomFile);

            SettingsLoadResult result = _store.Load(Keys);

            Assert.False(result.FileUnreadable);
            Assert.Empty(result.ResetCategories);
            Assert.Equal("Rooms", result.Settings.LastCategory);
            CategorySettings doors = result.Settings.Find("Doors");
            Assert.Equal("D-01-07", Sample(doors));
            var grid = Assert.IsType<GridOrdering>(doors.Ordering);
            Assert.Equal(Corner.BottomRight, grid.Start);
            Assert.True(grid.ByColumns);
            Assert.Equal(250, grid.RowToleranceMm);
        }

        [Fact]
        public void CorruptedFile_DefaultsAreUsed_UserIsNotified()
        {
            WriteFile("{ \"categories\": [ oops");

            SettingsLoadResult result = _store.Load(Keys);

            Assert.True(result.FileUnreadable);
            Assert.Empty(result.ResetCategories);
            Assert.Equal("Д-01-007", Sample(result.Settings.Find("Doors")));
        }

        [Fact]
        public void InvalidCategoryRecord_IsReplacedByItsDefault()
        {
            WriteFile(CustomFile.Replace("\"type\": \"Grid\", \"start\": \"BottomRight\"",
                "\"type\": \"Spiral\", \"start\": \"BottomRight\""));

            SettingsLoadResult result = _store.Load(Keys);

            Assert.False(result.FileUnreadable);
            Assert.Equal(new[] { "Doors" }, result.ResetCategories);
            Assert.Equal("Д-01-007", Sample(result.Settings.Find("Doors")));
            Assert.Equal("W-7", Sample(result.Settings.Find("Walls")));
        }

        private static void Set(CategorySettings category, string prefix)
        {
            category.Prefix = prefix;
            category.IncludeLevel = false;
            category.Width = 1;
        }

        [Fact]
        public void RecordInTheOldFormat_WithOneTemplateString_IsReplacedByItsDefault()
        {
            WriteFile(CustomFile.Replace("\"prefix\": \"D-\", \"includeLevel\": true, \"levelFirst\": true, \"separator\": \"-\", \"width\": 2,",
                "\"template\": \"D-{Level}-{N:00}\","));

            SettingsLoadResult result = _store.Load(Keys);

            Assert.Equal(new[] { "Doors" }, result.ResetCategories);
            Assert.Equal("Д-01-007", Sample(result.Settings.Find("Doors")));
        }

        [Fact]
        public void Save_WritesTheWholeFile()
        {
            NumberingSettings settings = _store.Load(Keys).Settings;
            settings.LastCategory = "Walls";
            Set(settings.Find("Rooms"), "R-");

            Assert.True(_store.Save(settings));
            Set(settings.Find("Doors"), "D-");
            Assert.True(_store.Save(settings));

            NumberingSettings loaded = _store.Load(Keys).Settings;
            Assert.Equal("Walls", loaded.LastCategory);
            Assert.Equal("R-7", Sample(loaded.Find("Rooms")));
            Assert.Equal("D-7", Sample(loaded.Find("Doors")));
        }

        [Fact]
        public void Ordering_IsWrittenWithItsType_EnumsAsStrings_AndReadBack()
        {
            NumberingSettings settings = _store.Load(Keys).Settings;
            settings.Find("Doors").Ordering =
                new GridOrdering { Start = Corner.BottomLeft, ByColumns = true, RowToleranceMm = 1234 };

            _store.Save(settings);
            string text = File.ReadAllText(_store.FilePath);
            var grid = Assert.IsType<GridOrdering>(_store.Load(Keys).Settings.Find("Doors").Ordering);

            Assert.Contains("\"type\": \"Grid\"", text);
            Assert.Contains("\"start\": \"BottomLeft\"", text);
            Assert.Contains("\"prefix\": \"Д-\"", text);
            Assert.Equal(Corner.BottomLeft, grid.Start);
            Assert.True(grid.ByColumns);
            Assert.Equal(1234, grid.RowToleranceMm);
        }
    }
}
