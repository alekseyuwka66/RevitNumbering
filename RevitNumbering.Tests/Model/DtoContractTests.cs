using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Tests.Model
{
    /// <summary>
    /// DTO constructor rules fixed by the contracts: defensive copies, null collections become empty,
    /// null Name / CurrentValue become "".
    /// </summary>
    public class DtoContractTests
    {
        [Fact]
        public void ElementData_NullNameAndValue_BecomeEmpty()
        {
            var element = new ElementData(1, null, null, null, null, null, null, null, false, false, null,
                true, 0, false);

            Assert.Equal(string.Empty, element.Name);
            Assert.Equal(string.Empty, element.CurrentValue);
            Assert.Null(element.LevelKey);
        }

        [Fact]
        public void CategorySnapshot_NullCollections_BecomeEmpty()
        {
            var snapshot = new CategorySnapshot(new DocumentKey("d"), 1, null, null, null, false);

            Assert.Empty(snapshot.Elements);
            Assert.Empty(snapshot.Levels);
        }

        [Fact]
        public void SelectionSnapshot_CopiesIds()
        {
            var ids = new List<long> { 1, 2 };
            var selection = new SelectionSnapshot(new DocumentKey("d"), ids, false);
            ids.Add(3);

            Assert.Equal(new long[] { 1, 2 }, selection.ElementIds);
        }

        [Fact]
        public void DocumentKey_ComparesByValue()
        {
            Assert.Equal(new DocumentKey("a"), new DocumentKey("a"));
            Assert.NotEqual(new DocumentKey("a"), new DocumentKey("A"));
            Assert.True(new DocumentKey("a") == new DocumentKey("a"));
        }

        [Fact]
        public void TemplateIssue_OnlyWidthTooSmallIsWarning()
        {
            foreach (TemplateIssueCode code in Enum.GetValues(typeof(TemplateIssueCode)))
            {
                var issue = new TemplateIssue(code, null);

                Assert.Equal(code == TemplateIssueCode.WidthTooSmall, issue.IsWarning);
                Assert.Empty(issue.Arguments);
            }
        }

        [Fact]
        public void NumberingPlan_ComputesCounters()
        {
            var rows = new[]
            {
                new PlannedRow(1, "A-1", RowStatus.WillChange, FixReason.None, null),
                new PlannedRow(2, "A-2", RowStatus.Conflict, FixReason.None, null),
                new PlannedRow(3, null, RowStatus.Unchanged, FixReason.None, null)
            };
            var skipped = new[] { new SkippedNumber("A-3", 10) };

            var plan = new NumberingPlan(7, rows, skipped, null, null, null);

            Assert.Equal(2, plan.WillChangeCount);
            Assert.Equal(2, plan.ConflictCount);
            Assert.True(plan.CanApply);
            Assert.Equal(string.Empty, plan.Rows[2].NewValue);
        }
    }
}
