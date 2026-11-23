using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using static RevitNumbering.Tests.Numbering.NumberingTestData;

namespace RevitNumbering.Tests.Numbering
{
    /// <summary>
    /// Composition comparison and plan invariants.
    /// </summary>
    public class SameCompositionTests
    {
        /// <summary>Example 1 where door 201 (out of scope) has the given value and door 102 an owner.</summary>
        private static NumberingPlan Example1(string value201 = "Д-1-003", string owner102 = null)
        {
            var scenario = new Scenario { Scope = Range(101, 105) };
            foreach (long id in Range(101, 105))
            {
                scenario.Add(El(id, L1, owner: id == 102 ? owner102 : null));
            }

            return scenario.Add(El(201, L1, value201)).Compute();
        }

        [Fact]
        public void SameInput_SameComposition()
        {
            Assert.True(NumberingEngine.SameComposition(Example1(), Example1()));
        }

        [Fact]
        public void OccupiedValueMoved_Differs()
        {
            NumberingPlan fresh = Example1("Д-1-004");

            Assert.Equal("Д-1-003", Row(fresh, 103).NewValue);
            Assert.False(NumberingEngine.SameComposition(Example1(), fresh));
        }

        [Fact]
        public void NoSkipsAnyMore_Differs()
        {
            NumberingPlan fresh = Example1("Д-1-099");

            Assert.Empty(fresh.SkippedNumbers);
            Assert.False(NumberingEngine.SameComposition(Example1(), fresh));
        }

        [Fact]
        public void FixedSetChanged_Differs()
        {
            NumberingPlan fresh = Example1(owner102: "Петров");

            Assert.Equal(RowStatus.Fixed, Row(fresh, 102).Status);
            Assert.False(NumberingEngine.SameComposition(Example1(), fresh));
        }

        [Fact]
        public void OnlyEdgeWhitespaceOfOutOfScopeValue_Same()
        {
            Assert.True(NumberingEngine.SameComposition(Example1(), Example1("Д-1-003 ")));
        }

        [Fact]
        public void ConflictRowMoved_Differs()
        {
            Scenario Data(string value9)
            {
                return new Scenario
                {
                    Scope = new long[] { 1, 2, 3, 4 },
                    Strategy = ConflictStrategy.AllowDuplicates
                }.Add(El(1, L1), El(2, L1, "Д-1-002"), El(3, L1, "Д-1-050", owner: "Иванов"), El(4, L1),
                    El(9, L1, value9));
            }

            NumberingPlan shown = Data("Д-1-003").Compute();
            NumberingPlan fresh = Data("Д-1-001").Compute();

            Assert.Equal(RowStatus.Conflict, Row(shown, 4).Status);
            Assert.Equal(RowStatus.Conflict, Row(fresh, 1).Status);
            Assert.Equal(RowStatus.WillChange, Row(fresh, 4).Status);
            Assert.False(NumberingEngine.SameComposition(shown, fresh));
        }

        [Fact]
        public void SameConflictSet_OtherValues_Differs()
        {
            Scenario Data(bool firstWithoutPoint)
            {
                return new Scenario { Scope = new long[] { 1, 2 }, Strategy = ConflictStrategy.AllowDuplicates }
                    .Add(El(1, L1, noPoint: firstWithoutPoint), El(2, L1), El(8, L1, "Д-1-002"),
                        El(9, L1, "Д-1-001"));
            }

            NumberingPlan shown = Data(false).Compute();
            NumberingPlan fresh = Data(true).Compute();

            AssertStatus(shown, RowStatus.Conflict, 1, 2);
            AssertStatus(fresh, RowStatus.Conflict, 1, 2);
            AssertValues(shown, (1, "Д-1-001"), (2, "Д-1-002"));
            AssertValues(fresh, (1, "Д-1-002"), (2, "Д-1-001"));
            Assert.False(NumberingEngine.SameComposition(shown, fresh));
        }

        [Fact]
        public void SameComposition_NullArguments_Throw()
        {
            NumberingPlan plan = Example1();

            Assert.Throws<ArgumentNullException>(() => NumberingEngine.SameComposition(null, plan));
            Assert.Throws<ArgumentNullException>(() => NumberingEngine.SameComposition(plan, null));
        }

        /// <summary>
        /// Every plan of the Compute tests goes through NumberingTestData.AssertInvariants (Scenario.Compute).
        /// Here: a plan built by hand gives the same counters as Compute.
        /// </summary>
        [Fact]
        public void PlanInvariants_HandMadePlanHasTheSameCounters()
        {
            Scenario scenario = NumberingEngineTests.Example1();
            scenario.Strategy = ConflictStrategy.AllowDuplicates;
            NumberingPlan computed = scenario.Compute();

            var manual = new NumberingPlan(computed.SnapshotGeneration,
                computed.Rows.Select(r => new PlannedRow(r.Id, r.NewValue, r.Status, r.FixReason, r.OwnerName))
                    .ToArray(),
                computed.SkippedNumbers, computed.Duplicates, computed.Warnings, computed.BlockingErrors);

            NumberingTestData.AssertInvariants(scenario.Input(), manual);
            Assert.Equal(computed.WillChangeCount, manual.WillChangeCount);
            Assert.Equal(computed.ConflictCount, manual.ConflictCount);
            Assert.Equal(computed.CanApply, manual.CanApply);

            var blocked = new NumberingPlan(1, new[] { new PlannedRow(1, "A", RowStatus.WillChange, FixReason.None, null) },
                null, null, null, new[] { new BlockingError(null, new DuplicateValue("A", new long[] { 1, 2 }), null) });
            Assert.False(blocked.CanApply);
            Assert.Equal(1, blocked.WillChangeCount);
        }
    }
}
