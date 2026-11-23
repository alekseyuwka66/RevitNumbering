using System.Diagnostics;
using System.Globalization;
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Ordering;
using RevitNumbering.Core.Templates;
using Xunit.Abstractions;

namespace RevitNumbering.Tests.Numbering
{
    /// <summary>
    /// Recalculation time budget on 10 000 elements.
    /// Run separately in Release: dotnet test -c Release --filter "Category=Performance".
    /// </summary>
    [Trait("Category", "Performance")]
    public class NumberingPerformanceTests
    {
        /// <summary>The first size.</summary>
        private const int ElementCount = 10000;

        private const int LevelCount = 10;
        private const int PerLevel = ElementCount / LevelCount;
        private const int Measurements = 5;

        private readonly ITestOutputHelper _output;

        public NumberingPerformanceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Performance_WholeScope()
        {
            NumberingInput input = Input(null, false, ConflictStrategy.SkipOccupied);

            NumberingPlan plan = Measure("WholeScope", input);

            Assert.Equal(ElementCount, plan.WillChangeCount);
        }

        [Fact]
        public void Performance_ManySkips()
        {
            NumberingInput input = Input(HalfScope(), true, ConflictStrategy.SkipOccupied);

            NumberingPlan plan = Measure("ManySkips", input);

            Assert.Equal(4995, plan.ConflictCount);
            AssertCounters(plan, n => 2 * n - 1);
        }

        [Fact]
        public void Performance_Duplicates()
        {
            NumberingInput input = Input(HalfScope(), true, ConflictStrategy.AllowDuplicates);

            NumberingPlan plan = Measure("Duplicates", input);

            Assert.Equal(2500, plan.ConflictCount);
            Assert.Equal(2500, plan.Duplicates.Count);
            Assert.Equal(5000, plan.WillChangeCount);
            AssertCounters(plan, n => n);
        }

        /// <summary>One warm-up run, then the median of five Stopwatch measurements around Compute.</summary>
        private NumberingPlan Measure(string name, NumberingInput input)
        {
            var registry = new TraversalRegistry(new ITraversalStrategy[] { new GridTraversal() });
            NumberingPlan plan = NumberingEngine.Compute(input, registry);

            var times = new double[Measurements];
            for (int i = 0; i < Measurements; i++)
            {
                Stopwatch watch = Stopwatch.StartNew();
                plan = NumberingEngine.Compute(input, registry);
                watch.Stop();
                times[i] = watch.Elapsed.TotalMilliseconds;
            }

            Array.Sort(times);
            double median = times[Measurements / 2];
            _output.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0}: median {1:F1} ms, min {2:F1} ms, max {3:F1} ms, budget {4} ms", name, median, times[0],
                times[Measurements - 1], NumberingParameters.PreviewMs));

            Assert.True(median <= NumberingParameters.PreviewMs, string.Format(CultureInfo.InvariantCulture,
                "{0}: median {1:F1} ms exceeds the budget of {2} ms", name, median,
                NumberingParameters.PreviewMs));
            return plan;
        }

        /// <summary>On every scope level the k-th element in traversal order has the expected counter.</summary>
        private static void AssertCounters(NumberingPlan plan, Func<int, int> expectedCounter)
        {
            for (int level = 1; level <= LevelCount / 2; level++)
            {
                string prefix = "Д-" + level.ToString(CultureInfo.InvariantCulture) + "-";
                var actual = new HashSet<string>(StringComparer.Ordinal);
                for (int i = (level - 1) * PerLevel; i < level * PerLevel; i++)
                {
                    actual.Add(plan.Rows[i].NewValue);
                }

                var expected = new HashSet<string>(StringComparer.Ordinal);
                for (int n = 1; n <= PerLevel; n++)
                {
                    expected.Add(prefix + expectedCounter(n).ToString("0000", CultureInfo.InvariantCulture));
                }

                Assert.True(expected.SetEquals(actual), "Unexpected numbers on level " + level);
            }
        }

        private static long[] HalfScope()
        {
            return NumberingTestData.Range(1, ElementCount / 2);
        }

        /// <summary>
        /// 10 000 doors on 10 levels of 1000; points on a 50 × 20 grid with a 900 × 1500 mm step and
        /// a ±200 mm shift (Random). With occupied == true the element t = 0…4999 of levels 6–10
        /// holds "Д-k-mmmm", k = t / 1000 + 1, m = 2·(t mod 1000 + 1).
        /// </summary>
        private static NumberingInput Input(long[] scope, bool occupied, ConflictStrategy strategy)
        {
            var levels = new LevelData[LevelCount];
            for (int i = 0; i < LevelCount; i++)
            {
                levels[i] = new LevelData("L" + (i + 1), "Этаж " + (i + 1), i * 3000, null);
            }

            var random = new Random(42);
            var elements = new ElementData[ElementCount];
            for (int i = 0; i < ElementCount; i++)
            {
                int level = i / PerLevel;
                int cell = i % PerLevel;
                double x = cell % 50 * 900 + random.Next(-200, 201);
                double y = cell / 50 * 1500 + random.Next(-200, 201);
                string value = string.Empty;
                if (occupied && i >= ElementCount / 2)
                {
                    int t = i - ElementCount / 2;
                    value = "Д-" + (t / PerLevel + 1).ToString(CultureInfo.InvariantCulture) + "-"
                        + (2 * (t % PerLevel + 1)).ToString("0000", CultureInfo.InvariantCulture);
                }

                elements[i] = new ElementData(i + 1, "Door", levels[level].Key, levels[level].Name, value, x, y,
                    null, false, false, null, true, 0, false);
            }

            var category = new CategoryInfo("Doors", "Двери", "Д-{Уровень}-{N:000}", 500, ConflictScope.Document,
                false);
            var snapshot = new CategorySnapshot(NumberingTestData.Document, 1, category, elements, levels, false);
            var order = new TraversalOrder(GridTraversal.StrategyKey, new Dictionary<string, string>
            {
                { GridTraversal.CornerParameter, "TopLeft" },
                { GridTraversal.DirectionParameter, "Rows" }
            });

            return new NumberingInput(snapshot, scope ?? NumberingTestData.Range(1, ElementCount),
                "Д-{Уровень}-{N:0000}", NumberingTestData.LastWord, order, 500, strategy, false);
        }
    }
}
