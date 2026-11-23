using System.Diagnostics;
using RevitNumbering.Core.Settings;

namespace RevitNumbering.Tests.Settings
{
    /// <summary>Saving one category and changed general fields, retries and the lock.</summary>
    public class SettingsStoreSaveTests : SettingsTestBase
    {
        [Fact]
        public void Save_IsReadByAnotherSession()
        {
            NewStore().Save("Doors", WithTemplate(SettingsStore.Defaults(Doors), "Д{Уровень}.{N:00}"), null);

            SettingsLoadResult result = NewStore().Load();

            Assert.Equal(SettingsLoadStatus.Ok, result.Status);
            Assert.Equal("Д{Уровень}.{N:00}", result.Settings.Categories["Doors"].Template);
        }

        [Fact]
        public void Save_DoorsDoNotOverwriteRooms()
        {
            SettingsStore store = NewStore();
            store.Save("Doors", WithTemplate(SettingsStore.Defaults(Doors), "Д1-{N}"), null);
            store.Save("Rooms", WithTemplate(SettingsStore.Defaults(Rooms), "П{N:00}"), null);

            SettingsLoadResult result = store.Load();

            Assert.Equal(new[] { "Doors", "Rooms" }, result.Settings.Categories.Keys.ToArray());
            Assert.Equal("Д1-{N}", result.Settings.Categories["Doors"].Template);
            Assert.Equal("П{N:00}", result.Settings.Categories["Rooms"].Template);
            Assert.Equal(3000, result.Settings.Categories["Rooms"].RowToleranceMm);
        }

        [Fact]
        public void Save_TwoInstances_KeepBothChanges()
        {
            WriteText(FilePath, F0);
            SettingsStore a = NewStore();
            SettingsStore b = NewStore();
            a.Load();
            b.Load();

            CategorySettings doorsA = SettingsStore.Defaults(Doors);
            doorsA.Template = "Д{Уровень}.{N:00}";
            doorsA.ConflictStrategy = "AllowDuplicates";
            a.Save("Doors", doorsA, new GeneralSettingsChange
            {
                Traversal = new TraversalSettings
                {
                    StrategyKey = "grid",
                    Parameters = new Dictionary<string, string> { { "corner", "BottomLeft" }, { "direction", "Rows" } }
                }
            });
            b.Save("Rooms", WithTemplate(SettingsStore.Defaults(Rooms), "{Уровень}-{N:00}"), new GeneralSettingsChange
            {
                LastCategoryKey = "Rooms",
                Window = new WindowPlacement { Left = 200, Top = 150, Width = 1100, Height = 700 }
            });

            Assert.Equal(F1, ReadText(FilePath));
        }

        [Fact]
        public void Save_NullGeneralFields_KeepFileValues()
        {
            WriteText(FilePath, F0);
            SettingsStore store = NewStore();

            store.Save("Doors", SettingsStore.Defaults(Doors), new GeneralSettingsChange
            {
                Window = new WindowPlacement { Left = 200, Top = 150, Width = 1100, Height = 700 }
            });

            NumberingSettings saved = store.Load().Settings;
            AssertDefaultTraversal(saved.Traversal);
            Assert.Equal("Doors", saved.LastCategoryKey);
            AssertWindow(saved.Window, 200, 150, 1100, 700);
            Assert.Equal(F0.Replace("\"Left\":100,\"Top\":100", "\"Left\":200,\"Top\":150"), ReadText(FilePath));
        }

        [Fact]
        public void Save_GeneralFieldsOnly()
        {
            WriteText(FilePath, F0);

            NewStore().Save(null, null, new GeneralSettingsChange { LastCategoryKey = "Rooms" });

            Assert.Equal(F0.Replace("\"LastCategoryKey\":\"Doors\"", "\"LastCategoryKey\":\"Rooms\""),
                ReadText(FilePath));
        }

        [Theory]
        [InlineData("category null")]
        [InlineData("key null")]
        [InlineData("key blank")]
        [InlineData("tolerance 0")]
        [InlineData("template null")]
        [InlineData("strategy Skip")]
        [InlineData("window width 0")]
        [InlineData("last category empty")]
        [InlineData("traversal key empty")]
        public void Save_InvalidArguments_ThrowAndLeaveFile(string variant)
        {
            WriteText(FilePath, F0);
            SettingsStore store = NewStore();
            string key = "Doors";
            CategorySettings category = SettingsStore.Defaults(Doors);
            GeneralSettingsChange general = null;
            switch (variant)
            {
                case "category null": category = null; break;
                case "key null": key = null; break;
                case "key blank": key = " "; break;
                case "tolerance 0": category.RowToleranceMm = 0; break;
                case "template null": category.Template = null; break;
                case "strategy Skip": category.ConflictStrategy = "Skip"; break;
                case "window width 0":
                    general = new GeneralSettingsChange
                    {
                        Window = new WindowPlacement { Left = 0, Top = 0, Width = 0, Height = 700 }
                    };
                    break;
                case "last category empty":
                    general = new GeneralSettingsChange { LastCategoryKey = "" };
                    break;
                case "traversal key empty":
                    general = new GeneralSettingsChange { Traversal = new TraversalSettings { StrategyKey = "" } };
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(variant), variant, null);
            }

            Assert.Throws<ArgumentException>(() => store.Save(key, category, general));

            Assert.Equal(F0, ReadText(FilePath));
            Assert.False(File.Exists(TempPath));
        }

        [Fact]
        public void Save_FileCorruptedAfterLoad_MovesToBadAndWritesDefaults()
        {
            WriteText(FilePath, F0);
            SettingsStore store = NewStore();
            store.Load();
            WriteText(FilePath, "{");

            store.Save("Doors", SettingsStore.Defaults(Doors), null);

            Assert.Equal("{", ReadText(BadPath));
            Assert.Equal(D.Replace("\"Categories\":{}", "\"Categories\":{\"Doors\":" + DoorsRecord + "}"),
                ReadText(FilePath));
        }

        [Fact]
        public void Save_NewerSchemaAppearedAfterLoad_SwitchesToReadOnly()
        {
            WriteText(FilePath, F0);
            SettingsStore store = NewStore();
            Assert.Equal(SettingsLoadStatus.Ok, store.Load().Status);
            WriteText(FilePath, N2);

            store.Save("Doors", SettingsStore.Defaults(Doors), null);

            Assert.Equal(N2, ReadText(FilePath));
            Assert.True(store.IsReadOnly);
            Assert.False(File.Exists(BadPath));
            Assert.False(File.Exists(TempPath));
        }

        [Fact]
        public void Save_KeepsForeignAndInvalidRecords()
        {
            string windowsRecord = CategoryJson(rowToleranceMm: "0");
            WriteText(FilePath, WithCategory(WithCategory(F0, "Equipment", DoorsRecord), "Windows", windowsRecord));
            SettingsStore store = NewStore();

            store.Save("Doors", WithTemplate(SettingsStore.Defaults(Doors), "X{N}"), null);

            string expected = WithCategory(WithCategory(F0.Replace("Д-{Уровень}-{N:000}", "X{N}"), "Equipment",
                DoorsRecord), "Windows", windowsRecord);
            Assert.Equal(expected, ReadText(FilePath));
            NumberingSettings loaded = store.Load().Settings;
            Assert.Equal(new[] { "Doors", "Equipment" }, loaded.Categories.Keys.ToArray());
            Assert.Equal("X{N}", loaded.Categories["Doors"].Template);
        }

        [Fact]
        public void Save_FileBusyAllTheTime_ThrowsAfterRetries()
        {
            WriteText(FilePath, F0);
            SettingsStore store = NewStore();
            var watch = new Stopwatch();

            using (new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                watch.Start();
                Assert.ThrowsAny<IOException>(() => store.Save("Doors", SettingsStore.Defaults(Doors), null));
                watch.Stop();
            }

            // 3 retries with a pause of 100 ms; a small allowance for the timer resolution.
            Assert.True(watch.ElapsedMilliseconds >= 290, "elapsed " + watch.ElapsedMilliseconds + " ms");
            Assert.Equal(1, Log.ErrorCount);
            Assert.False(File.Exists(TempPath));
            Assert.Equal(F0, ReadText(FilePath));
        }

        [Fact]
        public async Task Save_FileReleasedDuringRetries_Succeeds()
        {
            WriteText(FilePath, F0);
            SettingsStore store = NewStore();
            var busy = new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Task release = Task.Delay(150).ContinueWith(_ => busy.Dispose());

            store.Save("Doors", WithTemplate(SettingsStore.Defaults(Doors), "R{N}"), null);
            await release;

            Assert.Equal(0, Log.ErrorCount);
            Assert.Equal("R{N}", store.Load().Settings.Categories["Doors"].Template);
        }

        [Fact]
        public async Task Save_ConcurrentInstances_LoseNothing()
        {
            WriteText(FilePath, F0);
            SettingsStore a = NewStore();
            SettingsStore b = NewStore();

            Task saveDoors = Task.Run(() =>
            {
                for (int i = 0; i < 20; i++)
                {
                    a.Save("Doors", WithTemplate(SettingsStore.Defaults(Doors), "A{N}" + i), null);
                }
            });
            Task saveRooms = Task.Run(() =>
            {
                for (int i = 0; i < 20; i++)
                {
                    b.Save("Rooms", WithTemplate(SettingsStore.Defaults(Rooms), "B{N}" + i), null);
                }
            });
            await Task.WhenAll(saveDoors, saveRooms);

            NumberingSettings saved = NewStore().Load().Settings;
            Assert.Equal("A{N}19", saved.Categories["Doors"].Template);
            Assert.Equal("B{N}19", saved.Categories["Rooms"].Template);
        }

        [Fact]
        public void Save_WritesUtf8WithoutBomAndLeavesNoTempFile()
        {
            NewStore().Save("Doors", SettingsStore.Defaults(Doors), null);

            Assert.Equal(0x7B, File.ReadAllBytes(FilePath)[0]);
            Assert.False(File.Exists(TempPath));
        }

        [Fact]
        public void Save_ProducesReferenceBytes()
        {
            SettingsStore store = NewStore();
            var rooms = new CategorySettings
            {
                Template = "{Уровень}{N:00}",
                LevelCodeSource = "LevelParameter",
                LevelParameterName = "Код/этажа \"А\"",
                LevelCodePart = "WordN",
                LevelWordNumber = 2,
                RowToleranceMm = 3000,
                ConflictStrategy = "AllowDuplicates",
                AppendMode = true,
                Traversal = null
            };

            store.Save("Doors", SettingsStore.Defaults(Doors), new GeneralSettingsChange
            {
                Window = new WindowPlacement { Left = 100, Top = 100.5, Width = 1100, Height = 700 }
            });
            store.Save("Rooms", rooms, null);

            Assert.Equal(Bytes(G), File.ReadAllBytes(FilePath));
        }

        [Fact]
        public void Load_ReadsReferenceFile()
        {
            WriteText(FilePath, G);

            SettingsLoadResult result = NewStore().Load();

            Assert.Equal(SettingsLoadStatus.Ok, result.Status);
            NumberingSettings settings = result.Settings;
            AssertDefaultTraversal(settings.Traversal);
            Assert.Equal("Doors", settings.LastCategoryKey);
            AssertWindow(settings.Window, 100, 100.5, 1100, 700);
            Assert.Equal(new[] { "Doors", "Rooms" }, settings.Categories.Keys.ToArray());
            AssertCategory(settings.Categories["Doors"], "Д-{Уровень}-{N:000}", "LevelName", "", "Whole", 1, 500,
                "SkipOccupied", false);
            AssertCategory(settings.Categories["Rooms"], "{Уровень}{N:00}", "LevelParameter", "Код/этажа \"А\"",
                "WordN", 2, 3000, "AllowDuplicates", true);
        }
    }
}
