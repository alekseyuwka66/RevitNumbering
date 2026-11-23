using RevitNumbering.Core.Templates;
using static RevitNumbering.Tests.Templates.TemplateTestData;

namespace RevitNumbering.Tests.Templates
{
    /// <summary>
    /// Level code.
    /// </summary>
    public class LevelCodeRuleTests
    {
        private const string FtLevel = "01 этаж +0.000";
        private const string CodeParameter = "Код этажа";

        [Fact]
        public void FtExample_FirstWord()
        {
            var rule = new LevelCodeRule(LevelCodeSource.LevelName, "", LevelCodePart.WordN, 1);

            Assert.Equal("01", rule.Resolve(Level(FtLevel)));
        }

        [Theory]
        [InlineData(LevelCodePart.WordN, 2, "этаж")]
        [InlineData(LevelCodePart.WordN, 3, "+0.000")]
        [InlineData(LevelCodePart.WordN, 4, null)]
        [InlineData(LevelCodePart.LastWord, 1, "+0.000")]
        [InlineData(LevelCodePart.Whole, 1, FtLevel)]
        public void NameParts(LevelCodePart part, int wordNumber, string expected)
        {
            Assert.Equal(expected, NameRule(part, wordNumber).Resolve(Level(FtLevel)));
        }

        [Theory]
        [InlineData("Этаж__3 _A", LevelCodePart.WordN, "3")]
        [InlineData("Этаж__3 _A", LevelCodePart.LastWord, "A")]
        [InlineData("Этаж-3", LevelCodePart.WordN, null)]
        [InlineData("Этаж\t3", LevelCodePart.WordN, null)]
        [InlineData("Этаж 3", LevelCodePart.WordN, null)]
        public void Separators(string name, LevelCodePart part, string expected)
        {
            Assert.Equal(expected, NameRule(part, 2).Resolve(Level(name)));
        }

        [Theory]
        [InlineData(CodeParameter, LevelCodePart.Whole, "Э1 Б")]
        [InlineData(CodeParameter, LevelCodePart.WordN, "Б")]
        [InlineData("код этажа", LevelCodePart.Whole, null)]
        [InlineData("Отметка", LevelCodePart.Whole, null)]
        [InlineData("", LevelCodePart.Whole, null)]
        public void LevelParameterSource(string parameterName, LevelCodePart part, string expected)
        {
            var level = Level("Этаж 1", parameters: new Dictionary<string, string> { { CodeParameter, "Э1 Б" } });
            var rule = new LevelCodeRule(LevelCodeSource.LevelParameter, parameterName, part, 2);

            Assert.Equal(expected, rule.Resolve(level));
        }

        [Fact]
        public void EmptyValues()
        {
            var byParameter = new LevelCodeRule(LevelCodeSource.LevelParameter, CodeParameter, LevelCodePart.Whole, 1);

            Assert.Null(NameRule(LevelCodePart.Whole).Resolve(Level("  ")));
            Assert.Null(byParameter.Resolve(Level("Этаж 1",
                parameters: new Dictionary<string, string> { { CodeParameter, "" } })));
            Assert.Null(byParameter.Resolve(Level("Этаж 1",
                parameters: new Dictionary<string, string> { { CodeParameter, "   " } })));
            Assert.Null(NameRule(LevelCodePart.WordN, 0).Resolve(Level("Этаж 1")));
            Assert.Throws<ArgumentNullException>(() => NameRule(LevelCodePart.Whole).Resolve(null));
        }

        [Fact]
        public void Constructor_NullParameterNameIsStoredAsEmpty()
        {
            var rule = new LevelCodeRule(LevelCodeSource.LevelParameter, null, LevelCodePart.WordN, -5);

            Assert.Equal(string.Empty, rule.ParameterName);
            Assert.Equal(-5, rule.WordNumber);
            Assert.Null(rule.Resolve(Level("Этаж 1")));
        }
    }
}
