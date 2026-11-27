using RevitNumbering.Core.Settings;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Tests.Templates
{
    public class TemplateTests
    {
        [Fact]
        public void LevelFirst_PrefixLevelSeparatorCounterSuffix()
        {
            Template template = Template.From(Make.Settings(suffix: "/А"));

            Assert.True(template.HasLevel);
            Assert.Equal(3, template.Width);
            Assert.Equal("Д-01-007/А", template.Format("01", 7));
        }

        [Fact]
        public void CounterFirst_SwapsLevelAndCounter()
        {
            Template template = Template.From(Make.Settings(levelFirst: false, suffix: "/А"));

            Assert.Equal("Д-007-01/А", template.Format("01", 7));
        }

        [Fact]
        public void WithoutLevel_SeparatorAndOrderAreNotUsed()
        {
            foreach (bool levelFirst in new[] { true, false })
            {
                Template template = Template.From(Make.Settings(includeLevel: false, levelFirst: levelFirst));

                Assert.False(template.HasLevel);
                Assert.Equal("Д-007", template.Format("01", 7));
            }
        }

        [Fact]
        public void Width_IsTheNumberOfDigits_AndNeverCutsTheNumber()
        {
            Template one = Template.From(Make.Settings(prefix: "", includeLevel: false, width: 1));
            Template two = Template.From(Make.Settings(prefix: "", includeLevel: false, width: 2));

            Assert.Equal("12", one.Format(null, 12));
            Assert.Equal("7", one.Format(null, 7));
            Assert.Equal("07", two.Format(null, 7));
            Assert.Equal("150", two.Format(null, 150));
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(9, true)]
        [InlineData(10, false)]
        public void Width_MustBeFromOneToNine(int width, bool valid)
        {
            Assert.Equal(valid, Make.Settings(width: width).IsValid());
        }

        [Fact]
        public void TextPartsMustBeSet()
        {
            CategorySettings settings = Make.Settings();
            settings.Prefix = null;

            Assert.False(settings.IsValid());
        }
    }
}
