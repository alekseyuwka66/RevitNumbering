using RevitNumbering.Core.Templates;

namespace RevitNumbering.Tests.Templates
{
    public class LevelCodeTests
    {
        [Theory]
        [InlineData(LevelCodePart.Whole, 1, "01 этаж", "01 этаж")]
        [InlineData(LevelCodePart.Word, 1, "01 этаж", "01")]
        [InlineData(LevelCodePart.Word, 2, "01_этаж", "этаж")]
        [InlineData(LevelCodePart.Word, 2, "Корпус  А_03", "А")]
        [InlineData(LevelCodePart.LastWord, 1, "Этаж 2_Б", "Б")]
        [InlineData(LevelCodePart.LastWord, 1, "Кровля", "Кровля")]
        public void Resolve_TakesThePartOfTheName(LevelCodePart part, int wordNumber, string name, string expected)
        {
            var code = new LevelCode { Part = part, WordNumber = wordNumber };

            Assert.Equal(expected, code.Resolve(name));
        }

        [Theory]
        [InlineData(LevelCodePart.Word, 3, "01 этаж")]
        [InlineData(LevelCodePart.Word, 0, "01 этаж")]
        [InlineData(LevelCodePart.Whole, 1, "")]
        [InlineData(LevelCodePart.LastWord, 1, " _ ")]
        public void Resolve_ReturnsNull_WhenThePartIsMissing(LevelCodePart part, int wordNumber, string name)
        {
            var code = new LevelCode { Part = part, WordNumber = wordNumber };

            Assert.Null(code.Resolve(name));
        }
    }
}
