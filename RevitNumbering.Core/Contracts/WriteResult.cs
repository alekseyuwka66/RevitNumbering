namespace RevitNumbering.Core.Contracts
{
    /// <summary>A value to write into the target parameter of one element.</summary>
    public sealed class WriteItem
    {
        public long Id { get; set; }

        public string Value { get; set; }
    }

    public sealed class WriteResult
    {
        public int WrittenCount { get; set; }

        public List<WriteFailure> Failures { get; set; } = new List<WriteFailure>();
    }

    public sealed class WriteFailure
    {
        public long Id { get; set; }

        public WriteFailReason Reason { get; set; }

        /// <summary>Details from Revit: the owner name or the failure text.</summary>
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
