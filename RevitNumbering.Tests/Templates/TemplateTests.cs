using RevitNumbering.Core.Model;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Tests.Templates
{
    public class TemplateTests
    {
        private static ProblemCode[] Codes(string text)
        {
            TemplateResult result = Template.Parse(text);
            Assert.Null(result.Template);
            return result.Problems.Select(problem => problem.Code).ToArray();
        }

        [Fact]
        public void ValidTemplate_FormatsPrefixLevelAndNumber()
        {
            TemplateResult result = Template.Parse("Д-{Уровень}-{N:000}");

            Assert.Empty(result.Problems);
            Assert.True(result.Template.HasLevel);
            Assert.True(result.Template.HasNumber);
            Assert.Equal(3, result.Template.Width);
            Assert.Equal("Д-01-007", result.Template.Format("01", 7));
        }

        [Fact]
        public void LevelPlaceholder_HasRussianAndEnglishNames()
        {
            Assert.Equal("A-02-5", Template.Parse("A-{Level}-{N}").Template.Format("02", 5));
            Assert.Equal("A-02-5", Template.Parse("A-{Уровень}-{N}").Template.Format("02", 5));
        }

        [Fact]
        public void Width_IsTheNumberOfZeros_AndNeverCutsTheNumber()
        {
            Template plain = Template.Parse("{N}").Template;
            Template two = Template.Parse("{N:00}").Template;

            Assert.Equal(0, plain.Width);
            Assert.False(plain.HasLevel);
            Assert.Equal("12", plain.Format(null, 12));
            Assert.Equal(2, two.Width);
            Assert.Equal("07", two.Format(null, 7));
            Assert.Equal("150", two.Format(null, 150));
        }

        [Theory]
        [InlineData("Д-{N")]
        [InlineData("Д-N}")]
        [InlineData("Д-{Уровень-{N}")]
        public void UnclosedBrace(string text)
        {
            Assert.Equal(new[] { ProblemCode.UnclosedBrace }, Codes(text));
        }

        [Fact]
        public void UnknownPlaceholder_ReportsItsText()
        {
            TemplateResult result = Template.Parse("Д-{Уровнь}-{N}");

            Problem problem = Assert.Single(result.Problems);
            Assert.Equal(ProblemCode.UnknownPlaceholder, problem.Code);
            Assert.Equal(new[] { "{Уровнь}" }, problem.Args);
        }

        [Theory]
        [InlineData("Д-{Уровень}")]
        [InlineData("")]
        public void NoNumber(string text)
        {
            Assert.Equal(new[] { ProblemCode.NoNumber }, Codes(text));
        }

        [Theory]
        [InlineData("{N:abc}")]
        [InlineData("{N:}")]
        [InlineData("{N:0000000000}")]
        [InlineData("{N}-{N:00}")]
        public void BadWidth(string text)
        {
            Assert.Equal(new[] { ProblemCode.BadWidth }, Codes(text));
        }
    }
}
