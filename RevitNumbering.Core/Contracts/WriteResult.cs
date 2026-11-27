namespace RevitNumbering.Core.Contracts
{
    public sealed class WriteItem
    {
        public long Id { get; set; }

        public string Value { get; set; }
    }

    public sealed class WriteResult
    {
        public int WrittenCount { get; set; }

        // The user cancelled the write. Everything is rolled back, no value is written.
        public bool Cancelled { get; set; }

        public List<WriteFailure> Failures { get; set; } = new List<WriteFailure>();

        // Timings of the write for the log, in milliseconds.
        public long ChecksMs { get; set; }

        public long TransactionsMs { get; set; }

        public long ProgressMs { get; set; }

        public long FinishMs { get; set; }

        public long SlowestBatchMs { get; set; }

        public int Batches { get; set; }

        public int ElementByElementBatches { get; set; }
    }

    public sealed class WriteFailure
    {
        public long Id { get; set; }

        public WriteFailReason Reason { get; set; }

        // Text from Revit: the owner name or the failure text.
        public string Details { get; set; }
    }

    public enum WriteFailReason
    {
        Deleted,
        OwnedByOther,
        NotUpToDate,
        ReadOnly,
        Rejected,
        RolledBack,
    }
}
