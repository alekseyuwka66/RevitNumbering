using RevitNumbering.Core.Common;
using RevitNumbering.Core.Domain.Elements;
using RevitNumbering.Core.Domain.Ordering;
using RevitNumbering.Core.Domain.Templates;

namespace RevitNumbering.Core.Domain.Numbering
{
    public enum ConflictStrategy
    {
        SkipOccupied,
        AllowDuplicates
    }

    /// <summary>
    /// Everything NumberingEngine.Compute needs: elements and levels of the whole document, the scope and the
    /// user's settings. No document, no generation, no category: the engine only computes.
    /// </summary>
    public sealed class NumberingInput
    {
        public NumberingInput(IReadOnlyList<NumberableElement> elements, IReadOnlyList<LevelData> levels,
            IReadOnlyCollection<long> scopeIds, string templateText, LevelCodeRule levelCode,
            ITraversalStrategy traversal, StrategyArguments traversalArguments,
            ConflictStrategy strategy, bool appendMode)
        {
            Elements = DtoCollections.CopyList(elements);
            Levels = DtoCollections.CopyList(levels);
            ScopeIds = DtoCollections.CopyCollection(scopeIds);
            TemplateText = templateText;
            LevelCode = levelCode;
            Traversal = traversal;
            TraversalArguments = traversalArguments ?? StrategyArguments.Empty;
            Strategy = strategy;
            AppendMode = appendMode;
        }

        /// <summary>All elements of the category in the document (conflicts are searched among them).</summary>
        public IReadOnlyList<NumberableElement> Elements { get; }

        public IReadOnlyList<LevelData> Levels { get; }

        public IReadOnlyCollection<long> ScopeIds { get; }

        public string TemplateText { get; }

        public LevelCodeRule LevelCode { get; }

        public ITraversalStrategy Traversal { get; }

        public StrategyArguments TraversalArguments { get; }

        public ConflictStrategy Strategy { get; }

        public bool AppendMode { get; }
    }
}
