using RevitNumbering.Core.Settings;

namespace RevitNumbering.Tests.Settings
{
    /// <summary>Export and import of the whole file.</summary>
    public class SettingsStoreExportImportTests : SettingsTestBase
    {
        [Fact]
        public void Export_CopiesSavedFile()
        {
            WriteText(FilePath, F1);
            string target = Path.Combine(NewDirectory(), "exp.json");

            NewStore().Export(target);

            Assert.Equal(F1, ReadText(target));
            Assert.Equal(F1, ReadText(FilePath));
        }

        [Fact]
        public void Export_WithoutFile_WritesDefaults()
        {
            string target = Path.Combine(NewDirectory(), "exp.json");

            NewStore().Export(target);

            Assert.Equal(D, ReadText(target));
            Assert.False(File.Exists(FilePath));
        }

        [Fact]
        public void ExportThenImport_GivesSameSettings()
        {
            WriteText(FilePath, F1);
            SettingsStore s1 = NewStore();
            string exchange = Path.Combine(NewDirectory(), "numbering.json");
            s1.Export(exchange);

            string dir2 = NewDirectory();
            string path2 = Path.Combine(dir2, "settings.json");
            WriteText(path2, F0);
            var s2 = new SettingsStore(path2, Log);
            s2.Load();

            SettingsLoadResult imported = s2.Import(exchange);

            Assert.Equal(SettingsLoadStatus.Ok, imported.Status);
            AssertSameSettings(s1.Load().Settings, imported.Settings);
            Assert.Equal(F1, ReadText(path2));
            Assert.Equal(F0, ReadText(s2.BackupFilePath));
            AssertSameSettings(s1.Load().Settings, s2.Load().Settings);
            Assert.False(File.Exists(path2 + ".tmp"));
        }

        [Fact]
        public void Import_WithoutOwnFile_WritesDefaultsToBackup()
        {
            string exchange = Path.Combine(NewDirectory(), "numbering.json");
            WriteText(exchange, F1);
            SettingsStore store = NewStore();

            SettingsLoadResult imported = store.Import(exchange);

            Assert.Equal(SettingsLoadStatus.Ok, imported.Status);
            Assert.Equal(F1, ReadText(FilePath));
            Assert.Equal(D, ReadText(BackupPath));
        }

        [Theory]
        [InlineData("broken", SettingsLoadStatus.Corrupted)]
        [InlineData("newer", SettingsLoadStatus.NewerSchemaReadOnly)]
        [InlineData("invalid record", SettingsLoadStatus.Corrupted)]
        [InlineData("no file", SettingsLoadStatus.Missing)]
        public void Import_Refused_ChangesNothing(string variant, SettingsLoadStatus expected)
        {
            WriteText(FilePath, F0);
            WriteText(BackupPath, "old");
            string exchange = Path.Combine(NewDirectory(), "numbering.json");
            switch (variant)
            {
                case "broken": WriteText(exchange, "{"); break;
                case "newer": WriteText(exchange, N2); break;
                case "invalid record":
                    WriteText(exchange, WithCategory(F0, "Windows", CategoryJson(rowToleranceMm: "0")));
                    break;
                case "no file": break;
                default: throw new ArgumentOutOfRangeException(nameof(variant), variant, null);
            }

            SettingsStore store = NewStore();

            SettingsLoadResult result = store.Import(exchange);

            Assert.Equal(expected, result.Status);
            Assert.Null(result.Settings);
            Assert.Equal(F0, ReadText(FilePath));
            Assert.Equal("old", ReadText(BackupPath));
            Assert.False(store.IsReadOnly);
            Assert.Equal(0, Log.ErrorCount);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Import_EmptyPath_IsMissing(string sourcePath)
        {
            SettingsLoadResult result = NewStore().Import(sourcePath);

            Assert.Equal(SettingsLoadStatus.Missing, result.Status);
            Assert.Null(result.Settings);
        }

        [Fact]
        public void Import_OverNewerSchema_ClearsReadOnly()
        {
            WriteText(FilePath, N2);
            string exchange = Path.Combine(NewDirectory(), "numbering.json");
            WriteText(exchange, F1);
            SettingsStore store = NewStore();
            store.Load();
            Assert.True(store.IsReadOnly);

            SettingsLoadResult imported = store.Import(exchange);
            store.Save("Doors", SettingsStore.Defaults(Doors), null);

            Assert.Equal(SettingsLoadStatus.Ok, imported.Status);
            Assert.Equal(N2, ReadText(BackupPath));
            Assert.False(store.IsReadOnly);
            Assert.Equal("Д-{Уровень}-{N:000}", store.Load().Settings.Categories["Doors"].Template);
            Assert.Contains("\"Template\":\"Д-{Уровень}-{N:000}\"", ReadText(FilePath), StringComparison.Ordinal);
        }

        [Fact]
        public void Export_ToMissingFolder_Throws()
        {
            string target = Path.Combine(Dir, "нет_папки", "exp.json");

            IOException error = Assert.ThrowsAny<IOException>(() => NewStore().Export(target));

            Assert.IsType<DirectoryNotFoundException>(error);
            Assert.Equal(1, Log.ErrorCount);
        }

        [Fact]
        public void Export_EmptyOrOwnPath()
        {
            WriteText(FilePath, F0);
            SettingsStore store = NewStore();

            Assert.Throws<ArgumentException>(() => store.Export(null));
            Assert.Throws<ArgumentException>(() => store.Export(""));
            store.Export(FilePath.ToUpperInvariant());

            Assert.Equal(F0, ReadText(FilePath));
            Assert.Equal(1, Log.InfoCount);
        }
    }
}
