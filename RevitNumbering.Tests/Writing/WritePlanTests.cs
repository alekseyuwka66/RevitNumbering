using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Writing;
using static RevitNumbering.Tests.Writing.WritingTestData;

namespace RevitNumbering.Tests.Writing
{
    /// <summary>Plan invariants, WritePlan.Create and texts.</summary>
    public class WritePlanTests
    {
        private static WritePlan PlanOf(params WriteItem[] items)
        {
            return new WritePlan(DocumentA, 8, Doors.Key, Doors.DisplayName, items);
        }

        [Fact]
        public void EmptyValue_Throws()
        {
            Assert.Throws<ArgumentException>(() => PlanOf(new WriteItem(201, "Д-1-001", "  ")));
        }

        [Fact]
        public void ValueDoesNotChange_Throws()
        {
            Assert.Throws<ArgumentException>(() => PlanOf(new WriteItem(201, "Д-1-001", "Д-1-001 ")));
        }

        [Fact]
        public void RepeatedId_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => PlanOf(new WriteItem(201, "Д-1-001", "Д-1-002"), new WriteItem(201, "Д-1-001", "Д-1-003")));
        }

        [Fact]
        public void Constructor_NullDocumentThrows_NullItemsBecomeEmpty()
        {
            Assert.Throws<ArgumentNullException>(() => new WritePlan(null, 8, "Doors", "Двери", null));
            Assert.Empty(new WritePlan(DocumentA, 8, "Doors", "Двери", null).Items);

            var item = new WriteItem(1, null, null);
            Assert.Equal(string.Empty, item.ExpectedCurrentValue);
            Assert.Equal(string.Empty, item.NewValue);
        }

        [Theory]
        [InlineData("Двери", 120, "Нумерация: Двери (120)")]
        [InlineData("Стены", 10000, "Нумерация: Стены (10000)")]
        public void TransactionName(string category, int count, string expected)
        {
            Assert.Equal(expected, WriteTexts.TransactionName(category, count));
        }

        [Fact]
        public void AttemptsExhaustedText()
        {
            Assert.Equal("превышено число попыток записи: 5", WriteTexts.AttemptsExhausted(5));
        }

        // The WritePlan.Create rule is covered here.

        private static CategorySnapshot Snapshot(long generation = 8)
        {
            return new CategorySnapshot(DocumentA, generation, Doors, new[]
            {
                Element(1, "old-1"), Element(2, null), Element(3, "Д-1-003"), Element(4, "old-4"),
                Element(5, "Д-1-005"), Element(6, "x")
            }, null, false);
        }

        private static NumberingPlan NumberingPlan(long generation = 8, params PlannedRow[] extraRows)
        {
            var rows = new List<PlannedRow>
            {
                new PlannedRow(4, "Д-1-004", RowStatus.Conflict, FixReason.None, null),
                new PlannedRow(1, "Д-1-001", RowStatus.WillChange, FixReason.None, null),
                new PlannedRow(3, "Д-1-003", RowStatus.Unchanged, FixReason.None, null),
                new PlannedRow(5, "", RowStatus.Fixed, FixReason.OwnedByOtherUser, "petrov"),
                new PlannedRow(6, "", RowStatus.OutOfScope, FixReason.None, null),
                new PlannedRow(2, "Д-1-002", RowStatus.WillChange, FixReason.None, null)
            };
            rows.AddRange(extraRows);
            return new NumberingPlan(generation, rows, null, null, null, null);
        }

        [Fact]
        public void Create_TakesWillChangeAndConflictRowsInPlanOrder()
        {
            WritePlan plan = WritePlan.Create(DocumentA, Snapshot(), NumberingPlan());

            Assert.Equal(DocumentA, plan.Document);
            Assert.Equal(8, plan.SnapshotGeneration);
            Assert.Equal("Doors", plan.CategoryKey);
            Assert.Equal("Двери", plan.CategoryDisplayName);
            Assert.Equal(new long[] { 4, 1, 2 }, Ids(plan.Items));
            Assert.Equal(new[] { "old-4", "old-1", "" }, plan.Items.Select(item => item.ExpectedCurrentValue));
            Assert.Equal(new[] { "Д-1-004", "Д-1-001", "Д-1-002" }, plan.Items.Select(item => item.NewValue));
        }

        [Fact]
        public void Create_RejectsWrongArguments()
        {
            Assert.Throws<ArgumentNullException>(() => WritePlan.Create(null, Snapshot(), NumberingPlan()));
            Assert.Throws<ArgumentNullException>(() => WritePlan.Create(DocumentA, null, NumberingPlan()));
            Assert.Throws<ArgumentNullException>(() => WritePlan.Create(DocumentA, Snapshot(), null));
            Assert.Throws<ArgumentException>(() => WritePlan.Create(DocumentB, Snapshot(), NumberingPlan()));
            Assert.Throws<ArgumentException>(() => WritePlan.Create(DocumentA, Snapshot(9), NumberingPlan()));
            Assert.Throws<ArgumentException>(() => WritePlan.Create(DocumentA, Snapshot(), NumberingPlan(8,
                new PlannedRow(77, "Д-1-077", RowStatus.WillChange, FixReason.None, null))));
        }
    }
}
