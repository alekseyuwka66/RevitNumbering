using System.Runtime.CompilerServices;
using RevitNumbering.Core.Logging;

namespace RevitNumbering.Tests.Logging
{
    /// <summary>
    /// Log fake that remembers the calls.
    /// </summary>
    internal sealed class FakeLog : ILog, ILogLocation
    {
        public const string FilePath = @"C:\Logs\numbering-R2024-2026-11-23.log";

        public List<string> Infos { get; } = new List<string>();

        public List<KeyValuePair<string, Exception>> Errors { get; } = new List<KeyValuePair<string, Exception>>();

        public string CurrentFilePath => FilePath;

        public void Info(string message)
        {
            Infos.Add(message);
        }

        public void Error(string message, Exception exception)
        {
            Errors.Add(new KeyValuePair<string, Exception>(message, exception));
        }
    }

    /// <summary>Log whose Error always fails.</summary>
    internal sealed class BrokenLog : ILog, ILogLocation
    {
        public string CurrentFilePath => FakeLog.FilePath;

        public void Info(string message)
        {
            throw new IOException("log is broken");
        }

        public void Error(string message, Exception exception)
        {
            throw new IOException("log is broken");
        }
    }

    internal static class TestThrower
    {
        /// <summary>Throws "boom" with the inner "inner-secret"; the method name must appear in the stack.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowerForTest()
        {
            throw new InvalidOperationException("boom", new ArgumentException("inner-secret"));
        }
    }
}
