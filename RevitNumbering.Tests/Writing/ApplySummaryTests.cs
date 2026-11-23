using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Writing;
using static RevitNumbering.Tests.Writing.WritingTestData;

namespace RevitNumbering.Tests.Writing
{
    /// <summary>Model of the final summary.</summary>
    public class ApplySummaryTests
    {
        private const string ExhaustedText = "превышено число попыток записи: 5";

        /// <summary>Plan П.</summary>
        private static NumberingPlan PlanP()
        {
            var rows = new[]
            {
                new PlannedRow(1, "Д-1-001", RowStatus.WillChange, FixReason.None, null),
                new PlannedRow(2, "Д-1-002", RowStatus.WillChange, FixReason.None, null),
                new PlannedRow(3, "", RowStatus.Fixed, FixReason.OwnedByOtherUser, "petrov"),
                new PlannedRow(4, "Д-1-004", RowStatus.Conflict, FixReason.None, null),
                new PlannedRow(5, "Д-1-005", RowStatus.WillChange, FixReason.None, null),
                new PlannedRow(6, "", RowStatus.Unchanged, FixReason.None, null),
                new PlannedRow(7, "", RowStatus.Fixed, FixReason.ReadOnlyParameter, null),
                new PlannedRow(9, "", RowStatus.OutOfScope, FixReason.None, null)
            };
            var duplicates = new[] { new DuplicateValue("Д-1-004", new long[] { 4, 9 }) };
            return new NumberingPlan(8, rows, null, duplicates, null, null);
        }

        /// <summary>Report О1.</summary>
        private static WriteReport ReportO1()
        {
            return new WriteReport(WriteOutcome.Committed,
                new[] { new WrittenValue(1, "Д-1-001"), new WrittenValue(4, "Д-1-004"), new WrittenValue(5, "Д-1-005") },
                new[] { RolledBackFailure(2, "W") }, 2);
        }

        /// <summary>Re-read data Р1.</summary>
        private static List<ElementData> RereadR1()
        {
            return new List<ElementData>
            {
                Element(1, "Д-1-001"), Element(2, "Д-7"), Element(3, "Д-1-007"), Element(4, "Д-1-004"),
                Element(5, "Д-1-005"), Element(6, "Д-1-006"), Element(7, "Д-1-008"), Element(9, "Д-1-004"),
                Element(10, "Д-2-001"), Element(11, "Д-2-001")
            };
        }

        private static CategorySnapshot Snapshot(CategoryInfo category, IEnumerable<ElementData> elements)
        {
            return new CategorySnapshot(DocumentA, 9, category, elements.ToArray(), null, false);
        }

        private static WriteReport Committed(params long[] writtenIds)
        {
            return new WriteReport(WriteOutcome.Committed,
                writtenIds.Select(id => new WrittenValue(id, "any")).ToArray(), null, 1);
        }

        private static NumberingPlan EmptyPlan()
        {
            return new NumberingPlan(8, null, null, null, null, null);
        }

        private static void AssertDuplicate(string value, long[] ids, DuplicateValue actual)
        {
            Assert.Equal(value, actual.Value);
            Assert.Equal(ids, actual.ElementIds);
        }

        private static void AssertFixedOfPlanP(ApplySummary summary)
        {
            Assert.Equal(2, summary.FixedBeforeWrite.Count);
            Assert.Equal(3, summary.FixedBeforeWrite[0].Id);
            Assert.Equal(FixReason.OwnedByOtherUser, summary.FixedBeforeWrite[0].Reason);
            Assert.Equal("petrov", summary.FixedBeforeWrite[0].OwnerName);
            Assert.Equal(7, summary.FixedBeforeWrite[1].Id);
            Assert.Equal(FixReason.ReadOnlyParameter, summary.FixedBeforeWrite[1].Reason);
            Assert.Null(summary.FixedBeforeWrite[1].OwnerName);
        }

        [Fact]
        public void AllSectionsBeforeReread()
        {
            ApplySummary summary = ApplySummary.Build(PlanP(), ReportO1(), null);

            Assert.Equal(3, summary.ChangedCount);
            AssertFixedOfPlanP(summary);
            AssertFailure(2, WriteFailReason.RolledBackByWarning, "W", Assert.Single(summary.Failures));
            AssertDuplicate("Д-1-004", new long[] { 4, 9 }, Assert.Single(summary.RemainingDuplicates));
        }

        [Fact]
        public void AfterReread_OnlyDuplicatesWithWrittenElements()
        {
            ApplySummary summary = ApplySummary.Build(PlanP(), ReportO1(), Snapshot(Doors, RereadR1()));

            AssertDuplicate("Д-1-004", new long[] { 4, 9 }, Assert.Single(summary.RemainingDuplicates));
        }

        [Fact]
        public void DuplicateGone_NewOneAppeared()
        {
            List<ElementData> elements = RereadR1();
            elements[elements.FindIndex(element => element.Id == 9)] = Element(9, "Д-1-009");
            elements.Add(Element(12, "Д-1-005 "));

            ApplySummary summary = ApplySummary.Build(PlanP(), ReportO1(), Snapshot(Doors, elements));

            AssertDuplicate("Д-1-005", new long[] { 5, 12 }, Assert.Single(summary.RemainingDuplicates));
        }

        [Fact]
        public void ConflictElementNotWritten()
        {
            var report = new WriteReport(WriteOutcome.Committed,
                new[] { new WrittenValue(1, "Д-1-001"), new WrittenValue(5, "Д-1-005") },
                new[] { RolledBackFailure(2, "W"), new WriteFailure(4, WriteFailReason.NotUpToDate, "") }, 2);

            ApplySummary summary = ApplySummary.Build(PlanP(), report, null);

            Assert.Equal(2, summary.ChangedCount);
            Assert.Empty(summary.RemainingDuplicates);
        }

        [Fact]
        public void FailedOutcome()
        {
            var report = new WriteReport(WriteOutcome.Failed, null,
                new[] { RolledBackFailure(2, "W"), RolledBackFailure(0, ExhaustedText) }, 5);

            ApplySummary summary = ApplySummary.Build(PlanP(), report, Snapshot(Doors, RereadR1()));

            Assert.Equal(0, summary.ChangedCount);
            AssertFixedOfPlanP(summary);
            Assert.Equal(2, summary.Failures.Count);
            AssertFailure(2, WriteFailReason.RolledBackByWarning, "W", summary.Failures[0]);
            AssertFailure(0, WriteFailReason.RolledBackByWarning, ExhaustedText, summary.Failures[1]);
            Assert.Empty(summary.RemainingDuplicates);
        }

        [Fact]
        public void Rooms_DuplicatesWithinPhase()
        {
            var reread = Snapshot(Rooms, new[]
            {
                Element(21, "101", "P1"), Element(22, "101", "P2"), Element(23, "101", "P1")
            });

            ApplySummary summary = ApplySummary.Build(EmptyPlan(), Committed(21), reread);

            AssertDuplicate("101", new long[] { 21, 23 }, Assert.Single(summary.RemainingDuplicates));
        }

        [Fact]
        public void DuplicatesOrder()
        {
            var reread = Snapshot(Doors, new[]
            {
                Element(31, "Б"), Element(32, "Б"), Element(33, "А"), Element(34, "А")
            });

            ApplySummary summary = ApplySummary.Build(EmptyPlan(), Committed(31, 33), reread);

            Assert.Equal(2, summary.RemainingDuplicates.Count);
            AssertDuplicate("А", new long[] { 33, 34 }, summary.RemainingDuplicates[0]);
            AssertDuplicate("Б", new long[] { 31, 32 }, summary.RemainingDuplicates[1]);
        }

        [Theory]
        [InlineData(WriteOutcome.Stale)]
        [InlineData(WriteOutcome.NothingToWrite)]
        [InlineData(WriteOutcome.DocumentMismatch)]
        [InlineData(WriteOutcome.Cancelled)]
        public void OutcomeWithoutSummary_Throws(WriteOutcome outcome)
        {
            Assert.Throws<ArgumentException>(
                () => ApplySummary.Build(PlanP(), WriteSession.Rejected(outcome), null));
        }
    }
}
