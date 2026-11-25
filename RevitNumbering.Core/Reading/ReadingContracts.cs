using RevitNumbering.Core.Documents;
using RevitNumbering.Core.Domain.Categories;
using RevitNumbering.Core.Gateway;

namespace RevitNumbering.Core.Reading
{
    /// <summary>
    /// Loads a category of the whole document in slices. Does not know which categories exist:
    /// reading of a concrete category is the category's own method in the adapter.
    /// </summary>
    public interface IElementReader
    {
        Task<CategoryElements> LoadAsync(DocumentKey document, INumberingCategory category,
            IProgress<LoadProgress> progress, CancellationToken cancellationToken);
    }

    /// <summary>A scope as the window shows it.</summary>
    public interface IScopeInfo
    {
        string Key { get; }

        string DisplayName { get; }
    }

    /// <summary>
    /// Scopes of numbering. Independent of categories: a scope returns ids, the core intersects them
    /// with the loaded category (ScopeSelector). A new scope = one class in the adapter + one registration line.
    /// </summary>
    public interface IScopeService
    {
        IReadOnlyList<IScopeInfo> Scopes { get; }

        Task<ScopeSnapshot> TakeAsync(DocumentKey document, string scopeKey, CancellationToken cancellationToken);
    }
}
