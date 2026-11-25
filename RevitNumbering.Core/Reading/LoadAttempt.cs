using RevitNumbering.Core.Documents;
using RevitNumbering.Core.Domain.Elements;

namespace RevitNumbering.Core.Reading
{
    /// <summary>
    /// State of one load attempt; changed only by the slice reader in the API context.
    /// </summary>
    public sealed class LoadAttempt
    {
        public LoadAttempt(DocumentKey document, string categoryKey, long generation,
            IReadOnlyList<long> ids, IReadOnlyList<LevelData> levels)
        {
            Document = document;
            CategoryKey = categoryKey;
            Generation = generation;
            Ids = ids == null ? Array.Empty<long>() : ids.ToArray();
            Levels = levels == null ? Array.Empty<LevelData>() : levels.ToArray();
            Elements = new List<NumberableElement>();
        }

        public DocumentKey Document { get; }

        public string CategoryKey { get; }

        /// <summary>Document generation in the first slice of the attempt.</summary>
        public long Generation { get; }

        /// <summary>Ids fixed in the first slice, ascending.</summary>
        public IReadOnlyList<long> Ids { get; }

        public IReadOnlyList<LevelData> Levels { get; }

        /// <summary>Processed ids: read + skipped.</summary>
        public int NextIndex { get; set; }

        /// <summary>Read elements in Ids order.</summary>
        public List<NumberableElement> Elements { get; }

        public bool GenerationChanged { get; set; }

        public bool IsComplete => NextIndex >= Ids.Count;
    }

    /// <summary>
    /// Reads a category of the whole document in slices. Implemented by the adapter (ModelReader):
    /// it finds the category by key in its registry and calls the category's own reading; API context only.
    /// </summary>
    public interface ICategorySliceReader
    {
        /// <summary>First slice of an attempt: fixes ids, levels and the generation.</summary>
        LoadAttempt Begin(DocumentKey document, string categoryKey);

        /// <summary>
        /// false: the generation has changed and nothing was read (only when ignoreGenerationChange is false).
        /// </summary>
        bool ReadNext(LoadAttempt attempt, bool ignoreGenerationChange);
    }
}
