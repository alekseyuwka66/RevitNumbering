using RevitNumbering.Core.Templates;
using static RevitNumbering.Tests.Templates.TemplateTestData;

namespace RevitNumbering.Tests.Templates
{
    /// <summary>
    /// Parsing and number building.
    /// </summary>
    public class TemplateParserTests
    {
        [Fact]
        public void FtExample_ParsesAndFormats()
        {
            var result = TemplateParser.Parse("Д-{Уровень}-{N:000}");

            Assert.True(result.IsValid);
            Assert.Empty(result.Issues);
            Assert.True(result.Template.UsesLevel);
            Assert.Equal(3, result.Template.CounterWidth);
            Assert.Equal("Д-{Уровень}-{N:000}", result.Template.Text);
            Assert.Equal("Д-Этаж 1-001", result.Template.Format("Этаж 1", 1));
        }

        [Fact]
        public void LevelSynonym()
        {
            var template = Template("Д-{Level}-{N:000}");

            Assert.True(template.UsesLevel);
            Assert.Equal("Д-Этаж 1-012", template.Format("Этаж 1", 12));
        }

        [Fact]
        public void CounterWithoutWidth()
        {
            var template = Template("Д-{N}");

            Assert.False(template.UsesLevel);
            Assert.Equal(1, template.CounterWidth);
            Assert.Equal("Д-7", template.Format(null, 7));
            Assert.Equal("Д-1234", template.Format(null, 1234));
        }

        [Theory]
        [InlineData("{N:0}", "7")]
        [InlineData("{N:00}", "07")]
        [InlineData("{N:000000000}", "000000007")]
        public void WidthIsNumberOfZeros(string text, string expected)
        {
            Assert.Equal(expected, Template(text).Format(null, 7));
        }

        [Fact]
        public void CounterLongerThanWidth_IsNotTruncated()
        {
            Assert.Equal("Д-1-1234", Template("Д-{Уровень}-{N:000}").Format("1", 1234));
        }

        [Fact]
        public void TextIsCopiedAsIs()
        {
            Assert.Equal(" Кв.2_03 (А)", Template(" Кв.{Уровень}_{N:00} (А)").Format("2", 3));
            Assert.Equal("№7%", Template("№{N:0}%").Format(null, 7));
        }

        [Fact]
        public void RepeatedPlaceholders()
        {
            var result = TemplateParser.Parse("{Уровень}-{N:00}/{Уровень}-{N:00}");

            Assert.True(result.IsValid);
            Assert.Equal("1-04/1-04", result.Template.Format("1", 4));
        }

        [Fact]
        public void DefaultRoomTemplate()
        {
            Assert.Equal("0105", Template("{Уровень}{N:00}").Format("01", 5));
        }

        [Fact]
        public void UnclosedBrace_NewOpeningBrace()
        {
            var result = TemplateParser.Parse("Д-{Уровень-{N:000}");

            Assert.False(result.IsValid);
            Assert.Null(result.Template);
            var issue = Assert.Single(result.Issues);
            AssertIssue(issue, TemplateIssueCode.UnclosedBrace);
        }

        [Fact]
        public void UnclosedBrace_AtTheEnd_NoMissingCounter()
        {
            var result = TemplateParser.Parse("Д-{Уровень}-{N:000");

            Assert.Equal(new[] { "UnclosedBrace" }, Describe(result));
        }

        [Theory]
        [InlineData("Д}-{N}")]
        [InlineData("}{N}{")]
        public void ExtraClosingBrace_ReportedOnce(string text)
        {
            var result = TemplateParser.Parse(text);

            Assert.Equal(new[] { "UnclosedBrace" }, Describe(result));
        }

        [Fact]
        public void UnknownPlaceholder()
        {
            var first = TemplateParser.Parse("Д-{Уровнь}-{N:000}");
            var second = TemplateParser.Parse("Д-{Уровнь}");

            Assert.Equal(new[] { "UnknownPlaceholder {Уровнь}" }, Describe(first));
            Assert.Equal(new[] { "UnknownPlaceholder {Уровнь}", "MissingCounter" }, Describe(second));
            Assert.False(first.IsValid);
            Assert.Null(first.Template);
            AssertIssueText("Неизвестная подстановка: {Уровнь}", first.Issues[0]);
        }

        [Theory]
        [InlineData("{уровень}-{N}", new[] { "UnknownPlaceholder {уровень}" })]
        [InlineData("{LEVEL}-{N}", new[] { "UnknownPlaceholder {LEVEL}" })]
        [InlineData("{n}", new[] { "UnknownPlaceholder {n}", "MissingCounter" })]
        [InlineData("{ N }", new[] { "UnknownPlaceholder { N }", "MissingCounter" })]
        public void CaseAndSpacesMatter(string text, string[] expected)
        {
            Assert.Equal(expected, Describe(TemplateParser.Parse(text)));
        }

        [Fact]
        public void EmptyPlaceholder()
        {
            Assert.Equal(new[] { "UnknownPlaceholder {}", "MissingCounter" }, Describe(TemplateParser.Parse("{}")));
        }

        [Theory]
        [InlineData("Д-{Уровень}-")]
        [InlineData("")]
        [InlineData(null)]
        public void MissingCounter(string text)
        {
            var result = TemplateParser.Parse(text);

            Assert.Equal(new[] { "MissingCounter" }, Describe(result));
            Assert.Empty(result.Issues[0].Arguments);
            Assert.Null(result.Template);
        }

        [Theory]
        [InlineData("{N:00a}")]
        [InlineData("{N:}")]
        [InlineData("{N:5}")]
        [InlineData("{N: 000}")]
        [InlineData("{N:0000000000}")]
        public void InvalidWidth(string text)
        {
            var result = TemplateParser.Parse(text);

            Assert.Equal(new[] { "InvalidWidth " + text }, Describe(result));
            Assert.False(result.IsValid);
        }

        [Fact]
        public void WidthBoundary_MaxWidthIsValid()
        {
            var result = TemplateParser.Parse("{N:" + new string('0', TemplateParameters.CounterMaxWidth) + "}");

            Assert.Equal(9, TemplateParameters.CounterMaxWidth);
            Assert.True(result.IsValid);
            Assert.Equal(9, result.Template.CounterWidth);
        }

        [Theory]
        [InlineData("{N:00}-{N:000}", "{N:000}")]
        [InlineData("{N}-{N:0}", "{N:0}")]
        public void DifferentCounterPlaceholders(string text, string argument)
        {
            Assert.Equal(new[] { "InvalidWidth " + argument }, Describe(TemplateParser.Parse(text)));
        }

        [Fact]
        public void IssueOrderAndRepeats()
        {
            var result = TemplateParser.Parse("{X}-{Y}-{X}");

            Assert.Equal(new[] { "UnknownPlaceholder {X}", "UnknownPlaceholder {Y}", "MissingCounter" },
                Describe(result));
        }

        [Fact]
        public void FormatExceptions()
        {
            var withLevel = Template("Д-{Уровень}-{N}");
            var withoutLevel = Template("Д-{N}");

            Assert.Throws<ArgumentNullException>(() => withLevel.Format(null, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => withLevel.Format("1", 0));
            Assert.Equal("Д-1", withoutLevel.Format("X", 1));
            Assert.Equal("Д--1", withLevel.Format(string.Empty, 1));
        }

        [Fact]
        public void WarningFlag()
        {
            foreach (TemplateIssueCode code in Enum.GetValues(typeof(TemplateIssueCode)))
            {
                var issue = new TemplateIssue(code, null);

                Assert.Equal(code == TemplateIssueCode.WidthTooSmall, issue.IsWarning);
                Assert.Empty(issue.Arguments);
            }
        }
    }
}
