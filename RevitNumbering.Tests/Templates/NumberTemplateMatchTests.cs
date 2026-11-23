using static RevitNumbering.Tests.Templates.TemplateTestData;

namespace RevitNumbering.Tests.Templates
{
    /// <summary>
    /// Matching an existing value against the template.
    /// </summary>
    public class NumberTemplateMatchTests
    {
        private const string DoorTemplate = "Д-{Уровень}-{N:000}";

        [Theory]
        [InlineData("Д-1-010", 10)]
        [InlineData("Д-1-10", 10)]
        [InlineData("Д-1-0010", 10)]
        [InlineData(" Д-1-007 ", 7)]
        [InlineData("Д-1-000", 0)]
        public void TryMatch_Matches(string value, int expected)
        {
            Assert.True(Template(DoorTemplate).TryMatch(value, "1", out int counter));
            Assert.Equal(expected, counter);
        }

        [Theory]
        [InlineData("Д-2-010", "1")]
        [InlineData("д-1-010", "1")]
        [InlineData("Д-1-01a", "1")]
        [InlineData("Д-1-", "1")]
        [InlineData("Д-1-١٢", "1")]
        [InlineData("Д-1-99999999999", "1")]
        [InlineData("", "1")]
        [InlineData(null, "1")]
        [InlineData("Д-1-010", null)]
        public void TryMatch_DoesNotMatch(string value, string levelCode)
        {
            Assert.False(Template(DoorTemplate).TryMatch(value, levelCode, out int counter));
            Assert.Equal(0, counter);
        }

        [Theory]
        [InlineData("{Уровень}{N:00}", "1", "105", true, 5)]
        [InlineData("{Уровень}{N:00}", "1", "1", false, 0)]
        [InlineData("{Уровень}{N:00}", "1", "205", false, 0)]
        [InlineData("{N}{Уровень}", "1", "121", true, 12)]
        [InlineData("Д-{N:000}", null, "Д-005", true, 5)]
        public void TryMatch_DigitsNextToLevelCode(string text, string levelCode, string value,
            bool expectedResult, int expectedCounter)
        {
            Assert.Equal(expectedResult, Template(text).TryMatch(value, levelCode, out int counter));
            Assert.Equal(expectedCounter, counter);
        }

        [Theory]
        [InlineData("04/04", true, 4)]
        [InlineData("04/05", false, 0)]
        public void TryMatch_RepeatedCounter(string value, bool expectedResult, int expectedCounter)
        {
            Assert.Equal(expectedResult, Template("{N:00}/{N:00}").TryMatch(value, null, out int counter));
            Assert.Equal(expectedCounter, counter);
        }

        [Fact]
        public void ContinueCounting_DataForAppendMode()
        {
            var template = Template(DoorTemplate);
            var values = Enumerable.Range(1, 10).Where(n => n != 5).Select(n => template.Format("1", n)).ToList();

            int max = 0;
            foreach (string value in values)
            {
                Assert.True(template.TryMatch(value, "1", out int counter));
                max = Math.Max(max, counter);
            }

            Assert.Equal("Д-1-001", values[0]);
            Assert.Equal(10, max);
            Assert.Equal("Д-1-011", template.Format("1", max + 1));
        }
    }
}
