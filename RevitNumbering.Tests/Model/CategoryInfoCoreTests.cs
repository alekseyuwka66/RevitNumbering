// Exercise SettingsStore.Defaults, FixClassifier and NumberingEngine,
// GridTraversal and the template parser. They are compiled only when those modules are present:
// remove this guard (the #if line and the closing #endif) once the dependent modules are merged.
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Ordering;
using RevitNumbering.Core.Settings;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Tests.Model
{
    /// <summary>
    /// The core works by CategoryInfo data for any category,
    /// including an unknown one.
    /// </summary>
    public class CategoryInfoCoreTests
    {
        private static readonly DocumentKey Doc = new DocumentKey("doc-1");

        private static readonly CategoryInfo Rooms =
            new CategoryInfo("Rooms", "Помещения", "{Уровень}{N:00}", 3000, ConflictScope.Phase, true);

        private static readonly CategoryInfo Equipment =
            new CategoryInfo("Equipment", "Оборудование", "ОБ-{Уровень}-{N:000}", 700, ConflictScope.Document, false);

        private static readonly CategoryInfo Doors =
            new CategoryInfo("Doors", "Двери", "Д-{Уровень}-{N:000}", 500, ConflictScope.Document, false);

        private static readonly LevelData Level1 = new LevelData("L1", "1", 0, null);

        private static ElementData Element(long id, double? xmm = null, double? ymm = null,
            string currentValue = "", string phaseKey = null, bool hasLocation = true, double areaSqM = 20,
            bool hasBoundaries = true)
        {
            return new ElementData(id, "E" + id, "L1", "1", currentValue, xmm, ymm, phaseKey,
                false, false, null, hasLocation, areaSqM, hasBoundaries);
        }

        private static NumberingPlan Compute(CategoryInfo category, IReadOnlyList<ElementData> elements,
            IReadOnlyCollection<long> scopeIds, string template, double toleranceMm, ConflictStrategy strategy)
        {
            var snapshot = new CategorySnapshot(Doc, 1, category, elements, new[] { Level1 }, false);
            var order = new TraversalOrder("grid", new Dictionary<string, string>
            {
                { "corner", "TopLeft" },
                { "direction", "Rows" }
            });
            var input = new NumberingInput(snapshot, scopeIds, template,
                new LevelCodeRule(LevelCodeSource.LevelName, "", LevelCodePart.Whole, 1), order, toleranceMm,
                strategy, false);
            var registry = new TraversalRegistry(new ITraversalStrategy[] { new GridTraversal() });
            return NumberingEngine.Compute(input, registry);
        }

        private static PlannedRow Row(NumberingPlan plan, long id)
        {
            return plan.Rows.Single(row => row.Id == id);
        }

        [Fact]
        public void DefaultsOfUnknownCategory()
        {
            CategorySettings defaults = SettingsStore.Defaults(Equipment);

            Assert.Equal("ОБ-{Уровень}-{N:000}", defaults.Template);
            Assert.Equal(700, defaults.RowToleranceMm);
            Assert.Equal("LevelName", defaults.LevelCodeSource);
            Assert.Equal("", defaults.LevelParameterName);
            Assert.Equal("Whole", defaults.LevelCodePart);
            Assert.Equal(1, defaults.LevelWordNumber);
            Assert.Equal("SkipOccupied", defaults.ConflictStrategy);
            Assert.False(defaults.AppendMode);
            Assert.Null(defaults.Traversal);
        }

        [Fact]
        public void ComputeForUnknownCategory()
        {
            var elements = new[] { Element(1, 1000, 0), Element(2, 3000, 0) };

            NumberingPlan plan = Compute(Equipment, elements, new long[] { 1, 2 }, "ОБ-{Уровень}-{N:000}", 700,
                ConflictStrategy.SkipOccupied);

            Assert.Equal("ОБ-1-001", Row(plan, 1).NewValue);
            Assert.Equal("ОБ-1-002", Row(plan, 2).NewValue);
            Assert.Equal(RowStatus.WillChange, Row(plan, 1).Status);
            Assert.Equal(RowStatus.WillChange, Row(plan, 2).Status);
            Assert.Equal(2, plan.WillChangeCount);
            Assert.True(plan.CanApply);
        }

        [Theory]
        [InlineData(false, 0d, false, FixReason.RoomNotPlaced)]
        [InlineData(true, 0d, false, FixReason.RoomNotEnclosed)]
        [InlineData(true, 0d, true, FixReason.RoomRedundant)]
        [InlineData(true, 12.5, true, FixReason.None)]
        public void FourRoomStates(bool hasLocation, double areaSqM, bool hasBoundaries,
            FixReason expected)
        {
            ElementData room = Element(1, 0, 0, hasLocation: hasLocation, areaSqM: areaSqM,
                hasBoundaries: hasBoundaries);

            Assert.Equal(expected, FixClassifier.Classify(room, Rooms, true));
        }

        [Fact]
        public void DefaultFactsOfNonRooms()
        {
            ElementData door = Element(1, 0, 0, hasLocation: true, areaSqM: 0, hasBoundaries: false);

            Assert.Equal(FixReason.None, FixClassifier.Classify(door, Doors, true));
        }

        [Fact]
        public void FixedElementsDoNotShiftCounter()
        {
            var elements = new[]
            {
                Element(1, null, null, hasLocation: false, areaSqM: 0, hasBoundaries: false),
                Element(2, 0, 9000, hasLocation: true, areaSqM: 0, hasBoundaries: false),
                Element(3, 2000, 9000, hasLocation: true, areaSqM: 0, hasBoundaries: true),
                Element(4, 4000, 9000, hasLocation: true, areaSqM: 20, hasBoundaries: true)
            };

            NumberingPlan plan = Compute(Rooms, elements, new long[] { 1, 2, 3, 4 }, "{Уровень}{N:00}", 3000,
                ConflictStrategy.SkipOccupied);

            Assert.Equal(RowStatus.Fixed, Row(plan, 1).Status);
            Assert.Equal(RowStatus.Fixed, Row(plan, 2).Status);
            Assert.Equal(RowStatus.Fixed, Row(plan, 3).Status);
            Assert.Equal(FixReason.RoomNotPlaced, Row(plan, 1).FixReason);
            Assert.Equal(FixReason.RoomNotEnclosed, Row(plan, 2).FixReason);
            Assert.Equal(FixReason.RoomRedundant, Row(plan, 3).FixReason);
            Assert.Equal("", Row(plan, 1).NewValue);
            Assert.Equal("", Row(plan, 2).NewValue);
            Assert.Equal("", Row(plan, 3).NewValue);
            Assert.Equal("101", Row(plan, 4).NewValue);
            Assert.Equal(RowStatus.WillChange, Row(plan, 4).Status);
            Assert.Equal(1, plan.WillChangeCount);
        }

        [Theory]
        [InlineData(ConflictStrategy.SkipOccupied)]
        [InlineData(ConflictStrategy.AllowDuplicates)]
        public void SameNumberInAnotherPhaseIsNotConflict(ConflictStrategy strategy)
        {
            var elements = new[]
            {
                Element(1, 1000, 0, phaseKey: "P2"),
                Element(2, 5000, 0, currentValue: "101", phaseKey: "P1")
            };

            NumberingPlan plan = Compute(Rooms, elements, new long[] { 1 }, "{Уровень}{N:00}", 3000, strategy);

            Assert.Equal("101", Row(plan, 1).NewValue);
            Assert.Equal(RowStatus.WillChange, Row(plan, 1).Status);
            Assert.Equal(0, plan.ConflictCount);
            Assert.Empty(plan.SkippedNumbers);
            Assert.Equal(RowStatus.OutOfScope, Row(plan, 2).Status);
        }

        [Fact]
        public void SamePhaseSkipOccupied()
        {
            var elements = new[]
            {
                Element(1, 1000, 0, phaseKey: "P2"),
                Element(2, 5000, 0, currentValue: "101", phaseKey: "P2")
            };

            NumberingPlan plan = Compute(Rooms, elements, new long[] { 1 }, "{Уровень}{N:00}", 3000,
                ConflictStrategy.SkipOccupied);

            Assert.Equal("102", Row(plan, 1).NewValue);
            SkippedNumber skipped = Assert.Single(plan.SkippedNumbers);
            Assert.Equal("101", skipped.Value);
            Assert.Equal(2, skipped.OccupiedById);
            Assert.Equal(1, plan.ConflictCount);
        }

        [Fact]
        public void SamePhaseAllowDuplicates()
        {
            var elements = new[]
            {
                Element(1, 1000, 0, phaseKey: "P2"),
                Element(2, 5000, 0, currentValue: "101", phaseKey: "P2")
            };

            NumberingPlan plan = Compute(Rooms, elements, new long[] { 1 }, "{Уровень}{N:00}", 3000,
                ConflictStrategy.AllowDuplicates);

            Assert.Equal("101", Row(plan, 1).NewValue);
            Assert.Equal(RowStatus.Conflict, Row(plan, 1).Status);
            Assert.Equal(1, plan.ConflictCount);
        }
    }
}
