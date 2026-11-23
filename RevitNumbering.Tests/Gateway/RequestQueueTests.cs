// The tests pump the queue themselves and read results of already completed tasks.
#pragma warning disable xUnit1031
using System.Diagnostics;
using RevitNumbering.Core.Gateway;
using RevitNumbering.Core.Model;

namespace RevitNumbering.Tests.Gateway
{
    /// <summary>
    /// Request queue without Revit.
    /// </summary>
    public class RequestQueueTests
    {
        private static readonly DocumentKey Doc1 = new DocumentKey("doc-1");
        private static readonly DocumentKey Doc2 = new DocumentKey("doc-2");

        [Fact]
        public void Fifo()
        {
            var harness = new QueueHarness();
            var executed = new List<string>();

            Task<int> a = harness.Queue.Enqueue("A", Doc1, () => { executed.Add("A"); return 1; }, CancellationToken.None);
            Task<int> b = harness.Queue.Enqueue("B", Doc1, () => { executed.Add("B"); return 2; }, CancellationToken.None);
            Task<int> c = harness.Queue.Enqueue("C", Doc1, () => { executed.Add("C"); return 3; }, CancellationToken.None);
            harness.Queue.ExecutePending();
            harness.Queue.ExecutePending();
            harness.Queue.ExecutePending();

            Assert.Equal(new[] { "A", "B", "C" }, executed);
            Assert.Equal(TaskStatus.RanToCompletion, a.Status);
            Assert.Equal(TaskStatus.RanToCompletion, b.Status);
            Assert.Equal(TaskStatus.RanToCompletion, c.Status);
            Assert.Equal(new[] { 1, 2, 3 }, new[] { a.Result, b.Result, c.Result });
        }

        [Fact]
        public void OneRequestPerCall()
        {
            var harness = new QueueHarness();

            Task<int> a = harness.Queue.Enqueue("A", Doc1, () => 1, CancellationToken.None);
            Task<int> b = harness.Queue.Enqueue("B", Doc1, () => 2, CancellationToken.None);
            Assert.Equal(1, harness.RaiseCount);

            harness.Queue.ExecutePending();
            Assert.Equal(TaskStatus.RanToCompletion, a.Status);
            Assert.False(b.IsCompleted);
            Assert.Equal(new[] { RequestState.Queued }, harness.States("B"));
            Assert.Equal(2, harness.RaiseCount);

            harness.Queue.ExecutePending();
            Assert.Equal(TaskStatus.RanToCompletion, b.Status);
            Assert.Equal(2, harness.RaiseCount);
        }

        [Fact]
        public void RaiseRejectedOnEnqueue()
        {
            var harness = new QueueHarness(false);
            bool called = false;

            Task<int> a = harness.Queue.Enqueue("A", Doc1, () => { called = true; return 1; }, CancellationToken.None);
            harness.Queue.ExecutePending();

            Assert.Equal(TaskStatus.Faulted, a.Status);
            var rejected = Assert.IsType<RequestRejectedException>(a.Exception.InnerException);
            Assert.Equal("A", rejected.RequestName);
            Assert.Null(rejected.InnerException);
            Assert.False(called);
            Assert.Equal(new[] { RequestState.Queued, RequestState.Faulted }, harness.States("A"));
        }

        [Fact]
        public void RaiseRejectedAfterExecution()
        {
            var harness = new QueueHarness(true, false);
            var executed = new List<string>();

            Task<int> a = harness.Queue.Enqueue("A", Doc1, () => { executed.Add("A"); return 1; }, CancellationToken.None);
            Task<int> b = harness.Queue.Enqueue("B", Doc1, () => { executed.Add("B"); return 2; }, CancellationToken.None);
            Task<int> c = harness.Queue.Enqueue("C", Doc1, () => { executed.Add("C"); return 3; }, CancellationToken.None);
            harness.Queue.ExecutePending();
            harness.Queue.ExecutePending();

            Assert.Equal(TaskStatus.RanToCompletion, a.Status);
            Assert.Equal(TaskStatus.Faulted, b.Status);
            Assert.Equal(TaskStatus.Faulted, c.Status);
            Assert.IsType<RequestRejectedException>(b.Exception.InnerException);
            Assert.IsType<RequestRejectedException>(c.Exception.InnerException);
            Assert.Equal(new[] { "A" }, executed);
            Assert.Equal(new[] { RequestState.Queued, RequestState.Faulted }, harness.States("B"));
        }

        [Fact]
        public void RaiseThrows()
        {
            var harness = new QueueHarness { RaiseException = new InvalidOperationException("x") };

            Task<int> a = harness.Queue.Enqueue("A", Doc1, () => 1, CancellationToken.None);

            Assert.Equal(TaskStatus.Faulted, a.Status);
            var rejected = Assert.IsType<RequestRejectedException>(a.Exception.InnerException);
            Assert.Equal("x", rejected.InnerException.Message);
        }

        [Fact]
        public void ExceptionInWork()
        {
            var harness = new QueueHarness();
            var boom = new InvalidOperationException("boom");

            Task<int> a = harness.Queue.Enqueue<int>("A", Doc1, () => throw boom, CancellationToken.None);
            Task<int> b = harness.Queue.Enqueue("B", Doc1, () => 2, CancellationToken.None);
            harness.Queue.ExecutePending();
            harness.Queue.ExecutePending();

            Assert.Equal(TaskStatus.Faulted, a.Status);
            Assert.Same(boom, a.Exception.InnerException);
            Assert.Equal(new[] { RequestState.Queued, RequestState.Running, RequestState.Faulted }, harness.States("A"));
            Assert.Equal(TaskStatus.RanToCompletion, b.Status);
            Assert.Equal(2, b.Result);
        }

        [Fact]
        public void CancellationFromWork()
        {
            var harness = new QueueHarness();

            Task<int> a = harness.Queue.Enqueue<int>("A", Doc1, () => throw new OperationCanceledException(),
                CancellationToken.None);
            harness.Queue.ExecutePending();

            Assert.Equal(TaskStatus.Canceled, a.Status);
            Assert.Equal(new[] { RequestState.Queued, RequestState.Running, RequestState.Cancelled },
                harness.States("A"));
        }

        [Fact]
        public void CancelPending()
        {
            var harness = new QueueHarness();
            bool called = false;

            Task<int> a = harness.Queue.Enqueue("A", Doc1, () => { called = true; return 1; }, CancellationToken.None);
            bool first = harness.Queue.CancelPending();
            harness.Queue.ExecutePending();
            bool second = harness.Queue.CancelPending();

            Assert.True(first);
            Assert.Equal(TaskStatus.Canceled, a.Status);
            Assert.False(called);
            Assert.Equal(new[] { RequestState.Queued, RequestState.Cancelled }, harness.States("A"));
            Assert.False(second);
        }

        [Fact]
        public void RunningRequestIsNotCancelled()
        {
            var harness = new QueueHarness();

            Task<bool> a = harness.Queue.Enqueue("A", Doc1, () => harness.Queue.CancelPending(),
                CancellationToken.None);
            harness.Queue.ExecutePending();

            Assert.Equal(TaskStatus.RanToCompletion, a.Status);
            Assert.False(a.Result);
        }

        [Fact]
        public void DocumentClosing()
        {
            var harness = new QueueHarness();
            var executed = new List<string>();

            Task<int> a = harness.Queue.Enqueue("A", Doc1, () => { executed.Add("A"); return 1; }, CancellationToken.None);
            Task<int> b = harness.Queue.Enqueue("B", Doc2, () => { executed.Add("B"); return 2; }, CancellationToken.None);
            Task<int> c = harness.Queue.Enqueue("C", Doc1, () => { executed.Add("C"); return 3; }, CancellationToken.None);
            harness.Queue.CancelForDocument(new DocumentKey("doc-1"));
            harness.Queue.ExecutePending();
            harness.Queue.ExecutePending();

            Assert.Equal(TaskStatus.Canceled, a.Status);
            Assert.Equal(TaskStatus.Canceled, c.Status);
            Assert.Equal(TaskStatus.RanToCompletion, b.Status);
            Assert.Equal(new[] { "B" }, executed);
        }

        [Fact]
        public void ApplicationClosing()
        {
            var harness = new QueueHarness();
            int calls = 0;

            Task<int> a = harness.Queue.Enqueue("A", Doc1, () => ++calls, CancellationToken.None);
            Task<int> b = harness.Queue.Enqueue("B", Doc2, () => ++calls, CancellationToken.None);
            harness.Queue.CancelAll();
            harness.Queue.ExecutePending();

            Assert.Equal(TaskStatus.Canceled, a.Status);
            Assert.Equal(TaskStatus.Canceled, b.Status);
            Assert.Equal(0, calls);
        }

        [Fact]
        public void TokenCancelledWhileQueued()
        {
            var harness = new QueueHarness();
            bool called = false;
            using (var cts = new CancellationTokenSource())
            {
                Task<int> a = harness.Queue.Enqueue("A", Doc1, () => { called = true; return 1; }, cts.Token);
                cts.Cancel();

                Assert.Equal(TaskStatus.Canceled, a.Status);
                harness.Queue.ExecutePending();
                Assert.False(called);
                Assert.Equal(new[] { RequestState.Queued, RequestState.Cancelled }, harness.States("A"));
            }
        }

        [Fact]
        public void TokenCancelledAfterStartChangesNothing()
        {
            var harness = new QueueHarness();
            using (var cts = new CancellationTokenSource())
            {
                Task<int> a = harness.Queue.Enqueue("A", Doc1, () => { cts.Cancel(); return 1; }, cts.Token);
                harness.Queue.ExecutePending();

                Assert.Equal(TaskStatus.RanToCompletion, a.Status);
                Assert.Equal(new[] { RequestState.Queued, RequestState.Running, RequestState.Completed },
                    harness.States("A"));
            }
        }

        [Fact]
        public void TokenCancelledBeforeEnqueue()
        {
            var harness = new QueueHarness();
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                Task<int> a = harness.Queue.Enqueue("A", Doc1, () => 1, cts.Token);

                Assert.Equal(TaskStatus.Canceled, a.Status);
                Assert.Equal(0, harness.RaiseCount);
                Assert.Empty(harness.Events);
            }
        }

        [Fact]
        public void States()
        {
            var harness = new QueueHarness();

            Task<int> load = harness.Queue.Enqueue(RequestNames.Load, Doc1, () => 1, CancellationToken.None);
            harness.Queue.ExecutePending();

            Assert.Equal(TaskStatus.RanToCompletion, load.Status);
            Assert.Equal(
                new[] { (RequestState.Queued, "Load"), (RequestState.Running, "Load"), (RequestState.Completed, "Load") },
                harness.Events.Select(e => (e.State, e.RequestName)).ToArray());
        }

        [Fact]
        public void ContinuationsRunAsynchronously()
        {
            var harness = new QueueHarness();
            using (var gate = new ManualResetEventSlim(false))
            {
                Task<int> a = harness.Queue.Enqueue("A", Doc1, () => 1, CancellationToken.None);
                Task continuation = a.ContinueWith(t => gate.Wait(TimeSpan.FromSeconds(5)),
                    CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

                var watch = Stopwatch.StartNew();
                harness.Queue.ExecutePending();
                watch.Stop();
                gate.Set();
                QueueHarness.WaitCompleted(continuation);

                Assert.True(a.CreationOptions.HasFlag(TaskCreationOptions.RunContinuationsAsynchronously));
                Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1),
                    "ExecutePending was blocked by a synchronous continuation.");
            }
        }

        [Fact]
        public void NullWorkIsRejected()
        {
            var harness = new QueueHarness();

            Exception error = Record.Exception(
                () => { harness.Queue.Enqueue<int>("A", Doc1, null, CancellationToken.None); });

            Assert.IsType<ArgumentNullException>(error);
            Assert.Equal(0, harness.RaiseCount);
        }

        [Theory]
        [InlineData("doc-1", "doc-1", true)]
        [InlineData("doc-1", "DOC-1", false)]
        public void DocumentKeys(string left, string right, bool equal)
        {
            var a = new DocumentKey(left);
            var b = new DocumentKey(right);

            Assert.Equal(equal, a.Equals(b));
            Assert.Equal(equal, a == b);
            Assert.Equal(!equal, a != b);
            if (equal)
            {
                Assert.Equal(a.GetHashCode(), b.GetHashCode());
            }

            Assert.Equal(left, a.ToString());
        }

        [Fact]
        public void NullKeyValueIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new DocumentKey(null));
        }
    }
}
