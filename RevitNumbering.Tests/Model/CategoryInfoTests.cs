using RevitNumbering.Core.Model;

namespace RevitNumbering.Tests.Model
{
    /// <summary>
    /// CategoryInfo data and argument checks.
    /// </summary>
    public class CategoryInfoTests
    {
        private const string RoomsTemplate = "{Уровень}{N:00}";

        [Fact]
        public void CategoryInfoKeepsData()
        {
            var rooms = new CategoryInfo("Rooms", "Помещения", RoomsTemplate, 3000, ConflictScope.Phase, true);

            Assert.Equal("Rooms", rooms.Key);
            Assert.Equal("Помещения", rooms.DisplayName);
            Assert.Equal("{Уровень}{N:00}", rooms.DefaultTemplate);
            Assert.Equal(3000, rooms.DefaultRowToleranceMm);
            Assert.Equal(ConflictScope.Phase, rooms.ConflictScope);
            Assert.True(rooms.HasSpatialStates);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void InvalidKey(string key)
        {
            Assert.ThrowsAny<ArgumentException>(
                () => new CategoryInfo(key, "Помещения", RoomsTemplate, 3000, ConflictScope.Phase, true));
        }

        [Fact]
        public void InvalidDisplayName()
        {
            Assert.Throws<ArgumentException>(
                () => new CategoryInfo("Rooms", "", RoomsTemplate, 3000, ConflictScope.Phase, true));
        }

        [Fact]
        public void NullTemplate()
        {
            Assert.Throws<ArgumentNullException>(
                () => new CategoryInfo("Rooms", "Помещения", null, 3000, ConflictScope.Phase, true));
        }

        [Theory]
        [InlineData(0d)]
        [InlineData(0.5)]
        [InlineData(100001d)]
        [InlineData(double.NaN)]
        public void ToleranceOutOfRange(double toleranceMm)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CategoryInfo("Rooms", "Помещения", RoomsTemplate, toleranceMm, ConflictScope.Phase, true));
        }

        [Theory]
        [InlineData(1d)]
        [InlineData(100000d)]
        public void ToleranceBoundsAreAllowed(double toleranceMm)
        {
            var rooms = new CategoryInfo("Rooms", "Помещения", RoomsTemplate, toleranceMm, ConflictScope.Phase, true);

            Assert.Equal(toleranceMm, rooms.DefaultRowToleranceMm);
        }
    }
}
