namespace RevitNumbering.Core.Writing
{
    /// <summary>
    /// Retry decision after a rollback: made in the core, not in the adapter.
    /// </summary>
    public static class WriteRetryPolicy
    {
        /// <summary>Items of the next attempt. Empty: nothing to retry.</summary>
        public static IReadOnlyList<WriteItem> NextAttempt(IReadOnlyList<WriteItem> attempted,
            IReadOnlyCollection<long> failedIds, int attemptNumber)
        {
            throw new NotImplementedException();
        }
    }
}
