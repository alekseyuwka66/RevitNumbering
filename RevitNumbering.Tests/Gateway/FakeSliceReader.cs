using RevitNumbering.Core.Gateway;
using RevitNumbering.Core.Model;

namespace RevitNumbering.Tests.Gateway
{
    /// <summary>
    /// Fake reader by the ICategorySliceReader contract: ids 1…N, a fixed number of ids per slice,
    /// the generation is driven by the test scenario.
    /// </summary>
    internal sealed class FakeSliceReader : ICategorySliceReader
    {
        private readonly int _idCount;
        private readonly int _sliceSize;
        private int _readNextInAttempt;

        public FakeSliceReader(int idCount, int sliceSize, long generation)
        {
            _idCount = idCount;
            _sliceSize = sliceSize;
            Generation = generation;
        }

        public long Generation { get; set; }

        public int BeginCalls { get; private set; }

        public int ReadNextCalls { get; private set; }

        /// <summary>ignoreGenerationChange of every ReadNext call, in call order.</summary>
        public List<bool> IgnoreFlags { get; } = new List<bool>();

        /// <summary>Thrown by Begin when set.</summary>
        public Exception BeginException { get; set; }

        /// <summary>Called before the first ReadNext of every attempt; argument is the attempt number from 1.</summary>
        public Action<int> BeforeFirstReadNext { get; set; }

        public LoadAttempt Begin(DocumentKey document, string categoryKey)
        {
            BeginCalls++;
            _readNextInAttempt = 0;
            if (BeginException != null)
            {
                throw BeginException;
            }

            var ids = Enumerable.Range(1, _idCount).Select(i => (long)i).ToArray();
            var levels = new[] { new LevelData("L1", "1", 0, null) };
            var attempt = new LoadAttempt(document, categoryKey, Generation, ids, levels);
            ReadSlice(attempt);
            return attempt;
        }

        public bool ReadNext(LoadAttempt attempt, bool ignoreGenerationChange)
        {
            ReadNextCalls++;
            IgnoreFlags.Add(ignoreGenerationChange);
            if (_readNextInAttempt++ == 0)
            {
                BeforeFirstReadNext?.Invoke(BeginCalls);
            }

            if (Generation != attempt.Generation)
            {
                if (!ignoreGenerationChange)
                {
                    return false;
                }

                attempt.GenerationChanged = true;
            }

            ReadSlice(attempt);
            return true;
        }

        private void ReadSlice(LoadAttempt attempt)
        {
            for (int i = 0; i < _sliceSize && !attempt.IsComplete; i++)
            {
                long id = attempt.Ids[attempt.NextIndex];
                attempt.Elements.Add(new ElementData(id, "E" + id, "L1", "1", string.Empty, 0, 0, null,
                    false, false, null, true, 0, false));
                attempt.NextIndex++;
            }
        }
    }

    /// <summary>Synchronous IProgress: records reports on the calling thread.</summary>
    internal sealed class RecordingProgress : IProgress<LoadProgress>
    {
        private readonly object _sync = new object();
        private readonly List<(int Loaded, int Total)> _reports = new List<(int, int)>();

        public Action<LoadProgress> OnReport { get; set; }

        public IReadOnlyList<(int Loaded, int Total)> Reports
        {
            get
            {
                lock (_sync)
                {
                    return _reports.ToArray();
                }
            }
        }

        public void Report(LoadProgress value)
        {
            lock (_sync)
            {
                _reports.Add((value.Loaded, value.Total));
            }

            OnReport?.Invoke(value);
        }
    }
}
