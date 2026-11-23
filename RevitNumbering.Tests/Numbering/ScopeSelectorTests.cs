using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using static RevitNumbering.Tests.Numbering.NumberingTestData;

namespace RevitNumbering.Tests.Numbering
{
    /// <summary>Scope of the "selected in model" mode.</summary>
    public class ScopeSelectorTests
    {
        [Fact]
        public void ScopeFromSelection()
        {
            CategorySnapshot snapshot = new Scenario().Add(Empty(L1, Range(1, 12))).Snapshot();
            var selection = new SelectionSnapshot(Document, new long[] { 12, 3, 900, 3, 1 }, false);

            Assert.Equal(new long[] { 1, 3, 12 }, ScopeSelector.FromSelection(selection, snapshot));
        }

        [Fact]
        public void OtherDocumentOrEmptySelection_GivesEmptyScope()
        {
            CategorySnapshot snapshot = new Scenario().Add(Empty(L1, Range(1, 12))).Snapshot();

            var foreign = new SelectionSnapshot(new DocumentKey("other"), new long[] { 1, 2 }, false);
            Assert.Empty(ScopeSelector.FromSelection(foreign, snapshot));

            var empty = new SelectionSnapshot(Document, new long[0], false);
            Assert.Empty(ScopeSelector.FromSelection(empty, snapshot));
        }

        [Fact]
        public void FromSelection_NullArguments_Throw()
        {
            CategorySnapshot snapshot = new Scenario().Snapshot();
            var selection = new SelectionSnapshot(Document, null, false);

            Assert.Throws<ArgumentNullException>(() => ScopeSelector.FromSelection(null, snapshot));
            Assert.Throws<ArgumentNullException>(() => ScopeSelector.FromSelection(selection, null));
        }

        [Fact]
        public void SelectedScope_NumbersAllTen()
        {
            var scenario = new Scenario().Add(Empty(L1, Range(1, 10)));
            var selection = new SelectionSnapshot(Document, Range(1, 10).Concat(new long[] { 900 }).ToArray(), false);
            scenario.Scope = ScopeSelector.FromSelection(selection, scenario.Snapshot());

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (1, "Д-1-001"), (5, "Д-1-005"), (10, "Д-1-010"));
            Assert.Equal(10, plan.WillChangeCount);
        }
    }
}
