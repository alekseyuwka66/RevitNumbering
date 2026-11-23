using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Texts;
using static RevitNumbering.Tests.Numbering.NumberingTestData;

namespace RevitNumbering.Tests.Numbering
{
    /// <summary>Fix classification and its texts.</summary>
    public class FixClassifierTests
    {
        [Fact]
        public void Priority_OwnedFirst()
        {
            ElementData element = El(1, null, owner: "Иванов", readOnly: true);

            Assert.Equal(FixReason.OwnedByOtherUser, FixClassifier.Classify(element, Doors, true));
        }

        [Fact]
        public void Priority_ReadOnlyBeforeLevel()
        {
            ElementData element = El(1, null, readOnly: true);

            Assert.Equal(FixReason.ReadOnlyParameter, FixClassifier.Classify(element, Doors, true));
        }

        [Theory]
        [InlineData(false, 0d, false, FixReason.RoomNotPlaced)]
        [InlineData(true, 0d, false, FixReason.RoomNotEnclosed)]
        [InlineData(true, 0d, true, FixReason.RoomRedundant)]
        [InlineData(true, 12.5, true, FixReason.None)]
        public void RoomStates(bool hasLocation, double area, bool hasBoundaries, FixReason expected)
        {
            ElementData room = El(1, L1, hasLocation: hasLocation, area: area, hasBoundaries: hasBoundaries);

            Assert.Equal(expected, FixClassifier.Classify(room, Rooms, false));
        }

        [Fact]
        public void States_OnlyWithSpatialStates()
        {
            ElementData door = El(1, L1, hasLocation: false, area: 0, hasBoundaries: false);

            Assert.Equal(FixReason.None, FixClassifier.Classify(door, Doors, true));
        }

        [Fact]
        public void NoLevel()
        {
            Assert.Equal(FixReason.NoLevel, FixClassifier.Classify(El(1, null), Doors, true));
            Assert.Equal(FixReason.None, FixClassifier.Classify(El(1, null), Doors, false));

            ElementData notPlaced = El(1, null, hasLocation: false, area: 0, hasBoundaries: false);
            Assert.Equal(FixReason.RoomNotPlaced, FixClassifier.Classify(notPlaced, Rooms, true));
        }

        [Fact]
        public void Classify_NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => FixClassifier.Classify(null, Doors, true));
            Assert.Throws<ArgumentNullException>(() => FixClassifier.Classify(El(1, L1), null, true));
        }

        [Fact]
        public void EveryStatusAndReason_HasText()
        {
            var expectedStatus = new Dictionary<RowStatus, string>
            {
                { RowStatus.WillChange, "Изменится" },
                { RowStatus.Unchanged, "Без изменений" },
                { RowStatus.Fixed, "Зафиксирован: нет уровня" },
                { RowStatus.Conflict, "Конфликт" },
                { RowStatus.OutOfScope, "Вне охвата" }
            };
            foreach (RowStatus status in Enum.GetValues(typeof(RowStatus)))
            {
                FixReason reason = status == RowStatus.Fixed ? FixReason.NoLevel : FixReason.None;
                Assert.Equal(expectedStatus[status], TextCatalog.RowStatusText(status, reason, null));
            }

            var expectedReason = new Dictionary<FixReason, string>
            {
                { FixReason.OwnedByOtherUser, "занят Петров" },
                { FixReason.ReadOnlyParameter, "параметр только для чтения" },
                { FixReason.RoomNotPlaced, "Не размещено" },
                { FixReason.RoomNotEnclosed, "Не окружено" },
                { FixReason.RoomRedundant, "Избыточное" },
                { FixReason.NoLevel, "нет уровня" }
            };
            foreach (FixReason reason in Enum.GetValues(typeof(FixReason)))
            {
                if (reason != FixReason.None)
                {
                    Assert.Equal(expectedReason[reason], TextCatalog.FixReasonText(reason, "Петров"));
                }
            }
        }
    }
}
