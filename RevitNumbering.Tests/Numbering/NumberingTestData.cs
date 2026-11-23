using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Ordering;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Tests.Numbering
{
    /// <summary>Test strategy: ids in ascending order; remembers what it was given.</summary>
    internal sealed class ByIdTraversal : ITraversalStrategy
    {
        public const string StrategyKey = "test-by-id";

        public string Key => StrategyKey;

        public string DisplayName => "By id";

        public IReadOnlyList<StrategyParameter> Parameters { get; } = new StrategyParameter[0];

        public List<long[]> Calls { get; } = new List<long[]>();

        public IReadOnlyList<long> Order(IReadOnlyList<OrderItem> items,
            IReadOnlyDictionary<string, string> parameters, double rowToleranceMm)
        {
            long[] ids = items.Select(item => item.Id).OrderBy(id => id).ToArray();
            Calls.Add(ids);
            return ids;
        }
    }

    /// <summary>One Compute call with the common defaults; every plan is checked by Invariants.</summary>
    internal sealed class Scenario
    {
        public CategoryInfo Category = NumberingTestData.Doors;

        public IReadOnlyList<LevelData> Levels = new[] { NumberingTestData.L1, NumberingTestData.L2, NumberingTestData.LA };

        public List<ElementData> Elements = new List<ElementData>();

        /// <summary>null: the whole snapshot.</summary>
        public IReadOnlyCollection<long> Scope;

        public string Template = "Д-{Уровень}-{N:000}";

        public LevelCodeRule Rule = NumberingTestData.LastWord;

        public ConflictStrategy Strategy = ConflictStrategy.SkipOccupied;

        public bool Append;

        public ByIdTraversal Traversal = new ByIdTraversal();

        public Scenario Add(params ElementData[] elements)
        {
            Elements.AddRange(elements);
            return this;
        }

        public CategorySnapshot Snapshot()
        {
            return new CategorySnapshot(NumberingTestData.Document, 5, Category, Elements, Levels, false);
        }

        public NumberingInput Input()
        {
            CategorySnapshot snapshot = Snapshot();
            return new NumberingInput(snapshot, Scope ?? Elements.Select(e => e.Id).ToArray(), Template, Rule,
                new TraversalOrder(ByIdTraversal.StrategyKey, new Dictionary<string, string>()), 500, Strategy,
                Append);
        }

        public NumberingPlan Compute()
        {
            NumberingInput input = Input();
            NumberingPlan plan = NumberingEngine.Compute(input,
                new TraversalRegistry(new ITraversalStrategy[] { new GridTraversal(), Traversal }));
            NumberingTestData.AssertInvariants(input, plan);
            return plan;
        }
    }

    /// <summary>Common test data.</summary>
    internal static class NumberingTestData
    {
        public static readonly DocumentKey Document = new DocumentKey("doc");

        public static readonly CategoryInfo Doors =
            new CategoryInfo("Doors", "Двери", "Д-{Уровень}-{N:000}", 500, ConflictScope.Document, false);

        public static readonly CategoryInfo Rooms =
            new CategoryInfo("Rooms", "Помещения", "{Уровень}{N:00}", 3000, ConflictScope.Phase, true);

        public static readonly LevelData L1 = new LevelData("L1", "Этаж 1", 0, null);

        public static readonly LevelData L2 = new LevelData("L2", "Этаж 2", 3000, null);

        public static readonly LevelData LA = new LevelData("LA", "Антресоль 1", 1500, null);

        public static readonly LevelCodeRule LastWord =
            new LevelCodeRule(LevelCodeSource.LevelName, "", LevelCodePart.LastWord, 1);

        /// <summary>Element "id/level/value" with flags; the point is (id·1000, 0).</summary>
        public static ElementData El(long id, LevelData level, string value = "", string owner = null,
            bool readOnly = false, bool noPoint = false, string phase = null, bool hasLocation = true,
            double area = 20, bool hasBoundaries = true)
        {
            return new ElementData(id, "Element " + id, level?.Key, level?.Name, value,
                noPoint ? (double?)null : id * 1000, noPoint ? (double?)null : 0, phase, readOnly,
                owner != null, owner, hasLocation, area, hasBoundaries);
        }

        /// <summary>Elements without a value on one level.</summary>
        public static ElementData[] Empty(LevelData level, params long[] ids)
        {
            return ids.Select(id => El(id, level)).ToArray();
        }

        public static long[] Range(long first, long last)
        {
            var ids = new long[last - first + 1];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = first + i;
            }

            return ids;
        }

        public static PlannedRow Row(NumberingPlan plan, long id)
        {
            return plan.Rows.Single(row => row.Id == id);
        }

        public static void AssertValues(NumberingPlan plan, params (long Id, string Value)[] expected)
        {
            foreach ((long id, string value) in expected)
            {
                Assert.Equal(value, Row(plan, id).NewValue);
            }
        }

        public static void AssertStatus(NumberingPlan plan, RowStatus status, params long[] ids)
        {
            foreach (long id in ids)
            {
                Assert.Equal(status, Row(plan, id).Status);
            }
        }

        public static void AssertSkipped(NumberingPlan plan, params (string Value, long Id)[] expected)
        {
            Assert.Equal(expected, plan.SkippedNumbers.Select(s => (s.Value, s.OccupiedById)).ToArray());
        }

        public static void AssertDuplicate(DuplicateValue duplicate, string value, params long[] ids)
        {
            Assert.Equal(value, duplicate.Value);
            Assert.Equal(ids, duplicate.ElementIds);
        }

        /// <summary>The rules that hold for every plan.</summary>
        public static void AssertInvariants(NumberingInput input, NumberingPlan plan)
        {
            IReadOnlyList<ElementData> elements = input.Snapshot.Elements;
            Assert.Equal(input.Snapshot.Generation, plan.SnapshotGeneration);
            Assert.Equal(elements.Count, plan.Rows.Count);
            for (int i = 0; i < elements.Count; i++)
            {
                Assert.Equal(elements[i].Id, plan.Rows[i].Id);
            }

            int willChange = plan.Rows.Count(r => r.Status == RowStatus.WillChange);
            int conflict = plan.Rows.Count(r => r.Status == RowStatus.Conflict);
            Assert.Equal(willChange + conflict, plan.WillChangeCount);
            Assert.Equal(plan.BlockingErrors.Count == 0 && plan.WillChangeCount > 0, plan.CanApply);

            if (input.Strategy == ConflictStrategy.SkipOccupied)
            {
                Assert.Equal(plan.SkippedNumbers.Count, plan.ConflictCount);
                Assert.Empty(plan.Duplicates);
                Assert.Equal(0, conflict);
            }
            else
            {
                Assert.Equal(conflict, plan.ConflictCount);
                Assert.Empty(plan.SkippedNumbers);
            }

            var scope = new HashSet<long>(input.ScopeIds);
            foreach (PlannedRow row in plan.Rows)
            {
                Assert.NotNull(row.NewValue);
                Assert.Equal(!scope.Contains(row.Id), row.Status == RowStatus.OutOfScope);
                Assert.Equal(row.Status == RowStatus.Fixed, row.FixReason != FixReason.None);
                if (row.Status == RowStatus.OutOfScope || row.Status == RowStatus.Fixed)
                {
                    Assert.Equal(string.Empty, row.NewValue);
                }

                if (row.Status == RowStatus.WillChange || row.Status == RowStatus.Conflict)
                {
                    Assert.NotEqual(string.Empty, row.NewValue);
                }

                if (row.FixReason != FixReason.OwnedByOtherUser)
                {
                    Assert.Null(row.OwnerName);
                }
            }

            bool templateError = plan.BlockingErrors.Any(e => e.TemplateIssue != null);
            if (templateError)
            {
                Assert.All(plan.Rows, row => Assert.Equal(string.Empty, row.NewValue));
                Assert.Empty(plan.SkippedNumbers);
                Assert.Empty(plan.Duplicates);
                Assert.Empty(plan.Warnings);
            }

            foreach (BlockingError error in plan.BlockingErrors)
            {
                Assert.True((error.TemplateIssue == null) != (error.DuplicateInScope == null));
                Assert.False(error.TemplateIssue != null && error.TemplateIssue.IsWarning);
            }

            Assert.All(plan.Warnings, warning => Assert.True(warning.IsWarning));
        }
    }
}
