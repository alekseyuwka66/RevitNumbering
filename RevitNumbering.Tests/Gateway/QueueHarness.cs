using System.Diagnostics;
using RevitNumbering.Core.Gateway;

namespace RevitNumbering.Tests.Gateway
{
    /// <summary>
    /// A RequestQueue with a scripted raise: counts calls, records state events and pumps
    /// ExecutePending the way ExternalEvent would (one call per accepted raise).
    /// </summary>
    internal sealed class QueueHarness
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        private readonly object _sync = new object();
        private readonly Queue<bool> _raiseResults = new Queue<bool>();
        private readonly SemaphoreSlim _raised = new SemaphoreSlim(0);
        private readonly List<RequestStateEventArgs> _events = new List<RequestStateEventArgs>();
        private int _raiseCount;

        public QueueHarness(params bool[] raiseResults)
        {
            foreach (bool result in raiseResults)
            {
                _raiseResults.Enqueue(result);
            }

            Queue = new RequestQueue(Raise);
            Queue.StateChanged += (sender, e) =>
            {
                lock (_sync)
                {
                    _events.Add(e);
                }
            };
        }

        public RequestQueue Queue { get; }

        /// <summary>When set, raise throws it instead of returning a value.</summary>
        public Exception RaiseException { get; set; }

        public int RaiseCount => Volatile.Read(ref _raiseCount);

        public IReadOnlyList<RequestStateEventArgs> Events
        {
            get
            {
                lock (_sync)
                {
                    return _events.ToArray();
                }
            }
        }

        public IReadOnlyList<RequestState> States(string requestName)
        {
            return Events.Where(e => e.RequestName == requestName).Select(e => e.State).ToArray();
        }

        public int QueuedCount(string requestName)
        {
            return States(requestName).Count(state => state == RequestState.Queued);
        }

        /// <summary>Calls ExecutePending after every accepted raise until the task ends (5 s at most).</summary>
        public void PumpUntilCompleted(Task task)
        {
            var watch = Stopwatch.StartNew();
            while (!task.IsCompleted)
            {
                Assert.True(watch.Elapsed < Timeout, "The task did not complete in 5 s.");
                if (_raised.Wait(TimeSpan.FromMilliseconds(10)))
                {
                    Queue.ExecutePending();
                }
            }
        }

        /// <summary>Waits until a task ends without pumping the queue.</summary>
        public static void WaitCompleted(Task task)
        {
            try
            {
                Assert.True(task.Wait(Timeout), "The task did not complete in 5 s.");
            }
            catch (AggregateException)
            {
                // Cancelled or faulted: the test checks the status itself.
            }
        }

        private bool Raise()
        {
            Interlocked.Increment(ref _raiseCount);
            if (RaiseException != null)
            {
                throw RaiseException;
            }

            bool accepted;
            lock (_sync)
            {
                accepted = _raiseResults.Count == 0 || _raiseResults.Dequeue();
            }

            if (accepted)
            {
                _raised.Release();
            }

            return accepted;
        }
    }
}
