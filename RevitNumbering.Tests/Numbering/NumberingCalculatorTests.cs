using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Tests.Numbering
{
    public class NumberingCalculatorTests
    {
        private static readonly List<LevelInfo> TwoLevels = new List<LevelInfo>
        {
            // Given not in elevation order on purpose.
            Make.Level(20, "02", 3000),
            Make.Level(10, "01", 0),
        };

        private static string NewValue(Preview preview, long id)
        {
            return preview.Rows.Single(row => row.Item.Id == id).NewValue;
        }

        private static PreviewRow Row(Preview preview, long id)
        {
            return preview.Rows.Single(row => row.Item.Id == id);
        }

        [Fact]
        public void Counter_StartsAgainOnEveryLevel()
        {
            var items = new List<NumberingItem>
            {
                Make.Item(1, 20, 0, 0),
                Make.Item(2, 20, 1000, 0),
                Make.Item(3, 10, 1000, 0),
                Make.Item(4, 10, 0, 0),
            };

            Preview preview = NumberingCalculator.Calculate(Make.Request(items, TwoLevels));

            Assert.Empty(preview.Errors);
            Assert.Equal("Д-01-001", NewValue(preview, 4));
            Assert.Equal("Д-01-002", NewValue(preview, 3));
            Assert.Equal("Д-02-001", NewValue(preview, 1));
            Assert.Equal("Д-02-002", NewValue(preview, 2));
            Assert.Equal(4, preview.ChangeCount);
            Assert.True(preview.CanApply);
        }

        [Fact]
        public void SeveralLevelsWithoutLevelPlaceholder_NeedLevel_NoLevelGroupIsLast()
        {
            var items = new List<NumberingItem>
            {
                Make.Item(1, null, 0, 0),
                Make.Item(2, 20, 0, 0),
                Make.Item(3, 10, 0, 0),
            };

            Preview preview = NumberingCalculator.Calculate(Make.Request(items, TwoLevels, Make.Settings("Д-{N:000}")));

            Problem problem = Assert.Single(preview.Errors);
            Assert.Equal(ProblemCode.NeedLevel, problem.Code);
            Assert.Equal(new[] { "01", "02", "" }, problem.Args);
            Assert.All(preview.Rows, row => Assert.Equal("", row.NewValue));
            Assert.False(preview.CanApply);
        }

        [Fact]
        public void OneLevelWithoutLevelPlaceholder_IsNumbered()
        {
            var items = new List<NumberingItem> { Make.Item(1, 10, 0, 0), Make.Item(2, 10, 1000, 0) };

            Preview preview = NumberingCalculator.Calculate(Make.Request(items, TwoLevels, Make.Settings("Д-{N:000}")));

            Assert.Empty(preview.Errors);
            Assert.Equal("Д-001", NewValue(preview, 1));
            Assert.Equal("Д-002", NewValue(preview, 2));
        }

        [Fact]
        public void ElementWithoutLevel_IsLocked_WhenTheTemplateHasLevel()
        {
            var items = new List<NumberingItem>
            {
                Make.Item(1, null, 0, 0, "old"),
                Make.Item(2, 10, 0, 0),
            };

            Preview preview = NumberingCalculator.Calculate(Make.Request(items, TwoLevels));

            Assert.Equal(RowStatus.Locked, Row(preview, 1).Status);
            Assert.Equal(LockReason.NoLevel, Row(preview, 1).Lock);
            Assert.Equal("", NewValue(preview, 1));
            Assert.Equal("Д-01-001", NewValue(preview, 2));
            Assert.Equal(1, preview.LockedCount);
        }

        [Fact]
        public void NumberOccupiedOutOfScope_IsSkipped_AndListed()
        {
            var items = new List<NumberingItem>
            {
                Make.Item(1, 10, 0, 0),
                Make.Item(2, 10, 1000, 0),
                Make.Item(9, 10, 5000, 0, " Д-01-001 "),
            };

            Preview preview = NumberingCalculator.Calculate(
                Make.Request(items, TwoLevels, scope: new long[] { 1, 2 }));

            Assert.Equal("Д-01-002", NewValue(preview, 1));
            Assert.Equal("Д-01-003", NewValue(preview, 2));
            Assert.Equal(RowStatus.OutOfScope, Row(preview, 9).Status);
            SkippedNumber skipped = Assert.Single(preview.Skipped);
            Assert.Equal("Д-01-001", skipped.Value);
            Assert.Equal(9, skipped.OccupiedById);
        }

        [Fact]
        public void NumberOfLockedElement_IsSkipped_AndTheElementKeepsItsValue()
        {
            var locked = Make.Item(2, 10, 1000, 0, "Д-01-002");
            locked.Lock = LockReason.OwnedByOther;
            var items = new List<NumberingItem>
            {
                Make.Item(1, 10, 0, 0),
                locked,
                Make.Item(3, 10, 2000, 0),
            };

            Preview preview = NumberingCalculator.Calculate(Make.Request(items, TwoLevels));

            Assert.Equal("Д-01-001", NewValue(preview, 1));
            Assert.Equal("Д-01-003", NewValue(preview, 3));
            Assert.Equal(RowStatus.Locked, Row(preview, 2).Status);
            Assert.Equal(LockReason.OwnedByOther, Row(preview, 2).Lock);
            Assert.Equal("", NewValue(preview, 2));
            Assert.Equal(2, Assert.Single(preview.Skipped).OccupiedById);
        }

        [Fact]
        public void OccupiedNumbers_AreSearchedInsideTheConflictGroup()
        {
            // Rooms: a number is occupied only for rooms of the same phase.
            var holder = Make.Item(9, 10, 9000, 0, "0101");
            holder.ConflictGroup = "phase A";
            var samePhase = Make.Item(1, 10, 0, 0);
            samePhase.ConflictGroup = "phase A";
            var otherPhase = Make.Item(2, 10, 0, 0);
            otherPhase.ConflictGroup = "phase B";
            var settings = Make.Settings("{Уровень}{N:00}");

            Preview same = NumberingCalculator.Calculate(Make.Request(
                new List<NumberingItem> { holder, samePhase }, TwoLevels, settings, new long[] { 1 }));
            Preview other = NumberingCalculator.Calculate(Make.Request(
                new List<NumberingItem> { holder, otherPhase }, TwoLevels, settings, new long[] { 2 }));

            Assert.Equal("0102", NewValue(same, 1));
            Assert.Single(same.Skipped);
            Assert.Equal("0101", NewValue(other, 2));
            Assert.Empty(other.Skipped);
        }

        [Fact]
        public void LevelNameWithoutRequestedPart_LevelPartMissing()
        {
            var levels = new List<LevelInfo> { Make.Level(10, "01 этаж", 0), Make.Level(20, "Кровля", 3000) };
            var items = new List<NumberingItem> { Make.Item(1, 10, 0, 0), Make.Item(2, 20, 0, 0) };

            Preview preview = NumberingCalculator.Calculate(Make.Request(items, levels,
                Make.Settings(part: LevelCodePart.Word, wordNumber: 2)));

            Problem problem = Assert.Single(preview.Errors);
            Assert.Equal(ProblemCode.LevelPartMissing, problem.Code);
            Assert.Equal(new[] { "Кровля" }, problem.Args);
            Assert.All(preview.Rows, row => Assert.Equal("", row.NewValue));
        }

        [Fact]
        public void EqualLevelCodes_DuplicateInScope()
        {
            var levels = new List<LevelInfo> { Make.Level(10, "01 этаж", 0), Make.Level(20, "01 антресоль", 1500) };
            var items = new List<NumberingItem>
            {
                Make.Item(1, 10, 0, 0),
                Make.Item(2, 10, 1000, 0),
                Make.Item(3, 20, 0, 0),
                Make.Item(4, 20, 1000, 0),
            };

            Preview preview = NumberingCalculator.Calculate(Make.Request(items, levels,
                Make.Settings(part: LevelCodePart.Word, wordNumber: 1)));

            Problem problem = Assert.Single(preview.Errors);
            Assert.Equal(ProblemCode.DuplicateInScope, problem.Code);
            Assert.Equal(new[] { "Д-01-001", "01 этаж", "01 антресоль" }, problem.Args);
            Assert.False(preview.CanApply);
        }

        [Fact]
        public void UnchangedValue_IsNoChange_AndIsNotWritten()
        {
            var items = new List<NumberingItem>
            {
                Make.Item(1, 10, 0, 0, "Д-01-001"),
                Make.Item(2, 10, 1000, 0, "old"),
            };

            Preview preview = NumberingCalculator.Calculate(Make.Request(items, TwoLevels));

            Assert.Equal(RowStatus.NoChange, Row(preview, 1).Status);
            Assert.Equal(RowStatus.WillChange, Row(preview, 2).Status);
            Assert.Equal(1, preview.ChangeCount);
            WriteItem write = Assert.Single(preview.ToWrite());
            Assert.Equal(2, write.Id);
            Assert.Equal("Д-01-002", write.Value);
        }

        [Fact]
        public void ElementWithoutPoint_IsLockedAsNotOrderable_AndKeepsItsNumberOccupied()
        {
            var items = new List<NumberingItem>
            {
                Make.Item(1, 10, null, null, "Д-01-001"),
                Make.Item(2, 10, 0, 0),
            };

            Preview preview = NumberingCalculator.Calculate(Make.Request(items, TwoLevels));

            Assert.Equal(RowStatus.Locked, Row(preview, 1).Status);
            Assert.Equal(LockReason.NotOrderable, Row(preview, 1).Lock);
            Assert.Equal("Д-01-002", NewValue(preview, 2));
        }

        [Fact]
        public void TemplateError_LeavesEveryNewValueEmpty()
        {
            var items = new List<NumberingItem> { Make.Item(1, 10, 0, 0), Make.Item(2, 10, 1000, 0) };

            Preview preview = NumberingCalculator.Calculate(
                Make.Request(items, TwoLevels, Make.Settings("Д-{Уровнь}-{N}"), new long[] { 1 }));

            Assert.Equal(ProblemCode.UnknownPlaceholder, Assert.Single(preview.Errors).Code);
            Assert.All(preview.Rows, row => Assert.Equal("", row.NewValue));
            Assert.Equal(RowStatus.OutOfScope, Row(preview, 2).Status);
            Assert.Equal(0, preview.ChangeCount);
            Assert.False(preview.CanApply);
        }

        [Fact]
        public void RepeatedCalculation_GivesTheSameResult()
        {
            List<NumberingItem> items = Enumerable.Range(1, 200)
                .Select(i => Make.Item(i, i % 2 == 0 ? 10 : 20, (i * 7919) % 5000, (i * 104729) % 9000))
                .ToList();

            Preview first = NumberingCalculator.Calculate(Make.Request(items, TwoLevels));
            Preview second = NumberingCalculator.Calculate(Make.Request(items, TwoLevels));

            Assert.True(first.SameAs(second));
            Assert.Equal(
                first.Rows.Select(row => row.NewValue),
                second.Rows.Select(row => row.NewValue));
        }

        [Fact]
        public void SameAs_SeesChangedNewValuesAndStatuses()
        {
            List<NumberingItem> Items(string outsideValue, LockReason lockOfSecond)
            {
                var second = Make.Item(2, 10, 1000, 0);
                second.Lock = lockOfSecond;
                return new List<NumberingItem>
                {
                    Make.Item(1, 10, 0, 0),
                    second,
                    Make.Item(9, 10, 5000, 0, outsideValue),
                };
            }

            Preview Calculate(string outsideValue, LockReason lockOfSecond)
            {
                return NumberingCalculator.Calculate(
                    Make.Request(Items(outsideValue, lockOfSecond), TwoLevels, scope: new long[] { 1, 2 }));
            }

            Preview shown = Calculate("", LockReason.None);

            Assert.True(shown.SameAs(Calculate("", LockReason.None)));
            // An element outside the scope took a number: new values moved.
            Assert.False(shown.SameAs(Calculate("Д-01-001", LockReason.None)));
            // An element became locked: the status changed.
            Assert.False(shown.SameAs(Calculate("", LockReason.ReadOnly)));
            // A value outside the scope that takes no number changes nothing.
            Assert.True(shown.SameAs(Calculate("other", LockReason.None)));
        }
    }
}
