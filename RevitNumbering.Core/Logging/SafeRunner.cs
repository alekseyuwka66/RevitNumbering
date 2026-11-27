using System.Diagnostics;

namespace RevitNumbering.Core.Logging
{
    // One place that catches exceptions for every entry point: window commands, Revit event handlers
    // and calls in the API context. It never throws. The caller decides how to tell the user (onError).
    // Pass a named method, not a long lambda: breakpoints and call stacks stay readable.
    public static class SafeRunner
    {
        public static void Run(FileLog log, string operation, Action action, Action<Exception> onError = null)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                log.Error("[" + operation + "] unhandled exception", ex);
                try
                {
                    onError?.Invoke(ex);
                }
                catch (Exception reportFailure)
                {
                    // A failure while reporting must not reach Revit either.
                    log.Error("[" + operation + "] error handler failed", reportFailure);
                    Debug.WriteLine(reportFailure);
                }
            }
        }
    }
}
