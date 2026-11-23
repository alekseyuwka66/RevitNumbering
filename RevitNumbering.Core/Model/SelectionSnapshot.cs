namespace RevitNumbering.Core.Model
{
    public sealed class SelectionSnapshot
    {
        public SelectionSnapshot(DocumentKey document, IReadOnlyList<long> elementIds, bool hadGroupsOrAssemblies)
        {
            Document = document;
            ElementIds = DtoCollections.CopyList(elementIds);
            HadGroupsOrAssemblies = hadGroupsOrAssemblies;
        }

        public DocumentKey Document { get; }

        /// <summary>All selected ids; filtering by category is done in the core.</summary>
        public IReadOnlyList<long> ElementIds { get; }

        public bool HadGroupsOrAssemblies { get; }
    }
}
