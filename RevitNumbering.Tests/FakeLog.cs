using RevitNumbering.Core.Logging;

namespace RevitNumbering.Tests
{
    /// <summary>Collects log lines in memory.</summary>
    internal sealed class FakeLog : ILog
    {
        public List<string> Warnings { get; } = new List<string>();

        public List<string> Errors { get; } = new List<string>();

        public string FilePath => string.Empty;

        public void Info(string message)
        {
        }

        public void Warning(string message)
        {
            Warnings.Add(message);
        }

        public void Error(string message, Exception exception)
        {
            Errors.Add(message);
        }
    }
}
