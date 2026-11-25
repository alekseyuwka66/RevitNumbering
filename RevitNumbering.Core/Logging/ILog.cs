namespace RevitNumbering.Core.Logging
{
    /// <summary>
    /// The log of the plugin, in English. No member throws.
    /// A message starts with its source in brackets: "[Apply] written 42".
    /// </summary>
    public interface ILog
    {
        /// <summary>Path of the current log file.</summary>
        string FilePath { get; }

        /// <summary>The normal course of work.</summary>
        void Info(string message);

        /// <summary>Expected problems: settings were not read, an element was not written.</summary>
        void Warning(string message);

        /// <summary>Unexpected exceptions, with the stack.</summary>
        void Error(string message, Exception exception);
    }
}
