using RevitNumbering.Core.Model;
using RevitNumbering.Core.Settings;

namespace RevitNumbering.Tests.Settings
{
    /// <summary>Defaults, reading, corrupted files, newer schema, normalization.</summary>
    public class SettingsStoreLoadTests : SettingsTestBase
    {
        [Fact]
        public void Defaults_OfDoors()
        {
            CategorySettings defaults = SettingsStore.Defaults(Doors);

            AssertCategory(defaults, "Д-{Уровень}-{N:000}", "LevelName", "", "Whole", 1, 500, "SkipOccupied", false);
            Assert.NotSame(defaults, SettingsStore.Defaults(Doors));
        }

        [Fact]
        public void Defaults_DoNotBranchOnCategoryKey()
        {
            var equipment = new CategoryInfo("Equipment", "Оборудование", "EQ-{N}", 750, ConflictScope.Document,
                false);

            AssertCategory(SettingsStore.Defaults(equipment), "EQ-{N}", "LevelName", "", "Whole", 1, 750,
                "SkipOccupied", false);
            Assert.Throws<ArgumentNullException>(() => SettingsStore.Defaults(null));
        }

        [Fact]
        public void Load_NoFile_GivesDefaultsAndCreatesNothing()
        {
            SettingsStore store = NewStore();

            SettingsLoadResult result = store.Load();

            Assert.Equal(SettingsLoadStatus.Missing, result.Status);
            AssertGeneralDefaults(result.Settings);
            Assert.False(store.IsReadOnly);
            Assert.Empty(Directory.GetFileSystemEntries(Dir));
        }

        [Fact]
        public void Load_CorruptedFile_GivesDefaultsAndMovesFileToBad()
        {
            const string broken = "{\"SchemaVersion\":1,\"Categories\":{";
            WriteText(FilePath, broken);
            SettingsStore store = NewStore();

            SettingsLoadResult result = store.Load();

            Assert.Equal(SettingsLoadStatus.Corrupted, result.Status);
            AssertGeneralDefaults(result.Settings);
            Assert.Equal(33, File.ReadAllBytes(BadPath).Length);
            Assert.Equal(Bytes(broken), File.ReadAllBytes(BadPath));
            Assert.False(File.Exists(FilePath));
            Assert.Equal(1, Log.ErrorCount);
            Assert.False(store.IsReadOnly);
        }

        [Theory]
        [InlineData("")]
        [InlineData("null")]
        [InlineData("{}")]
        [InlineData("[]")]
        [InlineData("{\"SchemaVersion\":\"x\"}")]
        [InlineData("{\"SchemaVersion\":0}")]
        [InlineData("{\"SchemaVersion\":1,\"Categories\":{\"Doors\":{},\"Doors\":{}}}")]
        public void Load_CorruptionVariants(string content)
        {
            WriteText(FilePath, content);

            SettingsLoadResult result = NewStore().Load();

            Assert.Equal(SettingsLoadStatus.Corrupted, result.Status);
            AssertGeneralDefaults(result.Settings);
            Assert.Equal(Bytes(content), File.ReadAllBytes(BadPath));
            Assert.False(File.Exists(FilePath));
        }

        [Fact]
        public void Load_OverwritesPreviousBadFile()
        {
            WriteText(BadPath, "old");
            WriteText(FilePath, "{");

            NewStore().Load();

            Assert.Equal("{", ReadText(BadPath));
        }

        [Fact]
        public void Load_AfterCorrupted_GivesMissing()
        {
            WriteText(FilePath, "{\"SchemaVersion\":1,\"Categories\":{");
            SettingsStore store = NewStore();
            Assert.Equal(SettingsLoadStatus.Corrupted, store.Load().Status);

            SettingsLoadResult second = store.Load();

            Assert.Equal(SettingsLoadStatus.Missing, second.Status);
            Assert.Equal(1, Log.ErrorCount);
        }

        [Fact]
        public void NewerSchema_IsReadOnlyAndSaveWritesNothing()
        {
            WriteText(FilePath, N2);
            SettingsStore store = NewStore();

            SettingsLoadResult result = store.Load();
            store.Save("Doors", SettingsStore.Defaults(Doors), new GeneralSettingsChange { LastCategoryKey = "Doors" });

            Assert.Equal(SettingsLoadStatus.NewerSchemaReadOnly, result.Status);
            Assert.True(store.IsReadOnly);
            Assert.Equal(2, result.Settings.SchemaVersion);
            Assert.Equal("Rooms", result.Settings.LastCategoryKey);
            AssertDefaultTraversal(result.Settings.Traversal);
            Assert.Null(result.Settings.Window);
            Assert.Equal(new[] { "Rooms" }, result.Settings.Categories.Keys.ToArray());
            AssertCategory(result.Settings.Categories["Rooms"], "П-{Уровень}-{N:00}", "LevelName", "", "Whole", 1,
                3000, "SkipOccupied", false);
            Assert.Equal(N2, ReadText(FilePath));
            Assert.False(File.Exists(BadPath));
            Assert.False(File.Exists(TempPath));
        }

        [Fact]
        public void NewerSchema_UnparseableBody_GivesDefaultsWithoutBad()
        {
            const string content = "{\"SchemaVersion\":2,\"Categories\":{\"Doors\":{\"RowToleranceMm\":\"abc\"}}}";
            WriteText(FilePath, content);
            SettingsStore store = NewStore();

            SettingsLoadResult result = store.Load();

            Assert.Equal(SettingsLoadStatus.NewerSchemaReadOnly, result.Status);
            Assert.True(store.IsReadOnly);
            AssertGeneralDefaults(result.Settings);
            Assert.False(File.Exists(BadPath));
            Assert.Equal(content, ReadText(FilePath));
        }

        [Theory]
        [InlineData("tolerance 0")]
        [InlineData("tolerance 100001")]
        [InlineData("tolerance 0.5")]
        [InlineData("tolerance NaN")]
        [InlineData("part wordn")]
        [InlineData("source levelname")]
        [InlineData("strategy Skip")]
        [InlineData("word 0")]
        [InlineData("template null")]
        [InlineData("record null")]
        public void Load_InvalidRecord_IsDroppedFileStaysOk(string variant)
        {
            string content = WithCategory(F0, "Windows", InvalidWindowsRecord(variant));
            WriteText(FilePath, content);

            SettingsLoadResult result = NewStore().Load();

            Assert.Equal(SettingsLoadStatus.Ok, result.Status);
            Assert.Equal(new[] { "Doors" }, result.Settings.Categories.Keys.ToArray());
            Assert.True(Log.InfoCount >= 1);
            Assert.Equal(0, Log.ErrorCount);
            Assert.Equal(content, ReadText(FilePath));
        }

        [Theory]
        [InlineData("1", 1)]
        [InlineData("100000", 100000)]
        public void Load_ToleranceBounds_AreAllowed(string toleranceJson, double expected)
        {
            WriteText(FilePath, F0.Replace("\"RowToleranceMm\":500", "\"RowToleranceMm\":" + toleranceJson));

            SettingsLoadResult result = NewStore().Load();

            Assert.Equal(SettingsLoadStatus.Ok, result.Status);
            Assert.Equal(expected, result.Settings.Categories["Doors"].RowToleranceMm);
        }

        [Fact]
        public void Load_NormalizesGeneralFields()
        {
            WriteText(FilePath, "{\"SchemaVersion\":1,\"Traversal\":null,\"LastCategoryKey\":\"\","
                + "\"Window\":{\"Left\":0,\"Top\":0,\"Width\":0,\"Height\":700},\"Categories\":null}");

            SettingsLoadResult result = NewStore().Load();

            Assert.Equal(SettingsLoadStatus.Ok, result.Status);
            AssertGeneralDefaults(result.Settings);
        }

        [Fact]
        public void Load_NormalizesCategoryFields()
        {
            string content = F0
                .Replace("\"LevelParameterName\":\"\"", "\"LevelParameterName\":null")
                .Replace("\"AppendMode\":false,\"Traversal\":null",
                    "\"AppendMode\":false,\"Traversal\":{\"StrategyKey\":\"grid\",\"Parameters\":{}}");
            Assert.NotEqual(F0, content);
            WriteText(FilePath, content);

            SettingsLoadResult result = NewStore().Load();

            Assert.Equal(SettingsLoadStatus.Ok, result.Status);
            AssertF0(result.Settings);
            Assert.Equal(content, ReadText(FilePath));
        }

        [Fact]
        public void Load_StripsBom()
        {
            byte[] bom = { 0xEF, 0xBB, 0xBF };
            File.WriteAllBytes(FilePath, bom.Concat(Bytes(F0)).ToArray());

            SettingsLoadResult result = NewStore().Load();

            Assert.Equal(SettingsLoadStatus.Ok, result.Status);
            AssertF0(result.Settings);
        }

        [Fact]
        public void Load_ReturnsIndependentCopies()
        {
            WriteText(FilePath, F0);
            SettingsStore store = NewStore();

            store.Load().Settings.Categories["Doors"].Template = "X";

            Assert.Equal("Д-{Уровень}-{N:000}", store.Load().Settings.Categories["Doors"].Template);
        }

        [Fact]
        public void Constructor_ChecksArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new SettingsStore(null, Log));
            Assert.Throws<ArgumentException>(() => new SettingsStore("settings.json", Log));
            Assert.Throws<ArgumentException>(() => new SettingsStore("", Log));
            Assert.Throws<ArgumentNullException>(() => new SettingsStore(FilePath, null));
            Assert.Empty(Directory.GetFileSystemEntries(Dir));
        }

        [Fact]
        public void BadAndBackupPaths_AndLoadResult()
        {
            SettingsStore store = NewStore();

            Assert.Equal(FilePath + ".bad", store.BadFilePath);
            Assert.Equal(FilePath + ".bak", store.BackupFilePath);
            Assert.Empty(Directory.GetFileSystemEntries(Dir));

            var settings = new NumberingSettings();
            var missing = new SettingsLoadResult(null, SettingsLoadStatus.Missing);
            var ok = new SettingsLoadResult(settings, SettingsLoadStatus.Ok);
            Assert.Null(missing.Settings);
            Assert.Equal(SettingsLoadStatus.Missing, missing.Status);
            Assert.Same(settings, ok.Settings);
            Assert.Equal(SettingsLoadStatus.Ok, ok.Status);
        }

        internal static string InvalidWindowsRecord(string variant)
        {
            switch (variant)
            {
                case "tolerance 0": return CategoryJson(rowToleranceMm: "0");
                case "tolerance 100001": return CategoryJson(rowToleranceMm: "100001");
                case "tolerance 0.5": return CategoryJson(rowToleranceMm: "0.5");
                case "tolerance NaN": return CategoryJson(rowToleranceMm: "\"NaN\"");
                case "part wordn": return CategoryJson(levelCodePart: "\"wordn\"");
                case "source levelname": return CategoryJson(levelCodeSource: "\"levelname\"");
                case "strategy Skip": return CategoryJson(conflictStrategy: "\"Skip\"");
                case "word 0": return CategoryJson(levelWordNumber: "0");
                case "template null": return CategoryJson(template: "null");
                case "record null": return "null";
                default: throw new ArgumentOutOfRangeException(nameof(variant), variant, null);
            }
        }
    }
}
