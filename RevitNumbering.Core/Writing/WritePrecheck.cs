using RevitNumbering.Core.Domain.Numbering;

namespace RevitNumbering.Core.Writing
{
    /// <summary>State of an element relative to the central model (worksharing).</summary>
    public enum CentralState
    {
        NotWorkshared,
        CurrentWithCentral,
        NotYetInCentral,
        UpdatedInCentral,
        DeletedInCentral,
        Unknown
    }

    /// <summary>
    /// Facts about a plan element at the moment of writing. The adapter collects them,
    /// the core decides.
    /// </summary>
    public sealed class ElementWriteFacts
    {
        public ElementWriteFacts(long id, bool exists, bool isOwnedByOtherUser, string ownerName,
            CentralState centralState, bool isParameterWritable, string currentValue)
        {
            Id = id;
            Exists = exists;
            IsOwnedByOtherUser = isOwnedByOtherUser;
            OwnerName = ownerName;
            CentralState = centralState;
            IsParameterWritable = isParameterWritable;
            CurrentValue = currentValue ?? string.Empty;
        }

        public long Id { get; }

        public bool Exists { get; }

        public bool IsOwnedByOtherUser { get; }

        public string OwnerName { get; }

        public CentralState CentralState { get; }

        public bool IsParameterWritable { get; }

        public string CurrentValue { get; }
    }

    /// <summary>
    /// Check of one element before the transaction: deleted, owned, out of date.
    /// </summary>
    public static class WritePrecheck
    {
        /// <summary>null: write; otherwise the refusal. The first matching rule wins.</summary>
        public static WriteFailure Check(WriteItem item, ElementWriteFacts facts)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            if (facts.Id != item.Id)
            {
                throw new ArgumentException("Facts of element " + facts.Id + " were given for element "
                    + item.Id + ".", nameof(facts));
            }

            if (!facts.Exists || facts.CentralState == CentralState.DeletedInCentral)
            {
                return new WriteFailure(item.Id, WriteFailReason.Deleted, string.Empty);
            }

            if (facts.IsOwnedByOtherUser)
            {
                return new WriteFailure(item.Id, WriteFailReason.OwnedByOtherUser, facts.OwnerName ?? string.Empty);
            }

            if (facts.CentralState == CentralState.UpdatedInCentral
                || !facts.IsParameterWritable
                || !ValueComparer.AreEqual(facts.CurrentValue, item.ExpectedCurrentValue))
            {
                return new WriteFailure(item.Id, WriteFailReason.NotUpToDate, string.Empty);
            }

            // NotWorkshared, CurrentWithCentral, NotYetInCentral, Unknown are written.
            return null;
        }
    }
}
