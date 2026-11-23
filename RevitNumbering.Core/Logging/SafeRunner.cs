namespace RevitNumbering.Core.Logging
{
    /// <summary>
    /// Single exception trap for every entry point.
    /// </summary>
    public static class SafeRunner
    {
        public static void Run(ILog log, string operation, Action action, Action<string> showError)
        {
            throw new NotImplementedException();
        }

        public static Task RunAsync(ILog log, string operation, Func<Task> action, Action<string> showError)
        {
            throw new NotImplementedException();
        }
    }
}
