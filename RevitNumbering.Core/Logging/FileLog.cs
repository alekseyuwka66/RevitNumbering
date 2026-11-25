using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace RevitNumbering.Core.Logging
{
    /// <summary>
    /// File log. No public member throws.
    /// </summary>
    public sealed class FileLog : ILog
    {
        private const string TimeFormat = "yyyy-MM-dd HH:mm:ss.fff";
        private const string InfoLevel = "INFO";
        private const string ErrorLevel = "ERROR";

        // One lock for all instances of the process: the host, the ribbon command and PluginEntry
        // each create their own FileLog over the same file.
        private static readonly object WriteLock = new object();

        private readonly string _revitVersion;
        private readonly Func<DateTime> _localNow;

        /// <param name="directory">Log folder.</param>
        /// <param name="revitVersion">"2022" | "2024" | "2025".</param>
        /// <param name="localNow">Local time.</param>
        public FileLog(string directory, string revitVersion, Func<DateTime> localNow)
        {
            LogDirectory = string.IsNullOrEmpty(directory) ? DefaultDirectory : directory;
            _revitVersion = revitVersion ?? string.Empty;
            _localNow = localNow ?? (() => DateTime.Now);
        }

        public static string DefaultDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    LoggingParameters.LogDirProduct,
                    LoggingParameters.LogDirName);
            }
        }

        public string LogDirectory { get; }

        /// <summary>Path of the file for the current local date (the name is computed on every access).</summary>
        public string CurrentFilePath
        {
            get
            {
                try
                {
                    return Path.Combine(LogDirectory, BuildFileName(_revitVersion, Now().Date));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    return LogDirectory;
                }
            }
        }

        public static FileLog Create(string revitVersion)
        {
            return new FileLog(DefaultDirectory, revitVersion, () => DateTime.Now);
        }

        public static string BuildFileName(string revitVersion, DateTime localDate)
        {
            return LoggingParameters.LogFilePrefix
                + revitVersion
                + "-"
                + localDate.ToString(LoggingParameters.LogFileDateFormat, CultureInfo.InvariantCulture)
                + LoggingParameters.LogFileExtension;
        }

        /// <summary>
        /// Deletes log files older than the retention period by the date in the file name.
        /// Returns the number of files actually deleted.
        /// </summary>
        public int DeleteExpiredFiles()
        {
            int deleted = 0;
            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    return 0;
                }

                DateTime today = Now().Date;
                string mask = LoggingParameters.LogFilePrefix + "*" + LoggingParameters.LogFileExtension;
                foreach (string path in Directory.GetFiles(LogDirectory, mask))
                {
                    if (!TryGetFileDate(path, out DateTime fileDate))
                    {
                        continue;
                    }

                    if ((today - fileDate).Days <= LoggingParameters.LogRetentionDays)
                    {
                        continue;
                    }

                    try
                    {
                        File.Delete(path);
                        deleted++;
                    }
                    catch (Exception ex)
                    {
                        // Busy file or no rights: skip it and go on.
                        Debug.WriteLine(ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return deleted;
        }

        public void Info(string message)
        {
            Write(InfoLevel, message, null);
        }

        public void Error(string message, Exception exception)
        {
            Write(ErrorLevel, message, exception);
        }

        private static bool TryGetFileDate(string path, out DateTime date)
        {
            date = default;
            string fileName = Path.GetFileName(path);
            if (fileName == null
                || !fileName.StartsWith(LoggingParameters.LogFilePrefix, StringComparison.OrdinalIgnoreCase)
                || !fileName.EndsWith(LoggingParameters.LogFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string name = fileName.Substring(0, fileName.Length - LoggingParameters.LogFileExtension.Length);
            int dateLength = LoggingParameters.LogFileDateFormat.Length;
            if (name.Length < dateLength)
            {
                return false;
            }

            return DateTime.TryParseExact(name.Substring(name.Length - dateLength),
                LoggingParameters.LogFileDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        private DateTime Now()
        {
            return _localNow();
        }

        private void Write(string level, string message, Exception exception)
        {
            try
            {
                DateTime now = Now();
                var entry = new StringBuilder();
                entry.Append(now.ToString(TimeFormat, CultureInfo.InvariantCulture))
                    .Append(' ').Append(level)
                    .Append(' ').Append(message ?? string.Empty)
                    .Append(Environment.NewLine);
                if (exception != null)
                {
                    entry.Append(exception).Append(Environment.NewLine);
                }

                string path = Path.Combine(LogDirectory, BuildFileName(_revitVersion, now.Date));
                lock (WriteLock)
                {
                    Directory.CreateDirectory(LogDirectory);
                    Append(path, entry.ToString());
                }
            }
            catch (Exception ex)
            {
                // Losing a log line is better than failing an entry point.
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
                catch (IOException) when (attempt < LoggingParameters.LogWriteAttempts)
                {
                    // Another Revit process of the same version is writing the file.
                    Thread.Sleep(LoggingParameters.LogWriteRetryDelayMs);
                }
            }
        }
    }
}
