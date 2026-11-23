using RevitNumbering.Core.Ordering;
using RevitNumbering.Core.Settings;

namespace RevitNumbering.Tests.Settings
{
    /// <summary>
    /// The settings module keeps its own copies of the default traversal constants;
    /// they must match the "grid" strategy.
    /// </summary>
    public class SettingsDefaultsConsistencyTests
    {
        [Fact]
        public void DefaultTraversal_MatchesGridStrategy()
        {
            var grid = new GridTraversal();

            Assert.Equal(SettingsDefaults.DefaultTraversalStrategyKey, grid.Key);

            StrategyParameter corner = grid.Parameters.Single(
                p => p.Key == SettingsDefaults.DefaultTraversalCornerKey);
            StrategyParameter direction = grid.Parameters.Single(
                p => p.Key == SettingsDefaults.DefaultTraversalDirectionKey);

            Assert.Equal(SettingsDefaults.DefaultTraversalCorner, corner.DefaultValue);
            Assert.Equal(SettingsDefaults.DefaultTraversalDirection, direction.DefaultValue);
        }
    }
}
