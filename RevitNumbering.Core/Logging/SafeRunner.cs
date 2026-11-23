using System.Diagnostics;
using RevitNumbering.Core.Texts;

namespace RevitNumbering.Core.Logging
{
    /// <summary>
    /// Single exception trap for every entry point. Neither method throws; the user gets
    /// the ready unexpected-error text without exception details.
    /// </summary>
    public static class SafeRunner
    {
        private const string UnknownOperation = "unknown";

        public static void Run(ILog log, string operation, Action action, Action<string> showError)
        {
            if (action == null)
            {
                return;
            }

            try
            {
                action();
            }
            catch (OperationCanceledException)
            {
                OnCancelled(log, operation);
            }
            catch (Exception ex)
            {
                OnFailed(log, operation, ex, showError);
            }
        }

        /// <summary>
        /// The returned task always runs to completion. The continuation and showError run in the
        /// caller's synchronization context (the window thread).
        /// </summary>
        public static Task RunAsync(ILog log, string operation, Func<Task> action, Action<string> showError)
        {
            if (action == null)
            {
                return Task.CompletedTask;
            }

            return RunCoreAsync(log, operation, action, showError);
        }

        private static async Task RunCoreAsync(ILog log, string operation, Func<Task> action,
            Action<string> showError)
        {
            try
            {
                Task task = action();
                if (task != null)
                {
                    await task;
                }
            }
            catch (OperationCanceledException)
            {
                OnCancelled(log, operation);
            }
            catch (Exception ex)
            {
                OnFailed(log, operation, ex, showError);
            }
        }

        /// <summary>Cancellation is an expected outcome, not an error.</summary>
        private static void OnCancelled(ILog log, string operation)
        {
            try
            {
                log?.Info(Name(operation) + ": cancelled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private static void OnFailed(ILog log, string operation, Exception exception, Action<string> showError)
        {
            string name = Name(operation);
            TryLogError(log, name + ": unhandled exception", exception);

            try
            {
                if (showError != null)
                {
                    showError(TextCatalog.Format(TextCatalog.ErrUnexpected, LogPath(log)));
                }
            }
            catch (Exception showException)
            {
                TryLogError(log, name + ": showError failed", showException);
            }
        }

        private static void TryLogError(ILog log, string message, Exception exception)
        {
            try
            {
                log?.Error(message, exception);
            }
            catch (Exception ex)
            {
                // A log failure must not prevent showing the message.
                Debug.WriteLine(ex);
            }
        }

        /// <summary>Current log file, otherwise the log folder.</summary>
        private static string LogPath(ILog log)
        {
            string path = null;
            try
            {
                path = (log as ILogLocation)?.CurrentFilePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return string.IsNullOrEmpty(path) ? FileLog.DefaultDirectory : path;
        }

        private static string Name(string operation)
        {
            return operation ?? UnknownOperation;
        }
    }
}
