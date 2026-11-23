using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;

namespace RevitNumbering.Core.Writing
{
    /// <summary>An element fixed before the write.</summary>
    public sealed class FixedElement
    {
        public FixedElement(long id, FixReason reason, string ownerName)
        {
            Id = id;
            Reason = reason;
            OwnerName = ownerName;
        }

        public long Id { get; }

        public FixReason Reason { get; }

        public string OwnerName { get; }
    }

    /// <summary>
    /// The single final summary of Apply.
    /// </summary>
    public sealed class ApplySummary
    {
        public ApplySummary(int changedCount, IReadOnlyList<FixedElement> fixedBeforeWrite,
            IReadOnlyList<WriteFailure> failures, IReadOnlyList<DuplicateValue> remainingDuplicates)
        {
            ChangedCount = changedCount;
            FixedBeforeWrite = DtoCollections.CopyList(fixedBeforeWrite);
            Failures = DtoCollections.CopyList(failures);
            RemainingDuplicates = DtoCollections.CopyList(remainingDuplicates);
        }

        public int ChangedCount { get; }

        /// <summary>Shown in the summary.</summary>
        public IReadOnlyList<FixedElement> FixedBeforeWrite { get; }

        public IReadOnlyList<WriteFailure> Failures { get; }

        /// <summary>By re-read data.</summary>
        public IReadOnlyList<DuplicateValue> RemainingDuplicates { get; }

        /// <summary>reread == null until the re-read is finished or when the document has changed.</summary>
        public static ApplySummary Build(NumberingPlan plan, WriteReport report, CategorySnapshot reread)
        {
            throw new NotImplementedException();
        }
    }
}
