using System.Collections.Concurrent;
using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Texts;

namespace RevitNumbering.Tests.Logging
{
    public class SafeRunnerTests
    {
        private const string ExpectedText =
            @"Непредвиденная ошибка. Подробности записаны в журнал: C:\Logs\numbering-R2024-2026-11-23.log";

        [Fact]
        public void Run_Exception_IsLoggedAndShown()
        {
            var log = new FakeLog();
            var shown = new List<string>();
            var thrown = new InvalidOperationException("boom");

            SafeRunner.Run(log, "Apply", () => throw thrown, shown.Add);

            Assert.Single(log.Errors);
            Assert.Same(thrown, log.Errors[0].Value);
            Assert.Contains("Apply", log.Errors[0].Key, StringComparison.Ordinal);
            Assert.Empty(log.Infos);
            Assert.Equal(new[] { ExpectedText }, shown);
        }

        [Fact]
        public void Run_Success_NoLogNoMessage()
        {
            var log = new FakeLog();
            var shown = new List<string>();
            bool executed = false;

            SafeRunner.Run(log, "Apply", () => executed = true, shown.Add);

            Assert.True(executed);
            Assert.Empty(log.Errors);
            Assert.Empty(shown);
        }

        [Fact]
        public void Run_TextHasNoExceptionDetails()
        {
            var log = new FakeLog();
            var shown = new List<string>();

            SafeRunner.Run(log, "Apply", TestThrower.ThrowerForTest, shown.Add);

            string text = Assert.Single(shown);
            Assert.DoesNotContain("boom", text, StringComparison.Ordinal);
            Assert.DoesNotContain("inner-secret", text, StringComparison.Ordinal);
            Assert.DoesNotContain("InvalidOperationException", text, StringComparison.Ordinal);
            Assert.DoesNotContain("ThrowerForTest", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\n", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\r", text, StringComparison.Ordinal);
        }

        [Fact]
        public void Run_NullShowError_OnlyLog()
        {
            var log = new FakeLog();

            SafeRunner.Run(log, "Apply", () => throw new InvalidOperationException("boom"), null);

            Assert.Single(log.Errors);
        }

        [Fact]
        public void Run_ShowErrorFails_SecondErrorLogged()
        {
            var log = new FakeLog();
            var operationError = new InvalidOperationException();
            var uiError = new Exception("ui");

            SafeRunner.Run(log, "Apply", () => throw operationError, text => throw uiError);

            Assert.Equal(2, log.Errors.Count);
            Assert.Same(operationError, log.Errors[0].Value);
            Assert.Same(uiError, log.Errors[1].Value);
        }

        [Fact]
        public void Run_LogFails_MessageStillShown()
        {
            var shown = new List<string>();

            SafeRunner.Run(new BrokenLog(), "Apply", () => throw new InvalidOperationException(), shown.Add);

            Assert.Single(shown);
        }

        [Fact]
        public void Run_Cancellation_IsInfoWithoutMessage()
        {
            var log = new FakeLog();
            var shown = new List<string>();

            SafeRunner.Run(log, "Apply", () => throw new OperationCanceledException(), shown.Add);

            Assert.Single(log.Infos);
            Assert.Empty(log.Errors);
            Assert.Empty(shown);
        }

        [Fact]
        public void Run_NullLog_TextHasDefaultDirectory()
        {
            var shown = new List<string>();

            SafeRunner.Run(null, "Apply", () => throw new InvalidOperationException(), shown.Add);

            Assert.Equal(new[] { TextCatalog.Format(TextCatalog.ErrUnexpected, FileLog.DefaultDirectory) }, shown);
        }

        [Fact]
        public void Run_NullActionAndNullOperation_DoNotThrow()
        {
            var log = new FakeLog();

            SafeRunner.Run(log, "Apply", null, null);
            SafeRunner.Run(log, null, () => throw new InvalidOperationException(), null);

            KeyValuePair<string, Exception> error = Assert.Single(log.Errors);
            Assert.StartsWith("unknown", error.Key, StringComparison.Ordinal);
        }

        [Fact]
        public async Task RunAsync_ExceptionAfterAwait_IsLoggedAndShown()
        {
            var log = new FakeLog();
            var shown = new List<string>();

            Task task = SafeRunner.RunAsync(log, "Apply", async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("boom");
            }, shown.Add);
            await task;

            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            Assert.Single(log.Errors);
            Assert.Equal(new[] { ExpectedText }, shown);
        }

        [Fact]
        public async Task RunAsync_SynchronousThrow_IsLoggedAndShown()
        {
            var log = new FakeLog();
            var shown = new List<string>();

            Task task = SafeRunner.RunAsync(log, "Apply", () => throw new InvalidOperationException(), shown.Add);
            await task;

            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            Assert.Single(log.Errors);
            Assert.Single(shown);
        }

        [Fact]
        public async Task RunAsync_CancelledTask_IsInfoWithoutMessage()
        {
            var log = new FakeLog();
            var shown = new List<string>();

            Task task = SafeRunner.RunAsync(log, "Apply",
                () => Task.FromCanceled(new CancellationToken(true)), shown.Add);
            await task;

            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            Assert.Single(log.Infos);
            Assert.Empty(log.Errors);
            Assert.Empty(shown);
        }

        [Fact]
        public async Task RunAsync_NullTaskAndNullAction_Complete()
        {
            var log = new FakeLog();

            Task fromNullTask = SafeRunner.RunAsync(log, "Apply", () => null, null);
            Task fromNullAction = SafeRunner.RunAsync(log, "Apply", null, null);
            await fromNullTask;
            await fromNullAction;

            Assert.Equal(TaskStatus.RanToCompletion, fromNullTask.Status);
            Assert.Equal(TaskStatus.RanToCompletion, fromNullAction.Status);
            Assert.Empty(log.Infos);
            Assert.Empty(log.Errors);
        }

        [Fact]
        public void RunAsync_ShowErrorRunsOnCallerContextThread()
        {
            int pumpThreadId = 0;
            int showThreadId = -1;
            TaskStatus status = TaskStatus.Created;

            var thread = new Thread(() =>
            {
                using (var context = new SingleThreadContext())
                {
                    SynchronizationContext.SetSynchronizationContext(context);
                    pumpThreadId = Environment.CurrentManagedThreadId;

                    Task task = SafeRunner.RunAsync(new FakeLog(), "Apply", async () =>
                    {
                        await Task.Delay(1);
                        throw new InvalidOperationException("boom");
                    }, text => showThreadId = Environment.CurrentManagedThreadId);
                    task.ContinueWith(t => context.Complete(), TaskScheduler.Default);

                    context.Pump();
                    status = task.Status;
                }
            });
            thread.Start();

            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "the pump thread did not finish");
            Assert.Equal(TaskStatus.RanToCompletion, status);
            Assert.Equal(pumpThreadId, showThreadId);
        }

        /// <summary>Single-threaded context: posted callbacks run on the thread that calls Pump.</summary>
        private sealed class SingleThreadContext : SynchronizationContext, IDisposable
        {
            private readonly BlockingCollection<KeyValuePair<SendOrPostCallback, object>> _queue =
                new BlockingCollection<KeyValuePair<SendOrPostCallback, object>>();

            public override void Post(SendOrPostCallback d, object state)
            {
                _queue.Add(new KeyValuePair<SendOrPostCallback, object>(d, state));
            }

            public void Complete()
            {
                _queue.CompleteAdding();
            }

            public void Pump()
            {
                foreach (KeyValuePair<SendOrPostCallback, object> item in _queue.GetConsumingEnumerable())
                {
                    item.Key(item.Value);
                }
            }

            public void Dispose()
            {
                _queue.Dispose();
            }
        }
    }
}
