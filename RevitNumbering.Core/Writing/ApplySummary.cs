using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;

namespace RevitNumbering.Core.Writing
{
    /// <summary>An element fixed before the write.</summary>
    public sealed class FixedElement
    {
        public FixedElement(long id, FixReason reason, string ownerName)
        {
            Id = id;
            Reason = reason;
            OwnerName = ownerName;
        }

        public long Id { get; }

        public FixReason Reason { get; }

        public string OwnerName { get; }
    }

    /// <summary>
    /// The single final summary of Apply.
    /// </summary>
    public sealed class ApplySummary
    {
        public ApplySummary(int changedCount, IReadOnlyList<FixedElement> fixedBeforeWrite,
            IReadOnlyList<WriteFailure> failures, IReadOnlyList<DuplicateValue> remainingDuplicates)
        {
            ChangedCount = changedCount;
            FixedBeforeWrite = DtoCollections.CopyList(fixedBeforeWrite);
            Failures = DtoCollections.CopyList(failures);
            RemainingDuplicates = DtoCollections.CopyList(remainingDuplicates);
        }

        public int ChangedCount { get; }

        /// <summary>Shown in the summary.</summary>
        public IReadOnlyList<FixedElement> FixedBeforeWrite { get; }

        public IReadOnlyList<WriteFailure> Failures { get; }

        /// <summary>By re-read data.</summary>
        public IReadOnlyList<DuplicateValue> RemainingDuplicates { get; }

        /// <summary>
        /// Builds the summary for a Committed or Failed report. reread == null until the re-read
        /// is finished or when the document has changed: duplicates are then taken from the written values
        /// and the plan; otherwise from the re-read data.
        /// </summary>
        public static ApplySummary Build(NumberingPlan plan, WriteReport report, CategorySnapshot reread)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (report.Outcome != WriteOutcome.Committed && report.Outcome != WriteOutcome.Failed)
            {
                throw new ArgumentException("A summary exists only for a Committed or Failed write.",
                    nameof(report));
            }

            var fixedBeforeWrite = new List<FixedElement>();
            foreach (PlannedRow row in plan.Rows)
            {
                if (row.Status == RowStatus.Fixed)
                {
                    fixedBeforeWrite.Add(new FixedElement(row.Id, row.FixReason, row.OwnerName));
                }
            }

            var written = new Dictionary<long, string>();
            foreach (WrittenValue value in report.Written)
            {
                written[value.Id] = value.ActualValue;
            }

            List<DuplicateValue> duplicates = reread != null
                ? DuplicatesByReread(reread, written)
                : DuplicatesByPlan(plan, written);

            return new ApplySummary(report.Written.Count, fixedBeforeWrite, report.Failures, Sort(duplicates));
        }

        /// <summary>Groups of two or more elements with one value where at least one was written now.</summary>
        private static List<DuplicateValue> DuplicatesByReread(CategorySnapshot reread,
            Dictionary<long, string> written)
        {
            bool byPhase = reread.Category != null && reread.Category.ConflictScope == ConflictScope.Phase;
            var groups = new Dictionary<string, Dictionary<string, List<long>>>(StringComparer.Ordinal);
            foreach (ElementData element in reread.Elements)
            {
                string value = ValueComparer.Normalize(element.CurrentValue);
                if (value.Length == 0)
                {
                    continue;
                }

                string scope = byPhase ? element.PhaseKey ?? string.Empty : string.Empty;
                if (!groups.TryGetValue(scope, out Dictionary<string, List<long>> byValue))
                {
                    byValue = new Dictionary<string, List<long>>(StringComparer.Ordinal);
                    groups.Add(scope, byValue);
                }

                if (!byValue.TryGetValue(value, out List<long> ids))
                {
                    ids = new List<long>();
                    byValue.Add(value, ids);
                }

                ids.Add(element.Id);
            }

            var result = new List<DuplicateValue>();
            foreach (Dictionary<string, List<long>> byValue in groups.Values)
            {
                foreach (KeyValuePair<string, List<long>> group in byValue)
                {
                    AddIfDuplicate(result, group.Key, group.Value, written);
                }
            }

            return result;
        }

        /// <summary>Planned duplicates that really happened, judged by the written values.</summary>
        private static List<DuplicateValue> DuplicatesByPlan(NumberingPlan plan, Dictionary<long, string> written)
        {
            var result = new List<DuplicateValue>();
            foreach (DuplicateValue duplicate in plan.Duplicates)
            {
                var planned = new HashSet<long>();
                foreach (PlannedRow row in plan.Rows)
                {
                    if ((row.Status == RowStatus.WillChange || row.Status == RowStatus.Conflict)
                        && ValueComparer.AreEqual(row.NewValue, duplicate.Value))
                    {
                        planned.Add(row.Id);
                    }
                }

                var remaining = new List<long>();
                foreach (long id in duplicate.ElementIds)
                {
                    bool keepsValue = !planned.Contains(id);
                    bool gotValue = written.TryGetValue(id, out string actual)
                        && ValueComparer.AreEqual(actual, duplicate.Value);
                    if (keepsValue || gotValue)
                    {
                        remaining.Add(id);
                    }
                }

                AddIfDuplicate(result, ValueComparer.Normalize(duplicate.Value), remaining, written);
            }

            return result;
        }

        private static void AddIfDuplicate(List<DuplicateValue> result, string value, IEnumerable<long> ids,
            Dictionary<long, string> written)
        {
            long[] sorted = ids.Distinct().OrderBy(id => id).ToArray();
            if (sorted.Length >= 2 && sorted.Any(written.ContainsKey))
            {
                result.Add(new DuplicateValue(value, sorted));
            }
        }

        /// <summary>By value (ordinal), then by the smallest id.</summary>
        private static List<DuplicateValue> Sort(List<DuplicateValue> duplicates)
        {
            duplicates.Sort((a, b) =>
            {
                int byValue = string.CompareOrdinal(a.Value, b.Value);
                return byValue != 0 ? byValue : a.ElementIds[0].CompareTo(b.ElementIds[0]);
            });
            return duplicates;
        }
    }
}
