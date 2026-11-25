using RevitNumbering.Core.Common;
using RevitNumbering.Core.Domain.Elements;
using RevitNumbering.Core.Domain.Templates;

namespace RevitNumbering.Core.Domain.Numbering
{
    public enum RowStatus
    {
        WillChange,
        Unchanged,
        Fixed,
        Conflict,
        OutOfScope
    }

    public sealed class PlannedRow
    {
        public PlannedRow(long id, string newValue, RowStatus status, FixReason fixReason, string ownerName)
        {
            Id = id;
            NewValue = newValue ?? string.Empty;
            Status = status;
            FixReason = fixReason;
            OwnerName = ownerName;
        }

        public long Id { get; }

        /// <summary>Empty: no new value.</summary>
        public string NewValue { get; }

        public RowStatus Status { get; }

        /// <summary>null: not fixed.</summary>
        public FixReason FixReason { get; }

        public string OwnerName { get; }
    }

    /// <summary>A number skipped because it is occupied outside the scope.</summary>
    public sealed class SkippedNumber
    {
        public SkippedNumber(string value, long occupiedById)
        {
            Value = value;
            OccupiedById = occupiedById;
        }

        public string Value { get; }

        public long OccupiedById { get; }
    }

    /// <summary>A duplicate or a duplicate inside the scope.</summary>
    public sealed class DuplicateValue
    {
        public DuplicateValue(string value, IReadOnlyList<long> elementIds)
        {
            Value = value;
            ElementIds = DtoCollections.CopyList(elementIds);
        }

        public string Value { get; }

        public IReadOnlyList<long> ElementIds { get; }
    }

    /// <summary>An error that blocks Apply.</summary>
    public sealed class BlockingError
    {
        public BlockingError(TemplateIssue templateIssue, DuplicateValue duplicateInScope,
            IReadOnlyList<string> levelNames)
        {
            TemplateIssue = templateIssue;
            DuplicateInScope = duplicateInScope;
            LevelNames = DtoCollections.CopyList(levelNames);
        }

        /// <summary>Template or level code error; otherwise null.</summary>
        public TemplateIssue TemplateIssue { get; }

        /// <summary>Otherwise null.</summary>
        public DuplicateValue DuplicateInScope { get; }

        /// <summary>Levels that produced the duplicate.</summary>
        public IReadOnlyList<string> LevelNames { get; }
    }

    public sealed class NumberingPlan
    {
        public NumberingPlan(IReadOnlyList<PlannedRow> rows,
            IReadOnlyList<SkippedNumber> skippedNumbers, IReadOnlyList<DuplicateValue> duplicates,
            IReadOnlyList<TemplateIssue> warnings, IReadOnlyList<BlockingError> blockingErrors)
        {
            Rows = DtoCollections.CopyList(rows);
            SkippedNumbers = DtoCollections.CopyList(skippedNumbers);
            Duplicates = DtoCollections.CopyList(duplicates);
            Warnings = DtoCollections.CopyList(warnings);
            BlockingErrors = DtoCollections.CopyList(blockingErrors);

            int conflictRows = Rows.Count(row => row.Status == RowStatus.Conflict);
            WillChangeCount = Rows.Count(row => row.Status == RowStatus.WillChange) + conflictRows;
            ConflictCount = SkippedNumbers.Count + conflictRows;
            CanApply = BlockingErrors.Count == 0 && WillChangeCount > 0;
        }

        /// <summary>One row per input element, in input order.</summary>
        public IReadOnlyList<PlannedRow> Rows { get; }

        public int WillChangeCount { get; }

        public int ConflictCount { get; }

        public IReadOnlyList<SkippedNumber> SkippedNumbers { get; }

        public IReadOnlyList<DuplicateValue> Duplicates { get; }

        public IReadOnlyList<TemplateIssue> Warnings { get; }

        public IReadOnlyList<BlockingError> BlockingErrors { get; }

        /// <summary>No blocking errors and something to change.</summary>
        public bool CanApply { get; }
    }
}
