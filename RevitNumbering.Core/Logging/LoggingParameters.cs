namespace RevitNumbering.Core.Logging
{
    /// <summary>
    /// Parameters of the log.
    /// </summary>
    public static class LoggingParameters
    {
        /// <summary>Folder under %LOCALAPPDATA%.</summary>
        public const string LogDirProduct = "RevitNumbering";

        /// <summary>Subfolder of the product folder.</summary>
        public const string LogDirName = "Logs";

        /// <summary>File name prefix before the Revit version.</summary>
        public const string LogFilePrefix = "numbering-R";

        /// <summary>Date format in the file name.</summary>
        public const string LogFileDateFormat = "yyyy-MM-dd";

        /// <summary>File extension.</summary>
        public const string LogFileExtension = ".log";

        public const int LogRetentionDays = 14;

        /// <summary>Number of attempts to open the file.</summary>
        public const int LogWriteAttempts = 3;

        /// <summary>Pause between attempts, ms.</summary>
        public const int LogWriteRetryDelayMs = 20;
    }
}
