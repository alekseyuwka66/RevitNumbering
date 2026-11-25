using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace RevitNumbering.Core.Logging
{
    /// <summary>
    /// File log: one file per day, "numbering-yyyy-MM-dd.log" in the given folder.
    /// No public member throws: losing a log line is better than failing an entry point.
    /// </summary>
    public sealed class FileLog : ILog
    {
        private const string FilePrefix = "numbering-";
        private const string FileExtension = ".log";
        private const string DateFormat = "yyyy-MM-dd";
        private const string TimeFormat = "yyyy-MM-dd HH:mm:ss.fff";
        private const int RetentionDays = 14;
        private const int WriteAttempts = 3;
        private const int WriteRetryDelayMs = 20;

        private static readonly object WriteLock = new object();

        private readonly string _directory;
        private readonly Func<DateTime> _now;

        /// <param name="directory">Log folder; created on the first write.</param>
        /// <param name="now">Local time; null: the system clock.</param>
        public FileLog(string directory, Func<DateTime> now = null)
        {
            _directory = directory;
            _now = now ?? (() => DateTime.Now);
        }

        public string FilePath => Path.Combine(_directory, FileName(_now().Date));

        public void Info(string message)
        {
            Write("INFO", message, null);
        }

        public void Warning(string message)
        {
            Write("WARNING", message, null);
        }

        public void Error(string message, Exception exception)
        {
            Write("ERROR", message, exception);
        }

        /// <summary>Deletes log files older than the retention period, by the date in the file name.</summary>
        public void DeleteOldFiles()
        {
            try
            {
                if (!Directory.Exists(_directory))
                {
                    return;
                }

                DateTime today = _now().Date;
                foreach (string path in Directory.GetFiles(_directory, FilePrefix + "*" + FileExtension))
                {
                    string date = Path.GetFileNameWithoutExtension(path).Substring(FilePrefix.Length);
                    if (DateTime.TryParseExact(date, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None,
                            out DateTime fileDate)
                        && (today - fileDate).Days > RetentionDays)
                    {
                        TryDelete(path);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private static string FileName(DateTime date)
        {
            return FilePrefix + date.ToString(DateFormat, CultureInfo.InvariantCulture) + FileExtension;
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                // Busy file or no rights: skip it and go on.
                Debug.WriteLine(ex);
            }
        }

        private void Write(string level, string message, Exception exception)
        {
            try
            {
                DateTime now = _now();
                var entry = new StringBuilder();
                entry.Append(now.ToString(TimeFormat, CultureInfo.InvariantCulture))
                    .Append(' ').Append(level)
                    .Append(' ').Append(message ?? string.Empty)
                    .Append(Environment.NewLine);
                if (exception != null)
                {
                    entry.Append(exception).Append(Environment.NewLine);
                }

                lock (WriteLock)
                {
                    Directory.CreateDirectory(_directory);
                    Append(Path.Combine(_directory, FileName(now.Date)), entry.ToString());
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private static void Append(string path, string text)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.Write(text);
                    }

                    return;
                }
                catch (IOException) when (attempt < WriteAttempts)
                {
                    // Another Revit process of the same version is writing the file.
                    Thread.Sleep(WriteRetryDelayMs);
                }
            }
        }
    }
}
