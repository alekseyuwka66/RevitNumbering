namespace RevitNumbering.Core.Model
{
    public sealed class CategorySnapshot
    {
        public CategorySnapshot(DocumentKey document, long generation, CategoryInfo category,
            IReadOnlyList<ElementData> elements, IReadOnlyList<LevelData> levels, bool isStale)
        {
            Document = document;
            Generation = generation;
            Category = category;
            Elements = DtoCollections.CopyList(elements);
            Levels = DtoCollections.CopyList(levels);
            IsStale = isStale;
        }

        public DocumentKey Document { get; }

        /// <summary>Document generation at the moment of reading.</summary>
        public long Generation { get; }

        public CategoryInfo Category { get; }

        public IReadOnlyList<ElementData> Elements { get; }

        public IReadOnlyList<LevelData> Levels { get; }

        /// <summary>Data shown after the restart limit is reached.</summary>
        public bool IsStale { get; }
    }
}
