using RevitNumbering.Core.Common;
using RevitNumbering.Core.Documents;
using RevitNumbering.Core.Domain.Numbering;

namespace RevitNumbering.Core.Reading
{
    /// <summary>
    /// Ids of one scope ("весь документ", "на активном виде", "выделение") taken at a moment.
    /// A scope only limits what is numbered: elements are always loaded from the whole document.
    /// </summary>
    public sealed class ScopeSnapshot
    {
        public ScopeSnapshot(DocumentKey document, string scopeKey, IReadOnlyCollection<long> elementIds,
            bool hadGroupsOrAssemblies)
        {
            Document = document;
            ScopeKey = scopeKey;
            ElementIds = elementIds == null ? null : DtoCollections.CopyCollection(elementIds);
            HadGroupsOrAssemblies = hadGroupsOrAssemblies;
        }

        public DocumentKey Document { get; }

        public string ScopeKey { get; }

        /// <summary>null: no limit (the whole document). Ids of any category; filtering is done in the core.</summary>
        public IReadOnlyCollection<long> ElementIds { get; }

        public bool IsUnlimited => ElementIds == null;

        public bool HadGroupsOrAssemblies { get; }

        /// <summary>Scope ids among the loaded elements; a scope of another document gives an empty scope.</summary>
        public IReadOnlyList<long> IntersectWith(CategoryElements loaded)
        {
            if (loaded == null)
            {
                throw new ArgumentNullException(nameof(loaded));
            }

            if (!Equals(Document, loaded.Document))
            {
                return Array.Empty<long>();
            }

            return ScopeSelector.Intersect(ElementIds, loaded.Elements);
        }
    }
}
