namespace RevitNumbering.Core.Domain.Categories
{
    /// <summary>
    /// What the core and the window know about a category. Nothing about Revit and nothing about
    /// traversal strategies. The adapter implements it together with the reading of its elements;
    /// branching on Key is forbidden.
    /// </summary>
    public interface INumberingCategory
    {
        string Key { get; }

        string DisplayName { get; }

        /// <summary>Not parsed here: the core checks it like any entered text.</summary>
        string DefaultTemplate { get; }
    }
}
