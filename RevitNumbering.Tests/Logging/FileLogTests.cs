using System.Globalization;
using System.Text;
using RevitNumbering.Core.Logging;

namespace RevitNumbering.Tests.Logging
{
    public sealed class FileLogTests : IDisposable
    {
        private static readonly DateTime Today = new DateTime(2026, 11, 23, 10, 0, 0);

        private static readonly string[] SampleFiles =
        {
            "numbering-R2024-2026-11-23.log",
            "numbering-R2024-2026-11-09.log",
            "numbering-R2022-2026-11-08.log",
            "numbering-R2025-2025-12-31.log",
            "numbering-R2024-2026-12-01.log",
            "numbering-R2024-latest.log",
            "notes.txt"
        };

        private readonly string _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
            else if (File.Exists(_directory))
            {
                File.Delete(_directory);
            }
        }

        [Fact]
        public void BuildFileName_UsesInvariantGregorianDate()
        {
            Assert.Equal("numbering-R2024-2026-11-23.log", FileLog.BuildFileName("2024", new DateTime(2026, 11, 23)));

            CultureInfo saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("th-TH");
                Assert.Equal("numbering-R2022-2026-01-05.log",
                    FileLog.BuildFileName("2022", new DateTime(2026, 1, 5)));
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        [Fact]
        public void DefaultDirectory_IsLocalAppDataRevitNumberingLogs()
        {
            string expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RevitNumbering", "Logs");

            Assert.Equal(expected, FileLog.DefaultDirectory);
            Assert.Equal(expected, FileLog.Create("2024").LogDirectory);
        }

        [Fact]
        public void DeleteExpiredFiles_DeletesByDateInName()
        {
            CreateSampleFiles();
            File.SetLastWriteTime(Path.Combine(_directory, "numbering-R2024-2026-11-09.log"), new DateTime(2020, 1, 1));
            File.SetLastWriteTime(Path.Combine(_directory, "numbering-R2022-2026-11-08.log"), new DateTime(2026, 11, 23));
            var log = new FileLog(_directory, "2024", () => Today);

            int deleted = log.DeleteExpiredFiles();

            Assert.Equal(2, deleted);
            string[] expectedLeft = SampleFiles
                .Except(new[] { "numbering-R2022-2026-11-08.log", "numbering-R2025-2025-12-31.log" })
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(expectedLeft, FileNames());
        }

        [Fact]
        public void DeleteExpiredFiles_NoDirectory_ReturnsZero()
        {
            var log = new FileLog(_directory, "2024", () => Today);

            Assert.Equal(0, log.DeleteExpiredFiles());
            Assert.False(Directory.Exists(_directory));
        }

        [Fact]
        public void DeleteExpiredFiles_BusyFile_IsSkipped()
        {
            CreateSampleFiles();
            var log = new FileLog(_directory, "2024", () => Today);
            string busy = Path.Combine(_directory, "numbering-R2025-2025-12-31.log");

            int deleted;
            using (new FileStream(busy, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                deleted = log.DeleteExpiredFiles();
            }

            Assert.Equal(1, deleted);
            Assert.False(File.Exists(Path.Combine(_directory, "numbering-R2022-2026-11-08.log")));
            Assert.True(File.Exists(busy));
        }

        [Fact]
        public void Midnight_NextRecordGoesToNewFile()
        {
            DateTime now = new DateTime(2026, 11, 23, 23, 59, 59);
            var log = new FileLog(_directory, "2024", () => now);

            log.Info("a");
            Assert.Equal(Path.Combine(_directory, "numbering-R2024-2026-11-23.log"), log.CurrentFilePath);
            now = new DateTime(2026, 11, 24, 0, 0, 1);
            log.Info("b");

            string first = File.ReadAllText(Path.Combine(_directory, "numbering-R2024-2026-11-23.log"));
            string second = File.ReadAllText(Path.Combine(_directory, "numbering-R2024-2026-11-24.log"));
            Assert.Contains(" INFO a", first, StringComparison.Ordinal);
            Assert.DoesNotContain(" INFO b", first, StringComparison.Ordinal);
            Assert.Contains(" INFO b", second, StringComparison.Ordinal);
            Assert.StartsWith("2026-11-24 00:00:01.000 INFO b", second, StringComparison.Ordinal);
        }

        [Fact]
        public void ParallelWrites_NoLostOrTornLines()
        {
            const int threadCount = 8;
            const int perThread = 500;
            var log = new FileLog(_directory, "2024", () => Today);

            RunThreads(threadCount, thread =>
            {
                for (int i = 0; i < perThread; i++)
                {
                    log.Info("t" + thread.ToString(CultureInfo.InvariantCulture) + "-"
                        + i.ToString(CultureInfo.InvariantCulture));
                }
            });

            AssertAllLinesWritten(log.CurrentFilePath, threadCount, perThread);
        }

        [Fact]
        public void TwoInstances_NoLostOrTornLines()
        {
            const int perThread = 1000;
            var logs = new[]
            {
                new FileLog(_directory, "2024", () => Today),
                new FileLog(_directory, "2024", () => Today)
            };

            RunThreads(2, thread =>
            {
                for (int i = 0; i < perThread; i++)
                {
                    logs[thread].Info("t" + thread.ToString(CultureInfo.InvariantCulture) + "-"
                        + i.ToString(CultureInfo.InvariantCulture));
                }
            });

            AssertAllLinesWritten(logs[0].CurrentFilePath, 2, perThread);
        }

        [Fact]
        public void Error_WritesExceptionDetailsInUtf8WithoutBom()
        {
            var log = new FileLog(_directory, "2024", () => Today);
            Exception caught = Assert.Throws<InvalidOperationException>(TestThrower.ThrowerForTest);

            log.Error("op failed", caught);
            log.Info("Марка Д-1-001 ё");

            byte[] bytes = File.ReadAllBytes(log.CurrentFilePath);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            string text = new UTF8Encoding(false, true).GetString(bytes);
            Assert.Contains(" ERROR op failed", text, StringComparison.Ordinal);
            Assert.Contains("InvalidOperationException", text, StringComparison.Ordinal);
            Assert.Contains("ThrowerForTest", text, StringComparison.Ordinal);
            Assert.Contains("inner-secret", text, StringComparison.Ordinal);
            Assert.Contains(" INFO Марка Д-1-001 ё", text, StringComparison.Ordinal);
        }

        [Fact]
        public void WriteImpossible_DirectoryIsFile_NoException()
        {
            File.WriteAllText(_directory, "not a directory");
            var log = new FileLog(_directory, "2024", () => Today);

            log.Info("lost");
            log.Error("lost", null);
            log.Info(null);

            Assert.Equal(0, log.DeleteExpiredFiles());
            Assert.Equal("not a directory", File.ReadAllText(_directory));
        }

        [Fact]
        public void WriteImpossible_FileBusy_LineLostThenWritten()
        {
            var log = new FileLog(_directory, "2024", () => Today);
            log.Info("first");

            using (new FileStream(log.CurrentFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                log.Info("lost");
                log.Error("lost", null);
            }

            log.Info("after");

            string text = File.ReadAllText(log.CurrentFilePath);
            Assert.Contains(" INFO first", text, StringComparison.Ordinal);
            Assert.DoesNotContain("lost", text, StringComparison.Ordinal);
            Assert.Contains(" INFO after", text, StringComparison.Ordinal);
        }

        [Fact]
        public void Error_NullMessageAndNullException_WritesSingleLine()
        {
            var log = new FileLog(_directory, "2025", () => Today);

            log.Error(null, null);

            string[] lines = File.ReadAllLines(log.CurrentFilePath);
            Assert.Equal(new[] { "2026-11-23 10:00:00.000 ERROR " }, lines);
            Assert.Equal("numbering-R2025-2026-11-23.log", Path.GetFileName(log.CurrentFilePath));
        }

        private static void RunThreads(int count, Action<int> body)
        {
            var failures = new List<Exception>();
            var threads = new List<Thread>();
            for (int t = 0; t < count; t++)
            {
                int index = t;
                var thread = new Thread(() =>
                {
                    try
                    {
                        body(index);
                    }
                    catch (Exception ex)
                    {
                        lock (failures)
                        {
                            failures.Add(ex);
                        }
                    }
                });
                threads.Add(thread);
                thread.Start();
            }

            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            Assert.Empty(failures);
        }

        private static void AssertAllLinesWritten(string path, int threadCount, int perThread)
        {
            string[] lines = File.ReadAllLines(path);
            Assert.Equal(threadCount * perThread, lines.Length);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in lines)
            {
                int marker = line.IndexOf(" INFO ", StringComparison.Ordinal);
                Assert.True(marker > 0, "torn line: " + line);
                Assert.True(seen.Add(line.Substring(marker + " INFO ".Length)), "duplicate line: " + line);
            }

            for (int t = 0; t < threadCount; t++)
            {
                for (int i = 0; i < perThread; i++)
                {
                    Assert.Contains("t" + t.ToString(CultureInfo.InvariantCulture) + "-"
                        + i.ToString(CultureInfo.InvariantCulture), seen);
                }
            }
        }

        private void CreateSampleFiles()
        {
            Directory.CreateDirectory(_directory);
            foreach (string name in SampleFiles)
            {
                File.WriteAllText(Path.Combine(_directory, name), "x");
            }
        }

        private string[] FileNames()
        {
            return Directory.GetFiles(_directory)
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
