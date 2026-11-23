using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Ordering;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Tests.Ordering
{
    /// <summary>
    /// How NumberingEngine.Compute calls the strategy.
    /// These cases need working template and numbering.
    /// </summary>
    public class TraversalNumberingTests
    {
        private static readonly LevelData Level1 = OrderingTestData.Level("L1", "Этаж 1", 0);

        private static readonly TraversalOrder TopLeftRows = OrderingTestData.GridOrder("TopLeft", "Rows");

        private static IEnumerable<ElementData> Doors3x3OnLevel1()
        {
            return OrderingTestData.Doors3x3()
                .Select(item => OrderingTestData.Element(item.Id, item.Xmm, item.Ymm, Level1));
        }

        private static Dictionary<long, string> NewValues(NumberingPlan plan)
        {
            return plan.Rows.ToDictionary(row => row.Id, row => row.NewValue);
        }

        [Fact]
        public void NewStrategy_IsUsedByCompute()
        {
            var registry = new TraversalRegistry(new ITraversalStrategy[] { new GridTraversal(), new FakeTraversal() });
            CategorySnapshot snapshot = OrderingTestData.Snapshot(new[]
            {
                OrderingTestData.Element(1, 0, 0, Level1),
                OrderingTestData.Element(2, 1000, 0, Level1),
                OrderingTestData.Element(3, 2000, 0, Level1)
            }, new[] { Level1 });
            var order = new TraversalOrder("test", new Dictionary<string, string>());

            NumberingPlan plan = NumberingEngine.Compute(OrderingTestData.Input(snapshot, "Д-{N:000}", order),
                registry);

            Assert.Equal("Д-001", OrderingTestData.Row(plan, 3).NewValue);
            Assert.Equal("Д-002", OrderingTestData.Row(plan, 2).NewValue);
            Assert.Equal("Д-003", OrderingTestData.Row(plan, 1).NewValue);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ElementWithoutLevel_DoesNotShiftNumbers(bool withDoor5)
        {
            var elements = new List<ElementData>
            {
                OrderingTestData.Element(11, 0, 0, Level1),
                OrderingTestData.Element(12, 2000, 0, Level1),
                OrderingTestData.Element(13, 4000, 0, Level1)
            };
            if (withDoor5)
            {
                elements.Add(OrderingTestData.Element(5, -1000, 0, null));
            }

            CategorySnapshot snapshot = OrderingTestData.Snapshot(elements, new[] { Level1 });

            NumberingPlan plan = NumberingEngine.Compute(
                OrderingTestData.Input(snapshot, "Д-{Уровень}-{N:000}", TopLeftRows),
                OrderingTestData.GridRegistry());

            Assert.Equal("Д-Этаж 1-001", OrderingTestData.Row(plan, 11).NewValue);
            Assert.Equal("Д-Этаж 1-002", OrderingTestData.Row(plan, 12).NewValue);
            Assert.Equal("Д-Этаж 1-003", OrderingTestData.Row(plan, 13).NewValue);
            if (withDoor5)
            {
                PlannedRow door5 = OrderingTestData.Row(plan, 5);
                Assert.Equal(RowStatus.Fixed, door5.Status);
                Assert.Equal(FixReason.NoLevel, door5.FixReason);
            }
        }

        [Fact]
        public void NoLevelGroup_HasItsOwnCounter()
        {
            CategorySnapshot snapshot = OrderingTestData.Snapshot(new[]
            {
                OrderingTestData.Element(7, 5000, 0, null),
                OrderingTestData.Element(8, 0, 0, null)
            }, new LevelData[0]);

            NumberingPlan plan = NumberingEngine.Compute(OrderingTestData.Input(snapshot, "Д-{N:000}", TopLeftRows),
                OrderingTestData.GridRegistry());

            Assert.Equal("Д-001", OrderingTestData.Row(plan, 8).NewValue);
            Assert.Equal("Д-002", OrderingTestData.Row(plan, 7).NewValue);
            Assert.DoesNotContain(plan.BlockingErrors, error => error.TemplateIssue != null);
        }

        [Fact]
        public void NoLevelGroup_IsSeparateLevelForTemplateCheck()
        {
            CategorySnapshot snapshot = OrderingTestData.Snapshot(new[]
            {
                OrderingTestData.Element(11, 0, 0, Level1),
                OrderingTestData.Element(12, 2000, 0, Level1),
                OrderingTestData.Element(5, -1000, 0, null)
            }, new[] { Level1 });

            NumberingPlan plan = NumberingEngine.Compute(OrderingTestData.Input(snapshot, "Д-{N:000}", TopLeftRows),
                OrderingTestData.GridRegistry());

            Assert.Contains(plan.BlockingErrors, error => error.TemplateIssue != null
                && error.TemplateIssue.Code == TemplateIssueCode.MissingLevelForManyLevels);
            Assert.False(plan.CanApply);
        }

        [Fact]
        public void FixedElement_TakesNoPlaceInSequence()
        {
            LevelData level = OrderingTestData.Level("L1", "1", 0);
            CategorySnapshot snapshot = OrderingTestData.Snapshot(new[]
            {
                OrderingTestData.Element(1, 0, 0, level),
                OrderingTestData.Element(2, 1000, 0, level),
                OrderingTestData.Element(3, 2000, 0, level, "Д-1-007", true),
                OrderingTestData.Element(4, 3000, 0, level),
                OrderingTestData.Element(5, 4000, 0, level)
            }, new[] { level });

            NumberingPlan plan = NumberingEngine.Compute(
                OrderingTestData.Input(snapshot, "Д-{Уровень}-{N:000}", TopLeftRows),
                OrderingTestData.GridRegistry());

            Assert.Equal("Д-1-001", OrderingTestData.Row(plan, 1).NewValue);
            Assert.Equal("Д-1-002", OrderingTestData.Row(plan, 2).NewValue);
            Assert.Equal("Д-1-003", OrderingTestData.Row(plan, 4).NewValue);
            Assert.Equal("Д-1-004", OrderingTestData.Row(plan, 5).NewValue);
            PlannedRow door3 = OrderingTestData.Row(plan, 3);
            Assert.Equal(RowStatus.Fixed, door3.Status);
            Assert.Equal(FixReason.OwnedByOtherUser, door3.FixReason);
            Assert.DoesNotContain(plan.Rows, row => row.Id != 3 && row.NewValue == "Д-1-007");
        }

        [Fact]
        public void RepeatedCompute_GivesSameNumbers()
        {
            TraversalRegistry registry = OrderingTestData.GridRegistry();
            ElementData[] doors = Doors3x3OnLevel1().ToArray();

            NumberingPlan plan1 = NumberingEngine.Compute(OrderingTestData.Input(
                OrderingTestData.Snapshot(doors, new[] { Level1 }), "Д-{N:000}", TopLeftRows), registry);

            long[] sequence = OrderingTestData.Expected3x3("TopLeft", "Rows");
            for (int i = 0; i < sequence.Length; i++)
            {
                Assert.Equal("Д-00" + (i + 1), OrderingTestData.Row(plan1, sequence[i]).NewValue);
            }

            NumberingPlan plan2 = NumberingEngine.Compute(OrderingTestData.Input(
                OrderingTestData.Snapshot(Enumerable.Reverse(doors),new[] { Level1 }), "Д-{N:000}", TopLeftRows), registry);

            Assert.True(NumberingEngine.SameComposition(plan1, plan2));
            Assert.Equal(NewValues(plan1), NewValues(plan2));

            Dictionary<long, string> written = NewValues(plan1);
            IEnumerable<ElementData> numbered = doors.Select(door =>
                OrderingTestData.Element(door.Id, door.Xmm, door.Ymm, Level1, written[door.Id]));

            NumberingPlan plan3 = NumberingEngine.Compute(OrderingTestData.Input(
                OrderingTestData.Snapshot(numbered, new[] { Level1 }), "Д-{N:000}", TopLeftRows), registry);

            Assert.All(plan3.Rows, row => Assert.Equal(RowStatus.Unchanged, row.Status));
            Assert.Equal(0, plan3.WillChangeCount);
        }

        [Fact]
        public void ElementsWithoutPoint_GoToTheEndOfGroup()
        {
            CategorySnapshot snapshot = OrderingTestData.Snapshot(new[]
            {
                OrderingTestData.Element(3, null, null, Level1),
                OrderingTestData.Element(21, 1000, 0, Level1),
                OrderingTestData.Element(22, 0, 0, Level1)
            }, new[] { Level1 });

            NumberingPlan plan = NumberingEngine.Compute(OrderingTestData.Input(snapshot, "Д-{N:000}", TopLeftRows),
                OrderingTestData.GridRegistry());

            Assert.Equal("Д-001", OrderingTestData.Row(plan, 22).NewValue);
            Assert.Equal("Д-002", OrderingTestData.Row(plan, 21).NewValue);
            Assert.Equal("Д-003", OrderingTestData.Row(plan, 3).NewValue);
        }

        [Theory]
        [InlineData("Doors")]
        [InlineData("Windows")]
        public void SameOrderForAllCategories(string categoryKey)
        {
            CategorySnapshot snapshot = OrderingTestData.Snapshot(Doors3x3OnLevel1(), new[] { Level1 },
                categoryKey);
            TraversalOrder order = OrderingTestData.GridOrder("BottomLeft", "Columns");

            NumberingPlan plan = NumberingEngine.Compute(OrderingTestData.Input(snapshot, "Д-{N:000}", order),
                OrderingTestData.GridRegistry());

            // G D A H E B I F C (variant 7).
            long[] sequence = { 233, 118, 305, 109, 301, 112, 316, 127, 240 };
            for (int i = 0; i < sequence.Length; i++)
            {
                Assert.Equal("Д-00" + (i + 1), OrderingTestData.Row(plan, sequence[i]).NewValue);
            }
        }
    }
}
