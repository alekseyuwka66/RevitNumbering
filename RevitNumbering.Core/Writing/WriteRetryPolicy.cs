namespace RevitNumbering.Core.Writing
{
    /// <summary>
    /// Retry decision after a rollback: made in the core, not in the adapter.
    /// </summary>
    public static class WriteRetryPolicy
    {
        /// <summary>
        /// Items of the next attempt; attemptNumber is the number of the rolled back attempt, from 1.
        /// Empty: nothing to retry — no failed item among the attempt, the attempt limit
        /// is reached or every item failed.
        /// </summary>
        public static IReadOnlyList<WriteItem> NextAttempt(IReadOnlyList<WriteItem> attempted,
            IReadOnlyCollection<long> failedIds, int attemptNumber)
        {
            if (attemptNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(attemptNumber));
            }

            if (attempted == null || failedIds == null)
            {
                return Array.Empty<WriteItem>();
            }

            var failed = new HashSet<long>(failedIds);
            if (!attempted.Any(item => failed.Contains(item.Id)))
            {
                return Array.Empty<WriteItem>();
            }

            if (attemptNumber >= WritingParameters.WriteMaxAttempts)
            {
                return Array.Empty<WriteItem>();
            }

            return attempted.Where(item => !failed.Contains(item.Id)).ToArray();
        }
    }
}
