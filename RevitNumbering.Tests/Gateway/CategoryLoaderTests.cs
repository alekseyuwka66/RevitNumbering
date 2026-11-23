// The tests pump the queue themselves and read results of already completed tasks.
#pragma warning disable xUnit1031
using RevitNumbering.Core.Gateway;
using RevitNumbering.Core.Model;

namespace RevitNumbering.Tests.Gateway
{
    /// <summary>
    /// Loading in slices over the queue.
    /// Fake reader: ids 1…10, 4 ids per slice, maxRestarts = the default limit.
    /// </summary>
    public class CategoryLoaderTests
    {
        private static readonly DocumentKey Doc = new DocumentKey("doc-1");

        private static readonly CategoryInfo Doors =
            new CategoryInfo("Doors", "Двери", "Д-{Уровень}-{N:000}", 500, ConflictScope.Document, false);

        private static readonly long[] TenIds = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        private static Task<CategorySnapshot> Load(QueueHarness harness, FakeSliceReader reader,
            RecordingProgress progress, CancellationToken ct)
        {
            var loader = new CategoryLoader(harness.Queue, reader, GatewayParameters.LoadMaxRestarts);
            Task<CategorySnapshot> task = loader.LoadAsync(Doc, Doors, progress, ct);
            harness.PumpUntilCompleted(task);
            return task;
        }

        [Fact]
        public void ThreeSlices()
        {
            var harness = new QueueHarness();
            var reader = new FakeSliceReader(10, 4, 7);
            var progress = new RecordingProgress();

            Task<CategorySnapshot> task = Load(harness, reader, progress, CancellationToken.None);

            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            Assert.Equal(3, harness.QueuedCount(RequestNames.Load));
            Assert.Equal(3, harness.RaiseCount);
            Assert.Equal(new[] { (4, 10), (8, 10), (10, 10) }, progress.Reports);
            CategorySnapshot snapshot = task.Result;
            Assert.Equal(TenIds, snapshot.Elements.Select(e => e.Id));
            Assert.Equal(7, snapshot.Generation);
            Assert.False(snapshot.IsStale);
            Assert.Equal(Doc, snapshot.Document);
            Assert.Same(Doors, snapshot.Category);
            Assert.Single(snapshot.Levels);
        }

        [Fact]
        public void EverythingInFirstSlice()
        {
            var harness = new QueueHarness();
            var reader = new FakeSliceReader(3, 4, 7);
            var progress = new RecordingProgress();

            Task<CategorySnapshot> task = Load(harness, reader, progress, CancellationToken.None);

            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            Assert.Equal(1, harness.QueuedCount(RequestNames.Load));
            Assert.Equal(new[] { (3, 3) }, progress.Reports);
            Assert.Equal(3, task.Result.Elements.Count);
            Assert.Equal(0, reader.ReadNextCalls);
        }

        [Fact]
        public void OneRestart()
        {
            var harness = new QueueHarness();
            var reader = new FakeSliceReader(10, 4, 7);
            reader.BeforeFirstReadNext = attemptNumber =>
            {
                if (attemptNumber == 1)
                {
                    reader.Generation = 8;
                }
            };
            var progress = new RecordingProgress();

            Task<CategorySnapshot> task = Load(harness, reader, progress, CancellationToken.None);

            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            Assert.Equal(2, reader.BeginCalls);
            Assert.Equal(new[] { (4, 10), (4, 10), (8, 10), (10, 10) }, progress.Reports);
            Assert.Equal(8, task.Result.Generation);
            Assert.False(task.Result.IsStale);
            Assert.Equal(TenIds, task.Result.Elements.Select(e => e.Id));
        }

        [Fact]
        public void FourChangesGiveStaleSnapshot()
        {
            var harness = new QueueHarness();
            var reader = new FakeSliceReader(10, 4, 7);
            reader.BeforeFirstReadNext = attemptNumber => reader.Generation++;
            var progress = new RecordingProgress();

            Task<CategorySnapshot> task = Load(harness, reader, progress, CancellationToken.None);

            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            Assert.Equal(4, reader.BeginCalls);
            Assert.Equal(11, reader.Generation);
            Assert.Equal(new[] { false, false, false, true, true }, reader.IgnoreFlags);
            Assert.Equal(10, task.Result.Generation);
            Assert.True(task.Result.IsStale);
            Assert.Equal(10, task.Result.Elements.Count);
        }

        [Fact]
        public void ThreeChangesGiveFreshSnapshot()
        {
            var harness = new QueueHarness();
            var reader = new FakeSliceReader(10, 4, 7);
            reader.BeforeFirstReadNext = attemptNumber =>
            {
                if (attemptNumber <= 3)
                {
                    reader.Generation++;
                }
            };
            var progress = new RecordingProgress();

            Task<CategorySnapshot> task = Load(harness, reader, progress, CancellationToken.None);

            Assert.Equal(TaskStatus.RanToCompletion, task.Status);
            Assert.Equal(4, reader.BeginCalls);
            Assert.Equal(10, task.Result.Generation);
            Assert.False(task.Result.IsStale);
            Assert.Equal(10, task.Result.Elements.Count);
        }

        [Fact]
        public void CancelBetweenSlices()
        {
            var harness = new QueueHarness();
            var reader = new FakeSliceReader(10, 4, 7);
            using (var cts = new CancellationTokenSource())
            {
                var progress = new RecordingProgress { OnReport = report => cts.Cancel() };

                Task<CategorySnapshot> task = Load(harness, reader, progress, cts.Token);

                Assert.Equal(TaskStatus.Canceled, task.Status);
                Assert.Equal(0, reader.ReadNextCalls);
                Assert.Equal(1, harness.QueuedCount(RequestNames.Load));
                Assert.False(harness.Queue.CancelPending());
            }
        }

        [Fact]
        public void DocumentNotActive()
        {
            var harness = new QueueHarness();
            var reader = new FakeSliceReader(10, 4, 7) { BeginException = new OperationCanceledException() };

            Task<CategorySnapshot> task = Load(harness, reader, new RecordingProgress(), CancellationToken.None);

            Assert.Equal(TaskStatus.Canceled, task.Status);
            Assert.Equal(1, reader.BeginCalls);
        }

        [Fact]
        public void RaiseRejectedOnSecondSlice()
        {
            var harness = new QueueHarness(true, false);
            var reader = new FakeSliceReader(10, 4, 7);

            Task<CategorySnapshot> task = Load(harness, reader, new RecordingProgress(), CancellationToken.None);

            Assert.Equal(TaskStatus.Faulted, task.Status);
            Assert.IsType<RequestRejectedException>(task.Exception.InnerException);
            Assert.Equal(0, reader.ReadNextCalls);
        }

        [Fact]
        public void LoadAttemptTracksCompletion()
        {
            var attempt = new LoadAttempt(Doc, "Doors", 7, new long[] { 1, 2 }, null);

            Assert.False(attempt.IsComplete);
            Assert.Empty(attempt.Levels);
            Assert.Empty(attempt.Elements);
            attempt.NextIndex = 2;
            Assert.True(attempt.IsComplete);
            Assert.True(new LoadAttempt(Doc, "Doors", 7, null, null).IsComplete);
        }
    }
}
