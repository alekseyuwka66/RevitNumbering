using RevitNumbering.Core.Common;

namespace RevitNumbering.Core.Writing
{
    public enum WriteFailReason
    {
        OwnedByOtherUser,
        NotUpToDate,
        RolledBackByWarning,
        Deleted
    }

    public sealed class WriteFailure
    {
        public WriteFailure(long id, WriteFailReason reason, string detail)
        {
            Id = id;
            Reason = reason;
            Detail = detail;
        }

        /// <summary>0: failure without an element.</summary>
        public long Id { get; }

        public WriteFailReason Reason { get; }

        /// <summary>Owner name, Revit warning text.</summary>
        public string Detail { get; }
    }

    public enum WriteOutcome
    {
        Committed,
        NothingToWrite,
        DocumentMismatch,
        Stale,
        Failed,
        Cancelled
    }

    public sealed class WrittenValue
    {
        public WrittenValue(long id, string actualValue)
        {
            Id = id;
            ActualValue = actualValue;
        }

        public long Id { get; }

        /// <summary>Value after commit.</summary>
        public string ActualValue { get; }
    }

    public sealed class WriteReport
    {
        public WriteReport(WriteOutcome outcome, IReadOnlyList<WrittenValue> written,
            IReadOnlyList<WriteFailure> failures, int attempts)
        {
            Outcome = outcome;
            Written = DtoCollections.CopyList(written);
            Failures = DtoCollections.CopyList(failures);
            Attempts = attempts;
        }

        public WriteOutcome Outcome { get; }

        public IReadOnlyList<WrittenValue> Written { get; }

        public IReadOnlyList<WriteFailure> Failures { get; }

        public int Attempts { get; }
    }
}
