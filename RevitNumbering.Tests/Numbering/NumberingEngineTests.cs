using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Ordering;
using RevitNumbering.Core.Templates;
using RevitNumbering.Core.Texts;
using static RevitNumbering.Tests.Numbering.NumberingTestData;

namespace RevitNumbering.Tests.Numbering
{
    /// <summary>
    /// NumberingEngine.Compute.
    /// </summary>
    public class NumberingEngineTests
    {
        private static Scenario TwoLevels()
        {
            return new Scenario().Add(Empty(L1, 1, 2, 3)).Add(Empty(L2, 4, 5, 6));
        }

        /// <summary>Data of the case.</summary>
        private static Scenario AllStatuses()
        {
            return new Scenario { Scope = new long[] { 1, 2, 3, 4 } }.Add(
                El(1, L1),
                El(2, L1, "Д-1-002"),
                El(3, L1, "Д-1-050", owner: "Иванов"),
                El(4, L1),
                El(9, L1, "Д-1-003"));
        }

        /// <summary>Data of example 1.</summary>
        internal static Scenario Example1()
        {
            return new Scenario { Scope = Range(101, 105) }
                .Add(Empty(L1, Range(101, 105)))
                .Add(El(201, L1, "Д-1-003"));
        }

        /// <summary>Data of example 2: doors 1…10, door 3 is owned and has Д-1-007.</summary>
        private static Scenario Example2(int doors)
        {
            var scenario = new Scenario();
            for (long id = 1; id <= doors; id++)
            {
                scenario.Add(id == 3 ? El(3, L1, "Д-1-007", owner: "Петров") : El(id, L1));
            }

            return scenario;
        }

        /// <summary>Data of example 3: Д-1-001…004, 006…010 and a new door 11.</summary>
        private static Scenario Example3()
        {
            var scenario = new Scenario();
            foreach (long k in new long[] { 1, 2, 3, 4, 6, 7, 8, 9 })
            {
                scenario.Add(El(k, L1, "Д-1-00" + k));
            }

            return scenario.Add(El(10, L1, "Д-1-010"), El(11, L1));
        }

        [Fact]
        public void CounterStartsOnEveryLevel()
        {
            NumberingPlan plan = TwoLevels().Compute();

            AssertValues(plan, (1, "Д-1-001"), (2, "Д-1-002"), (3, "Д-1-003"),
                (4, "Д-2-001"), (5, "Д-2-002"), (6, "Д-2-003"));
            AssertStatus(plan, RowStatus.WillChange, Range(1, 6));
            Assert.Equal(6, plan.WillChangeCount);
            Assert.Equal(0, plan.ConflictCount);
            Assert.True(plan.CanApply);
        }

        [Fact]
        public void VisibleScope_AfterSearchByLevel()
        {
            Scenario scenario = TwoLevels();
            scenario.Scope = new long[] { 4, 5, 6 };

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (4, "Д-2-001"), (5, "Д-2-002"), (6, "Д-2-003"), (1, ""), (2, ""), (3, ""));
            AssertStatus(plan, RowStatus.OutOfScope, 1, 2, 3);
            Assert.Equal(3, plan.WillChangeCount);
        }

        [Fact]
        public void ScopeChange_IsRecalculatedWithoutState()
        {
            Scenario scenario = TwoLevels();

            scenario.Scope = new long[] { 4, 5, 6 };
            NumberingPlan first = scenario.Compute();
            scenario.Scope = Range(1, 6);
            NumberingPlan second = scenario.Compute();
            scenario.Scope = new long[] { 4, 5, 6 };
            NumberingPlan third = scenario.Compute();

            AssertValues(second, (1, "Д-1-001"), (2, "Д-1-002"), (3, "Д-1-003"),
                (4, "Д-2-001"), (5, "Д-2-002"), (6, "Д-2-003"));
            Assert.Equal(6, second.WillChangeCount);
            for (int i = 0; i < first.Rows.Count; i++)
            {
                Assert.Equal(first.Rows[i].Id, third.Rows[i].Id);
                Assert.Equal(first.Rows[i].NewValue, third.Rows[i].NewValue);
                Assert.Equal(first.Rows[i].Status, third.Rows[i].Status);
                Assert.Equal(first.Rows[i].FixReason, third.Rows[i].FixReason);
            }

            Assert.True(NumberingEngine.SameComposition(first, third));
        }

        [Fact]
        public void AllStatuses_AllowDuplicates()
        {
            Scenario scenario = AllStatuses();
            scenario.Strategy = ConflictStrategy.AllowDuplicates;

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (1, "Д-1-001"), (2, "Д-1-002"), (3, ""), (4, "Д-1-003"), (9, ""));
            AssertStatus(plan, RowStatus.WillChange, 1);
            AssertStatus(plan, RowStatus.Unchanged, 2);
            AssertStatus(plan, RowStatus.Fixed, 3);
            AssertStatus(plan, RowStatus.Conflict, 4);
            AssertStatus(plan, RowStatus.OutOfScope, 9);
            Assert.Equal(FixReason.OwnedByOtherUser, Row(plan, 3).FixReason);
            Assert.Equal("Иванов", Row(plan, 3).OwnerName);
            Assert.Equal(2, plan.WillChangeCount);
            Assert.Equal(1, plan.ConflictCount);
            AssertDuplicate(Assert.Single(plan.Duplicates), "Д-1-003", 4, 9);
            Assert.True(plan.CanApply);
        }

        [Fact]
        public void Statuses_SkipOccupied()
        {
            NumberingPlan plan = AllStatuses().Compute();

            AssertValues(plan, (1, "Д-1-001"), (4, "Д-1-004"));
            AssertStatus(plan, RowStatus.Unchanged, 2);
            AssertStatus(plan, RowStatus.Fixed, 3);
            AssertSkipped(plan, ("Д-1-003", 9));
            Assert.Equal(1, plan.ConflictCount);
            Assert.DoesNotContain(plan.Rows, row => row.Status == RowStatus.Conflict);
            Assert.Empty(plan.Duplicates);
        }

        [Fact]
        public void Example1_OccupiedNumberIsSkipped()
        {
            NumberingPlan plan = Example1().Compute();

            AssertValues(plan, (101, "Д-1-001"), (102, "Д-1-002"), (103, "Д-1-004"), (104, "Д-1-005"),
                (105, "Д-1-006"));
            AssertSkipped(plan, ("Д-1-003", 201));
            Assert.Equal(1, plan.ConflictCount);
            Assert.Equal(5, plan.WillChangeCount);

            SkippedNumber skipped = plan.SkippedNumbers[0];
            Assert.Equal("Д-1-003 занят элементом ID 201 — пропущен",
                TextCatalog.Format(TextCatalog.SkippedLine, skipped.Value, skipped.OccupiedById));
        }

        [Fact]
        public void Example1_AllowDuplicates()
        {
            Scenario scenario = Example1();
            scenario.Strategy = ConflictStrategy.AllowDuplicates;

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (101, "Д-1-001"), (102, "Д-1-002"), (103, "Д-1-003"), (104, "Д-1-004"),
                (105, "Д-1-005"));
            AssertStatus(plan, RowStatus.Conflict, 103);
            AssertStatus(plan, RowStatus.WillChange, 101, 102, 104, 105);
            AssertDuplicate(Assert.Single(plan.Duplicates), "Д-1-003", 103, 201);
            Assert.Equal(1, plan.ConflictCount);
            Assert.Equal(5, plan.WillChangeCount);
            Assert.Empty(plan.SkippedNumbers);
            Assert.True(plan.CanApply);
        }

        [Fact]
        public void OccupiedOnAnotherLevel()
        {
            var scenario = new Scenario { Scope = new long[] { 1, 2 } }
                .Add(Empty(L1, 1, 2))
                .Add(El(7, LA, "Д-1-001"));

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (1, "Д-1-002"), (2, "Д-1-003"));
            AssertSkipped(plan, ("Д-1-001", 7));
            Assert.Equal(1, plan.ConflictCount);
        }

        [Fact]
        public void EmptyValuesAreFree_SmallestIdIsReported()
        {
            var scenario = new Scenario { Scope = new long[] { 1, 2 } }
                .Add(Empty(L1, 1, 2))
                .Add(El(7, L2), El(8, L2, "  "), El(30, L2, "Д-1-002"), El(20, L2, "Д-1-002"));

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (1, "Д-1-001"), (2, "Д-1-003"));
            AssertSkipped(plan, ("Д-1-002", 20));
        }

        [Fact]
        public void Rooms_ConflictsWithinPhase()
        {
            var scenario = new Scenario
            {
                Category = Rooms,
                Template = "{Уровень}{N:00}",
                Scope = new long[] { 1, 2 }
            }.Add(El(1, L1, phase: "P1"), El(2, L1, phase: "P1"), El(7, L1, "101", phase: "P2"),
                El(8, L1, "102", phase: "P1"));

            NumberingPlan byPhase = scenario.Compute();

            AssertValues(byPhase, (1, "101"), (2, "103"));
            AssertSkipped(byPhase, ("102", 8));
            Assert.Equal(1, byPhase.ConflictCount);

            scenario.Category = new CategoryInfo("Rooms", "Помещения", "{Уровень}{N:00}", 3000,
                ConflictScope.Document, true);
            NumberingPlan byDocument = scenario.Compute();

            AssertValues(byDocument, (1, "103"), (2, "104"));
            AssertSkipped(byDocument, ("101", 7), ("102", 8));
        }

        [Fact]
        public void Example2_Doors1To5()
        {
            NumberingPlan plan = Example2(5).Compute();

            AssertValues(plan, (1, "Д-1-001"), (2, "Д-1-002"), (4, "Д-1-003"), (5, "Д-1-004"), (3, ""));
            AssertStatus(plan, RowStatus.Fixed, 3);
            Assert.Empty(plan.SkippedNumbers);
            Assert.Equal(0, plan.ConflictCount);
        }

        [Fact]
        public void Example2_NumberOfFixedElementIsSkipped()
        {
            NumberingPlan plan = Example2(10).Compute();

            AssertValues(plan, (1, "Д-1-001"), (2, "Д-1-002"), (4, "Д-1-003"), (5, "Д-1-004"), (6, "Д-1-005"),
                (7, "Д-1-006"), (8, "Д-1-008"), (9, "Д-1-009"), (10, "Д-1-010"));
            AssertSkipped(plan, ("Д-1-007", 3));
            Assert.Equal(1, plan.ConflictCount);
            Assert.DoesNotContain(plan.Rows, row => row.NewValue == "Д-1-007");
            Assert.Equal("Зафиксирован: занят Петров",
                TextCatalog.RowStatusText(Row(plan, 3).Status, Row(plan, 3).FixReason, Row(plan, 3).OwnerName));
        }

        [Fact]
        public void SwappingNumbers_IsNotAConflict()
        {
            var scenario = new Scenario().Add(El(1, L1, "Д-1-002"), El(2, L1, "Д-1-001"));

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (1, "Д-1-001"), (2, "Д-1-002"));
            AssertStatus(plan, RowStatus.WillChange, 1, 2);
            Assert.Empty(plan.SkippedNumbers);
            Assert.Equal(0, plan.ConflictCount);
            Assert.Empty(plan.BlockingErrors);
        }

        [Fact]
        public void DuplicateOfFixedElement_BlocksWithAllowDuplicates()
        {
            Scenario scenario = Example2(10);
            scenario.Strategy = ConflictStrategy.AllowDuplicates;

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (8, "Д-1-007"), (9, "Д-1-008"), (10, "Д-1-009"));
            AssertStatus(plan, RowStatus.WillChange, 8);
            BlockingError error = Assert.Single(plan.BlockingErrors);
            Assert.Null(error.TemplateIssue);
            AssertDuplicate(error.DuplicateInScope, "Д-1-007", 3, 8);
            Assert.Equal(new[] { "Этаж 1" }, error.LevelNames);
            Assert.Equal(0, plan.ConflictCount);
            Assert.False(plan.CanApply);
        }

        [Theory]
        [InlineData(ConflictStrategy.SkipOccupied)]
        [InlineData(ConflictStrategy.AllowDuplicates)]
        public void Example4_SameLevelCodes_Block(ConflictStrategy strategy)
        {
            var floor = new LevelData("E", "01 этаж", 0, null);
            var mezzanine = new LevelData("A", "01 антресоль", 2000, null);
            var scenario = new Scenario
            {
                Levels = new[] { floor, mezzanine },
                Rule = new LevelCodeRule(LevelCodeSource.LevelName, "", LevelCodePart.WordN, 1),
                Strategy = strategy
            }.Add(Empty(floor, 1, 2)).Add(Empty(mezzanine, 3, 4));

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (1, "Д-01-001"), (2, "Д-01-002"), (3, "Д-01-001"), (4, "Д-01-002"));
            Assert.Equal(2, plan.BlockingErrors.Count);
            AssertDuplicate(plan.BlockingErrors[0].DuplicateInScope, "Д-01-001", 1, 3);
            AssertDuplicate(plan.BlockingErrors[1].DuplicateInScope, "Д-01-002", 2, 4);
            Assert.All(plan.BlockingErrors,
                error => Assert.Equal(new[] { "01 этаж", "01 антресоль" }, error.LevelNames));
            BlockingError first = plan.BlockingErrors[0];
            Assert.Equal("Повтор номера Д-01-001 внутри охвата; уровни: 01 этаж, 01 антресоль",
                TextCatalog.Format(TextCatalog.ErrDupInScope, first.DuplicateInScope.Value,
                    string.Join(", ", first.LevelNames)));
            Assert.False(plan.CanApply);
        }

        [Fact]
        public void TemplateWithoutLevel_OneLevel_NoDuplicate()
        {
            var scenario = new Scenario { Template = "Д-{N:000}", Scope = new long[] { 1, 2 } }
                .Add(Empty(L1, 1, 2))
                .Add(El(3, L2, "Д-001"));

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (1, "Д-002"), (2, "Д-003"));
            AssertSkipped(plan, ("Д-001", 3));
            Assert.Empty(plan.BlockingErrors);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Example3_Append(bool onlyNewDoorInScope)
        {
            Scenario scenario = Example3();
            scenario.Append = true;
            if (onlyNewDoorInScope)
            {
                scenario.Scope = new long[] { 11 };
            }

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (11, "Д-1-011"));
            AssertStatus(plan, RowStatus.WillChange, 11);
            Assert.Equal(1, plan.WillChangeCount);
            Assert.Empty(plan.SkippedNumbers);
            if (!onlyNewDoorInScope)
            {
                foreach (long id in new long[] { 1, 2, 3, 4, 6, 7, 8, 9, 10 })
                {
                    Assert.Equal(RowStatus.Unchanged, Row(plan, id).Status);
                    Assert.Equal(string.Empty, Row(plan, id).NewValue);
                }
            }
        }

        [Fact]
        public void Example3_WithoutAppend_FillsTheGap()
        {
            Scenario scenario = Example3();
            scenario.Scope = new long[] { 11 };

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (11, "Д-1-005"));
            AssertSkipped(plan, ("Д-1-001", 1), ("Д-1-002", 2), ("Д-1-003", 3), ("Д-1-004", 4));
            Assert.Equal(4, plan.ConflictCount);
        }

        [Fact]
        public void Append_ValuesThatDoNotMatchTheTemplate()
        {
            var scenario = new Scenario { Append = true }
                .Add(El(1, L1, "Д-1-01A"), El(2, L1, "X-5"), El(3, L1, "Д-2-020"))
                .Add(Empty(L1, 4, 5));

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (4, "Д-1-001"), (5, "Д-1-002"), (1, ""), (2, ""), (3, ""));
            AssertStatus(plan, RowStatus.Unchanged, 1, 2, 3);
        }

        [Fact]
        public void Append_AndFixedElement()
        {
            var scenario = new Scenario { Append = true }
                .Add(El(1, L1, readOnly: true), El(2, L1, "Д-1-004"), El(3, L1));

            NumberingPlan plan = scenario.Compute();

            AssertStatus(plan, RowStatus.Fixed, 1);
            Assert.Equal(FixReason.ReadOnlyParameter, Row(plan, 1).FixReason);
            AssertStatus(plan, RowStatus.Unchanged, 2);
            AssertValues(plan, (2, ""), (3, "Д-1-005"));
            Assert.Equal(1, plan.WillChangeCount);
        }

        [Fact]
        public void SecondRun_ChangesNothing()
        {
            var scenario = new Scenario().Add(El(1, L1, "Д-1-001"), El(2, L1, "Д-1-002"), El(3, L1, "Д-1-003"));

            NumberingPlan plan = scenario.Compute();

            AssertStatus(plan, RowStatus.Unchanged, 1, 2, 3);
            AssertValues(plan, (1, "Д-1-001"), (2, "Д-1-002"), (3, "Д-1-003"));
            Assert.Equal(0, plan.WillChangeCount);
            Assert.Equal(0, plan.ConflictCount);
            Assert.False(plan.CanApply);
            Assert.Empty(plan.BlockingErrors);
        }

        [Fact]
        public void WhitespaceAndCaseInCurrentValue()
        {
            var scenario = new Scenario().Add(El(1, L1, "Д-1-001 "), El(2, L1, "д-1-002"));

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (1, "Д-1-001"), (2, "Д-1-002"));
            AssertStatus(plan, RowStatus.Unchanged, 1);
            AssertStatus(plan, RowStatus.WillChange, 2);
            Assert.Equal(1, plan.WillChangeCount);
        }

        [Fact]
        public void ElementWithoutLevel_IsFixedWhenTemplateUsesLevel()
        {
            var scenario = new Scenario().Add(Empty(L1, 1, 2)).Add(El(3, null));

            NumberingPlan plan = scenario.Compute();

            PlannedRow row = Row(plan, 3);
            Assert.Equal(RowStatus.Fixed, row.Status);
            Assert.Equal(FixReason.NoLevel, row.FixReason);
            Assert.Equal("Зафиксирован: нет уровня", TextCatalog.RowStatusText(row.Status, row.FixReason, null));
            AssertValues(plan, (1, "Д-1-001"), (2, "Д-1-002"));
            Assert.Empty(plan.BlockingErrors);
        }

        [Fact]
        public void NoLevelPlaceholder_TwoLevels()
        {
            var scenario = new Scenario { Template = "Д-{N:000}" }.Add(El(1, L1), El(2, L2));

            NumberingPlan plan = scenario.Compute();

            BlockingError error = Assert.Single(plan.BlockingErrors);
            Assert.Equal(TemplateIssueCode.MissingLevelForManyLevels, error.TemplateIssue.Code);
            Assert.Equal(new[] { "Этаж 1", "Этаж 2" }, error.TemplateIssue.Arguments);
            Assert.Null(error.DuplicateInScope);
            Assert.Empty(error.LevelNames);
            AssertStatus(plan, RowStatus.Unchanged, 1, 2);
            AssertValues(plan, (1, ""), (2, ""));
            Assert.False(plan.CanApply);
        }

        [Fact]
        public void NoLevelGroup_IsASeparateLevel()
        {
            var scenario = new Scenario { Template = "Д-{N:000}" }.Add(El(1, L1), El(2, null));

            NumberingPlan plan = scenario.Compute();

            BlockingError error = Assert.Single(plan.BlockingErrors);
            Assert.Equal(TemplateIssueCode.MissingLevelForManyLevels, error.TemplateIssue.Code);
            Assert.Equal(new[] { "Этаж 1", "Без уровня" }, error.TemplateIssue.Arguments);
            AssertValues(plan, (1, ""), (2, ""));
        }

        [Fact]
        public void OnlyNoLevelGroup()
        {
            var scenario = new Scenario { Template = "Д-{N:000}", Scope = new long[] { 1, 2 } }
                .Add(Empty(null, 1, 2))
                .Add(El(3, L1, "Д-001"));

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (1, "Д-002"), (2, "Д-003"));
            AssertSkipped(plan, ("Д-001", 3));
            Assert.Empty(plan.BlockingErrors);
        }

        [Fact]
        public void LevelOfScopeHasNoCodePart()
        {
            var roof = new LevelData("K", "Кровля", 9000, null);
            var scenario = new Scenario
            {
                Levels = new[] { L1, roof, L2 },
                Rule = new LevelCodeRule(LevelCodeSource.LevelName, "", LevelCodePart.WordN, 2),
                Scope = new long[] { 1, 2 }
            }.Add(El(1, L1), El(2, roof), El(3, L2));

            NumberingPlan blocked = scenario.Compute();

            BlockingError error = Assert.Single(blocked.BlockingErrors);
            Assert.Equal(TemplateIssueCode.LevelCodePartMissing, error.TemplateIssue.Code);
            Assert.Equal(new[] { "Кровля" }, error.TemplateIssue.Arguments);
            Assert.Equal("нет части: Кровля",
                TextCatalog.TemplateIssueText(error.TemplateIssue.Code, error.TemplateIssue.Arguments));
            AssertValues(blocked, (1, ""), (2, ""));

            scenario.Scope = new long[] { 1, 3 };
            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (1, "Д-1-001"), (3, "Д-2-001"));
            Assert.Empty(plan.BlockingErrors);
        }

        [Fact]
        public void TemplateError()
        {
            var scenario = new Scenario { Template = "Д-{Уровнь}-{N}" }.Add(El(1, L1), El(2, null));

            NumberingPlan plan = scenario.Compute();

            Assert.Contains(plan.BlockingErrors,
                error => error.TemplateIssue != null
                    && error.TemplateIssue.Code == TemplateIssueCode.UnknownPlaceholder);
            AssertStatus(plan, RowStatus.Unchanged, 1, 2);
            AssertValues(plan, (1, ""), (2, ""));
            Assert.Equal(FixReason.None, Row(plan, 2).FixReason);
            Assert.Empty(plan.SkippedNumbers);
            Assert.False(plan.CanApply);
        }

        [Fact]
        public void CounterWidthWarning()
        {
            var scenario = new Scenario { Template = "Д-{Уровень}-{N:00}" }.Add(Empty(L1, Range(1, 150)));

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (1, "Д-1-01"), (99, "Д-1-99"), (100, "Д-1-100"), (150, "Д-1-150"));
            TemplateIssue warning = Assert.Single(plan.Warnings);
            Assert.Equal(TemplateIssueCode.WidthTooSmall, warning.Code);
            Assert.Equal(new[] { "2", "150" }, warning.Arguments);
            Assert.True(plan.CanApply);

            scenario.Template = "Д-{Уровень}-{N}";
            Assert.Empty(scenario.Compute().Warnings);
        }

        [Fact]
        public void ParticipantWithoutPoint_GoesLast()
        {
            var scenario = new Scenario().Add(El(1, L1, noPoint: true)).Add(Empty(L1, 2, 3));

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (2, "Д-1-001"), (3, "Д-1-002"), (1, "Д-1-003"));
            Assert.Equal(new long[] { 2, 3 }, Assert.Single(scenario.Traversal.Calls));
        }

        [Fact]
        public void ForeignIdsInScope_AndEmptyScope()
        {
            var scenario = new Scenario { Scope = new long[] { 1, 999 } }.Add(Empty(L1, 1, 2));

            NumberingPlan plan = scenario.Compute();

            AssertValues(plan, (1, "Д-1-001"));
            AssertStatus(plan, RowStatus.OutOfScope, 2);
            Assert.Equal(2, plan.Rows.Count);

            scenario.Scope = new long[0];
            NumberingPlan empty = scenario.Compute();

            AssertStatus(empty, RowStatus.OutOfScope, 1, 2);
            Assert.Equal(0, empty.WillChangeCount);
            Assert.Empty(empty.BlockingErrors);
            Assert.False(empty.CanApply);
        }

        [Fact]
        public void OwnedElementOutOfScope_IsNotFixed()
        {
            var scenario = new Scenario { Scope = new long[] { 1 } }
                .Add(El(1, L1), El(2, L1, "Д-1-001", owner: "Петров"));

            NumberingPlan plan = scenario.Compute();

            AssertStatus(plan, RowStatus.OutOfScope, 2);
            Assert.Equal(FixReason.None, Row(plan, 2).FixReason);
            AssertValues(plan, (1, "Д-1-002"));
            AssertSkipped(plan, ("Д-1-001", 2));
        }

        [Fact]
        public void LevelKeyNotInLevels()
        {
            var unknown = new LevelData("L9", "Этаж 9", 27000, null);
            Scenario Data()
            {
                return new Scenario().Add(Empty(L1, 1, 2)).Add(El(3, unknown), El(4, null));
            }

            NumberingPlan a = Data().Compute();
            AssertStatus(a, RowStatus.Fixed, 3, 4);
            Assert.Equal(FixReason.NoLevel, Row(a, 3).FixReason);
            Assert.Equal(FixReason.NoLevel, Row(a, 4).FixReason);
            AssertValues(a, (1, "Д-1-001"), (2, "Д-1-002"));
            Assert.Empty(a.BlockingErrors);

            Scenario b = Data();
            b.Template = "Д-{N:000}";
            b.Scope = new long[] { 3, 4 };
            NumberingPlan planB = b.Compute();
            AssertValues(planB, (3, "Д-001"), (4, "Д-002"));
            Assert.Empty(planB.BlockingErrors);

            Scenario c = Data();
            c.Template = "Д-{N:000}";
            BlockingError error = Assert.Single(c.Compute().BlockingErrors);
            Assert.Equal(TemplateIssueCode.MissingLevelForManyLevels, error.TemplateIssue.Code);
            Assert.Equal(new[] { "Этаж 1", "Без уровня" }, error.TemplateIssue.Arguments);
        }

        [Fact]
        public void Compute_NullArguments_Throw()
        {
            var scenario = new Scenario().Add(Empty(L1, 1));
            NumberingInput input = scenario.Input();
            var registry = new TraversalRegistry(new ITraversalStrategy[] { new GridTraversal() });
            var order = new TraversalOrder("grid", null);

            Assert.Throws<ArgumentNullException>(() => NumberingEngine.Compute(null, registry));
            Assert.Throws<ArgumentNullException>(() => NumberingEngine.Compute(input, null));
            Assert.Throws<ArgumentNullException>(() => NumberingEngine.Compute(
                new NumberingInput(null, null, "{N}", LastWord, order, 500, ConflictStrategy.SkipOccupied, false),
                registry));
            Assert.Throws<ArgumentNullException>(() => NumberingEngine.Compute(
                new NumberingInput(input.Snapshot, null, "{N}", null, order, 500, ConflictStrategy.SkipOccupied,
                    false), registry));
            Assert.Throws<ArgumentNullException>(() => NumberingEngine.Compute(
                new NumberingInput(input.Snapshot, null, "{N}", LastWord, null, 500, ConflictStrategy.SkipOccupied,
                    false), registry));
        }
    }
}
