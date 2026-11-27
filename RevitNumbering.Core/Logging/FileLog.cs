using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace RevitNumbering.Core.Logging
{
    // The plugin log, in English: one file per day, "numbering-yyyy-MM-dd.log". A message starts with
    // its source in brackets, like "[Apply] written 42". No public member throws:
    // losing a log line is better than breaking an entry point.
    public sealed class FileLog
    {
        private const string FilePrefix = "numbering-";
        private const string FileExtension = ".log";
        private const string DateFormat = "yyyy-MM-dd";
        private const string TimeFormat = "yyyy-MM-dd HH:mm:ss.fff";
        private const int RetentionDays = 14;
        private const int WriteAttempts = 3;
        private const int WriteRetryDelayMs = 20;

        // Static: all FileLog objects write to the same files.
        private static readonly object WriteLock = new object();

        private readonly string _directory;
        private readonly Func<DateTime> _now;

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

        // Deletes log files older than the retention period. The age comes from the date in the file name.
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
                    // Another Revit process may be writing the same file. Try again.
                    Thread.Sleep(WriteRetryDelayMs);
                }
            }
        }
    }
}
