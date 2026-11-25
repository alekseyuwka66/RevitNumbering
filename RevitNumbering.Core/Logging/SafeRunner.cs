using System.Diagnostics;

namespace RevitNumbering.Core.Logging
{
    /// <summary>
    /// Single exception trap for every entry point. Neither method throws.
    /// How to tell the user is decided by the caller through onError.
    /// </summary>
    public static class SafeRunner
    {
        public static void Run(ILog log, string operation, Action action, Action<Exception> onError)
        {
            try
            {
                action();
            }
            catch (OperationCanceledException)
            {
                log?.Info("[" + operation + "] cancelled");
            }
            catch (Exception ex)
            {
                OnFailed(log, operation, ex, onError);
            }
        }

        /// <summary>The returned task always runs to completion.</summary>
        public static async Task RunAsync(ILog log, string operation, Func<Task> action, Action<Exception> onError)
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException)
            {
                log?.Info("[" + operation + "] cancelled");
            }
            catch (Exception ex)
            {
                OnFailed(log, operation, ex, onError);
            }
        }

        private static void OnFailed(ILog log, string operation, Exception exception, Action<Exception> onError)
        {
            log?.Error("[" + operation + "] unhandled exception", exception);
            try
            {
                onError?.Invoke(exception);
            }
            catch (Exception ex)
            {
                // A failure while reporting must not reach Revit either.
                log?.Error("[" + operation + "] error handler failed", ex);
                Debug.WriteLine(ex);
            }
        }
    }
}
