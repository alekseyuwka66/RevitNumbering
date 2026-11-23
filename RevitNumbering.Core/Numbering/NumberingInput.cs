using RevitNumbering.Core.Model;
using RevitNumbering.Core.Ordering;
using RevitNumbering.Core.Templates;

namespace RevitNumbering.Core.Numbering
{
    public enum ConflictStrategy
    {
        SkipOccupied,
        AllowDuplicates
    }

    /// <summary>
    /// Everything NumberingEngine.Compute needs: snapshot, scope and the user's settings.
    /// </summary>
    public sealed class NumberingInput
    {
        public NumberingInput(CategorySnapshot snapshot, IReadOnlyCollection<long> scopeIds, string templateText,
            LevelCodeRule levelCode, TraversalOrder order, double rowToleranceMm,
            ConflictStrategy strategy, bool appendMode)
        {
            Snapshot = snapshot;
            ScopeIds = DtoCollections.CopyCollection(scopeIds);
            TemplateText = templateText;
            LevelCode = levelCode;
            Order = order;
            RowToleranceMm = rowToleranceMm;
            Strategy = strategy;
            AppendMode = appendMode;
        }

        public CategorySnapshot Snapshot { get; }

        public IReadOnlyCollection<long> ScopeIds { get; }

        public string TemplateText { get; }

        public LevelCodeRule LevelCode { get; }

        public TraversalOrder Order { get; }

        public double RowToleranceMm { get; }

        public ConflictStrategy Strategy { get; }

        public bool AppendMode { get; }
    }
}
