using RevitNumbering.Core.Ordering;

namespace RevitNumbering.Tests.Ordering
{
    /// <summary>
    /// Strategy registry.
    /// </summary>
    public class TraversalRegistryTests
    {
        [Fact]
        public void Get_UnknownKeyGivesGrid()
        {
            var grid = new GridTraversal();
            var registry = new TraversalRegistry(new[] { grid });

            Assert.Same(grid, registry.Get("grid"));
            Assert.Same(grid, registry.Get("snake"));
            Assert.Same(grid, registry.Get("Grid"));
            Assert.Same(grid, registry.Get(""));
            Assert.Same(grid, registry.Get(null));
            Assert.Equal(1, registry.All.Count);
        }

        [Fact]
        public void NewStrategy_IsRegisteredWithoutCodeChanges()
        {
            var fake = new FakeTraversal();
            var registry = new TraversalRegistry(new ITraversalStrategy[] { new GridTraversal(), fake });

            Assert.Equal(new[] { "grid", "test" }, registry.All.Select(strategy => strategy.Key));
            Assert.Same(fake, registry.Get("test"));
        }

        [Fact]
        public void RegistryErrors()
        {
            Assert.Throws<ArgumentNullException>(() => new TraversalRegistry(null));
            Assert.Throws<ArgumentException>(
                () => new TraversalRegistry(new ITraversalStrategy[] { new GridTraversal(), new GridTraversal() }));
            Assert.Throws<ArgumentException>(
                () => new TraversalRegistry(new ITraversalStrategy[] { new FakeTraversal() }));
            Assert.Throws<ArgumentException>(
                () => new TraversalRegistry(new ITraversalStrategy[] { new GridTraversal(), null }));
        }
    }
}
