using RevitNumbering.Core.Writing;

namespace RevitNumbering.Tests.Writing
{
    /// <summary>Check of one element before the transaction.</summary>
    public class WritePrecheckTests
    {
        private static readonly WriteItem Item = new WriteItem(201, "Д-1-001", "Д-1-002");

        /// <summary>Default facts: exists, not owned, CurrentWithCentral, writable, "Д-1-001".</summary>
        private static ElementWriteFacts Facts(long id = 201, bool exists = true, bool owned = false,
            string owner = null, CentralState state = CentralState.CurrentWithCentral, bool writable = true,
            string current = "Д-1-001")
        {
            return new ElementWriteFacts(id, exists, owned, owner, state, writable, current);
        }

        [Fact]
        public void Deleted()
        {
            WritingTestData.AssertFailure(201, WriteFailReason.Deleted, "",
                WritePrecheck.Check(Item, Facts(exists: false)));
        }

        [Fact]
        public void DeletedInCentral()
        {
            WritingTestData.AssertFailure(201, WriteFailReason.Deleted, "",
                WritePrecheck.Check(Item, Facts(state: CentralState.DeletedInCentral)));
        }

        [Fact]
        public void OwnedBeatsOutdated()
        {
            WritingTestData.AssertFailure(201, WriteFailReason.OwnedByOtherUser, "petrov",
                WritePrecheck.Check(Item, Facts(owned: true, owner: "petrov", state: CentralState.UpdatedInCentral)));
        }

        [Fact]
        public void UpdatedInCentral()
        {
            WritingTestData.AssertFailure(201, WriteFailReason.NotUpToDate, "",
                WritePrecheck.Check(Item, Facts(state: CentralState.UpdatedInCentral)));
        }

        [Fact]
        public void ParameterNotWritable()
        {
            WritingTestData.AssertFailure(201, WriteFailReason.NotUpToDate, "",
                WritePrecheck.Check(Item, Facts(writable: false)));
        }

        [Fact]
        public void CurrentValueDiffersByCase()
        {
            WritingTestData.AssertFailure(201, WriteFailReason.NotUpToDate, "",
                WritePrecheck.Check(Item, Facts(current: "д-1-001")));
        }

        [Fact]
        public void OuterSpacesAreIgnored()
        {
            Assert.Null(WritePrecheck.Check(Item, Facts(current: " Д-1-001 ")));
        }

        [Theory]
        [InlineData(CentralState.NotYetInCentral)]
        [InlineData(CentralState.NotWorkshared)]
        [InlineData(CentralState.Unknown)]
        public void CanBeWritten(CentralState state)
        {
            Assert.Null(WritePrecheck.Check(Item, Facts(state: state)));
        }

        [Fact]
        public void FactsOfAnotherElement_Throws()
        {
            Assert.Throws<ArgumentException>(() => WritePrecheck.Check(Item, Facts(id: 202)));
        }
    }
}
