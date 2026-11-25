using RevitNumbering.UI.ViewModels;

namespace RevitNumbering.UI.Tests
{
    public class RowFilterTests
    {
        private static readonly TableRow Row = new TableRow
        {
            Id = 345612,
            Name = "Дверь_900 : Одностворчатая",
            Level = "Этаж 2",
            Current = "Д-02-015",
            New = "Н-77-999",
            Status = "Изменится",
        };

        [Theory]
        [InlineData("ДВЕРЬ")]
        [InlineData("дверь_900")]
        [InlineData("этаж 2")]
        [InlineData("д-02")]
        public void Search_IgnoresCase(string text)
        {
            Assert.True(RowFilter.Matches(Row, text));
        }

        [Theory]
        [InlineData("4561")]
        [InlineData("створ")]
        [InlineData("таж")]
        [InlineData("-015")]
        public void Search_FindsPartOfIdNameLevelAndCurrentValue(string text)
        {
            Assert.True(RowFilter.Matches(Row, text));
        }

        [Theory]
        [InlineData("Н-77")]
        [InlineData("999")]
        [InlineData("Изменится")]
        public void Search_DoesNotLookAtNewValueAndStatus(string text)
        {
            Assert.False(RowFilter.Matches(Row, text));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void EmptySearch_ShowsEveryRow(string text)
        {
            Assert.True(RowFilter.Matches(Row, text));
        }

        [Fact]
        public void Search_IgnoresSpacesAroundTheText()
        {
            Assert.True(RowFilter.Matches(Row, "  Этаж 2 "));
            Assert.False(RowFilter.Matches(Row, "Этаж 3"));
        }
    }
}
