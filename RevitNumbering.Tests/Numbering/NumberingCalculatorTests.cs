using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;

namespace RevitNumbering.Tests.Numbering
{
    public class NumberingCalculatorTests
    {
        private static readonly List<LevelInfo> TwoLevels = new List<LevelInfo>
        {
            // Not in elevation order on purpose.
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

            Preview preview = Make.Calculate(items, TwoLevels);

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

            Preview preview = Make.Calculate(items, TwoLevels, Make.Settings(includeLevel: false));

            Problem problem = Assert.Single(preview.Errors);
            Assert.Equal(ProblemKind.NeedLevel, problem.Kind);
            Assert.Equal(new[] { "01", "02", "" }, problem.Levels);
            Assert.All(preview.Rows, row => Assert.Equal("", row.NewValue));
            Assert.False(preview.CanApply);
        }

        [Fact]
        public void OneLevelWithoutLevelPlaceholder_IsNumbered()
        {
            var items = new List<NumberingItem> { Make.Item(1, 10, 0, 0), Make.Item(2, 10, 1000, 0) };

            Preview preview = Make.Calculate(items, TwoLevels, Make.Settings(includeLevel: false));

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

            Preview preview = Make.Calculate(items, TwoLevels);

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

            Preview preview = Make.Calculate(items, TwoLevels, scope: new long[] { 1, 2 });

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

            Preview preview = Make.Calculate(items, TwoLevels);

            Assert.Equal("Д-01-001", NewValue(preview, 1));
            Assert.Equal("Д-01-003", NewValue(preview, 3));
            Assert.Equal(RowStatus.Locked, Row(preview, 2).Status);
            Assert.Equal(LockReason.OwnedByOther, Row(preview, 2).Lock);
            Assert.Equal("", NewValue(preview, 2));
            SkippedNumber skipped = Assert.Single(preview.Skipped);
            Assert.Equal("Д-01-002", skipped.Value);
            Assert.Equal(2, skipped.OccupiedById);
        }

        [Fact]
        public void OccupiedNumbers_AreSearchedInsideTheConflictGroup()
        {
            // Rooms: a number is used only by rooms of the same phase.
            var holder = Make.Item(9, 10, 9000, 0, "0101");
            holder.ConflictGroup = "phase A";
            var samePhase = Make.Item(1, 10, 0, 0);
            samePhase.ConflictGroup = "phase A";
            var otherPhase = Make.Item(2, 10, 0, 0);
            otherPhase.ConflictGroup = "phase B";
            var settings = Make.Settings(prefix: "", separator: "", width: 2);

            Preview same = Make.Calculate(
                new List<NumberingItem> { holder, samePhase }, TwoLevels, settings, new long[] { 1 });
            Preview other = Make.Calculate(
                new List<NumberingItem> { holder, otherPhase }, TwoLevels, settings, new long[] { 2 });

            Assert.Equal("0102", NewValue(same, 1));
            Assert.Single(same.Skipped);
            Assert.Equal("0101", NewValue(other, 2));
            Assert.Empty(other.Skipped);
        }


        [Fact]
        public void LevelNameRunningIntoTheNumber_DuplicateNumber()
        {
            // "1" + "11" and "11" + "1" give the same "111".
            var levels = new List<LevelInfo> { Make.Level(10, "1", 0), Make.Level(20, "11", 3000) };
            List<NumberingItem> items = Enumerable.Range(1, 11).Select(i => Make.Item(i, 10, i * 1000, 0)).ToList();
            items.Add(Make.Item(100, 20, 0, 0));

            Preview preview = Make.Calculate(items, levels, Make.Settings(prefix: "", separator: "", width: 1));

            Problem problem = Assert.Single(preview.Errors);
            Assert.Equal(ProblemKind.DuplicateNumber, problem.Kind);
            Assert.Equal("111", problem.Text);
            Assert.Equal(new[] { "1", "11" }, problem.Levels);
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

            Preview preview = Make.Calculate(items, TwoLevels);

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

            Preview preview = Make.Calculate(items, TwoLevels);

            Assert.Equal(RowStatus.Locked, Row(preview, 1).Status);
            Assert.Equal(LockReason.NotOrderable, Row(preview, 1).Lock);
            Assert.Equal("Д-01-002", NewValue(preview, 2));
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
                return Make.Calculate(Items(outsideValue, lockOfSecond), TwoLevels, scope: new long[] { 1, 2 });
            }

            Preview shown = Calculate("", LockReason.None);

            Assert.True(shown.SameAs(Calculate("", LockReason.None)));
            // An element outside the scope took a number: the new values moved.
            Assert.False(shown.SameAs(Calculate("Д-01-001", LockReason.None)));
            // An element became locked: the status changed.
            Assert.False(shown.SameAs(Calculate("", LockReason.ReadOnly)));
            // A value outside the scope that takes no number changes nothing.
            Assert.True(shown.SameAs(Calculate("other", LockReason.None)));
        }
    }
}
