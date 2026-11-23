using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Writing
{
    /// <summary>
    /// Bookkeeping of one write: admission, precheck, attempts and refusals. ModelWriter only
    /// performs API calls and reports facts here; every decision is made in this class.
    /// </summary>
    public sealed class WriteSession
    {
        private readonly WritePlan _plan;
        private readonly List<WriteFailure> _failures = new List<WriteFailure>();
        private IReadOnlyList<WriteItem> _currentAttempt;
        private int _rollbackCount;
        private bool _prechecked;
        private bool _finished;

        public WriteSession(WritePlan plan)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _currentAttempt = plan.Items;
            AttemptNumber = 1;
        }

        /// <summary>Items of the attempt to run now.</summary>
        public IReadOnlyList<WriteItem> CurrentAttempt => _currentAttempt;

        /// <summary>Number of the current attempt, from 1.</summary>
        public int AttemptNumber { get; private set; }

        /// <summary>
        /// Admission to writing; the first matching rule wins. null: write.
        /// </summary>
        public static WriteOutcome? Admit(WritePlan plan, DocumentKey activeDocument, long activeGeneration,
            bool cancelled)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            if (cancelled)
            {
                return WriteOutcome.Cancelled;
            }

            if (activeDocument == null || !plan.Document.Equals(activeDocument))
            {
                return WriteOutcome.DocumentMismatch;
            }

            if (plan.SnapshotGeneration != activeGeneration)
            {
                return WriteOutcome.Stale;
            }

            if (plan.Items.Count == 0)
            {
                return WriteOutcome.NothingToWrite;
            }

            return null;
        }

        /// <summary>Report of a write that was not admitted: the model is untouched.</summary>
        public static WriteReport Rejected(WriteOutcome outcome)
        {
            return new WriteReport(outcome, null, null, 0);
        }

        /// <summary>
        /// Checks every plan element by its facts (one per element, in plan.Items order). Refused elements
        /// go to the failures, the rest make the first attempt.
        /// </summary>
        public void Precheck(IReadOnlyList<ElementWriteFacts> facts)
        {
            if (facts == null)
            {
                throw new ArgumentNullException(nameof(facts));
            }

            EnsureNotFinished();
            if (_prechecked || _rollbackCount > 0)
            {
                throw new InvalidOperationException("Precheck is done once, before the first attempt.");
            }

            if (facts.Count != _plan.Items.Count)
            {
                throw new ArgumentException("One fact per plan element is expected.", nameof(facts));
            }

            var attempt = new List<WriteItem>(_plan.Items.Count);
            for (int i = 0; i < _plan.Items.Count; i++)
            {
                WriteItem item = _plan.Items[i];
                WriteFailure failure = WritePrecheck.Check(item, facts[i]);
                if (failure == null)
                {
                    attempt.Add(item);
                }
                else
                {
                    _failures.Add(failure);
                }
            }

            _currentAttempt = attempt.ToArray();
            _prechecked = true;
        }

        /// <summary>
        /// The current attempt was rolled back. true: run CurrentAttempt again (without the failed elements);
        /// false: nothing to retry, call Fail.
        /// </summary>
        public bool RolledBack(IReadOnlyList<WriteFailure> failures, IReadOnlyList<string> unboundTexts)
        {
            EnsureNotFinished();
            _rollbackCount++;

            // Only failures of the attempt count: in the attempt order, one per element.
            var byId = new Dictionary<long, WriteFailure>();
            if (failures != null)
            {
                foreach (WriteFailure failure in failures)
                {
                    if (failure != null && !byId.ContainsKey(failure.Id))
                    {
                        byId.Add(failure.Id, failure);
                    }
                }
            }

            var failedIds = new List<long>();
            foreach (WriteItem item in _currentAttempt)
            {
                if (byId.TryGetValue(item.Id, out WriteFailure failure))
                {
                    _failures.Add(failure);
                    failedIds.Add(item.Id);
                }
            }

            IReadOnlyList<WriteItem> next = WriteRetryPolicy.NextAttempt(_currentAttempt, failedIds, AttemptNumber);
            if (next.Count > 0)
            {
                _currentAttempt = next;
                AttemptNumber++;
                return true;
            }

            if (failedIds.Count == 0)
            {
                // A failure without elements of the attempt.
                _failures.Add(new WriteFailure(0, WriteFailReason.RolledBackByWarning,
                    WriteTexts.UnboundFailure(unboundTexts)));
            }
            else if (failedIds.Count < _currentAttempt.Count)
            {
                // Elements without their own reason remain: the attempt limit is reached.
                _failures.Add(new WriteFailure(0, WriteFailReason.RolledBackByWarning,
                    WriteTexts.AttemptsExhausted(WritingParameters.WriteMaxAttempts)));
            }

            return false;
        }

        /// <summary>The current attempt was committed; written holds the actual values.</summary>
        public WriteReport Commit(IReadOnlyList<WrittenValue> written)
        {
            EnsureNotFinished();
            _finished = true;
            return new WriteReport(WriteOutcome.Committed, written, _failures, AttemptNumber);
        }

        /// <summary>Nothing was written: no element left or the last attempt was rolled back.</summary>
        public WriteReport Fail()
        {
            EnsureNotFinished();
            _finished = true;
            return new WriteReport(WriteOutcome.Failed, null, _failures, _rollbackCount);
        }

        private void EnsureNotFinished()
        {
            if (_finished)
            {
                throw new InvalidOperationException("The write session is already finished.");
            }
        }
    }
}
