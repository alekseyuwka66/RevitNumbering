using RevitNumbering.Core.Numbering;
using static RevitNumbering.Tests.Numbering.NumberingTestData;

namespace RevitNumbering.Tests.Numbering
{
    /// <summary>The single value comparison.</summary>
    public class ValueComparerTests
    {
        [Fact]
        public void CaseDiffers_NotEqual()
        {
            Assert.False(ValueComparer.AreEqual("Д-1", "д-1"));
        }

        [Theory]
        [InlineData("Д-1 ", "Д-1")]
        [InlineData(" Д-1\t", "Д-1")]
        public void EdgeWhitespace_IsIgnored(string a, string b)
        {
            Assert.True(ValueComparer.AreEqual(a, b));
        }

        [Fact]
        public void NullAndEmpty()
        {
            Assert.Equal(string.Empty, ValueComparer.Normalize(null));
            Assert.True(ValueComparer.AreEqual(null, "  "));
            Assert.False(ValueComparer.AreEqual("Д-1", "Д-1."));
        }

        [Fact]
        public void OccupiedSearch_UsesTheSameComparison()
        {
            var scenario = new Scenario { Scope = new long[] { 1, 2, 3 } }
                .Add(Empty(L1, 1, 2, 3))
                .Add(El(7, L2, "д-1-002"), El(8, L2, "Д-1-003 "));

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (1, "Д-1-001"), (2, "Д-1-002"), (3, "Д-1-004"));
            AssertSkipped(plan, ("Д-1-003", 8));
            Assert.Equal(1, plan.ConflictCount);
        }
    }
}
