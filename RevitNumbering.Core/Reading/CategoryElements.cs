using RevitNumbering.Core.Common;
using RevitNumbering.Core.Documents;
using RevitNumbering.Core.Domain.Categories;
using RevitNumbering.Core.Domain.Elements;

namespace RevitNumbering.Core.Reading
{
    /// <summary>
    /// Loaded category: always the whole document (conflicts are searched outside the scope, F7-01).
    /// Document and generation belong to reading; the engine gets only Elements and Levels.
    /// </summary>
    public sealed class CategoryElements
    {
        public CategoryElements(DocumentKey document, long generation, INumberingCategory category,
            IReadOnlyList<NumberableElement> elements, IReadOnlyList<LevelData> levels, bool isStale)
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

        public INumberingCategory Category { get; }

        public IReadOnlyList<NumberableElement> Elements { get; }

        public IReadOnlyList<LevelData> Levels { get; }

        /// <summary>Data shown after the restart limit is reached.</summary>
        public bool IsStale { get; }
    }
}
