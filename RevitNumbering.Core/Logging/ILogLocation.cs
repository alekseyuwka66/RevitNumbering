namespace RevitNumbering.Core.Logging
{
    /// <summary>
    /// Path of the current log file; SafeRunner needs it for the unexpected-error text.
    /// </summary>
    public interface ILogLocation
    {
        string CurrentFilePath { get; }
    }
}
