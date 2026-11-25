using RevitNumbering.Core.Documents;
using RevitNumbering.Core.Domain.Categories;
using RevitNumbering.Core.Gateway;

namespace RevitNumbering.Core.Reading
{
    /// <summary>
    /// Loading in slices on top of the queue: every slice is a separate request,
    /// restarts on generation change, IsStale after the last allowed restart, progress.
    /// </summary>
    public sealed class CategoryLoader : IElementReader
    {
        private readonly RequestQueue _queue;
        private readonly ICategorySliceReader _reader;
        private readonly int _maxRestarts;

        public CategoryLoader(RequestQueue queue, ICategorySliceReader reader, int maxRestarts)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            if (maxRestarts < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRestarts));
            }

            _maxRestarts = maxRestarts;
        }

        /// <summary>Called on the window thread; continuations stay on it (no ConfigureAwait(false)).</summary>
        public Task<CategoryElements> LoadAsync(DocumentKey document, INumberingCategory category,
            IProgress<LoadProgress> progress, CancellationToken cancellationToken)
        {
            if (category == null)
            {
                throw new ArgumentNullException(nameof(category));
            }

            return LoadCoreAsync(document, category, progress, cancellationToken);
        }

        private async Task<CategoryElements> LoadCoreAsync(DocumentKey document, INumberingCategory category,
            IProgress<LoadProgress> progress, CancellationToken cancellationToken)
        {
            int restarts = 0;
            while (true)
            {
                // The last allowed attempt reads to the end even if the model keeps changing.
                bool ignoreGenerationChange = restarts >= _maxRestarts;

                LoadAttempt attempt = await _queue.Enqueue(RequestNames.Load, document,
                    () => _reader.Begin(document, category.Key), cancellationToken);
                Report(progress, attempt);

                bool restart = false;
                while (!attempt.IsComplete)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    LoadAttempt current = attempt;
                    bool read = await _queue.Enqueue(RequestNames.Load, document,
                        () => _reader.ReadNext(current, ignoreGenerationChange), cancellationToken);
                    if (!read)
                    {
                        restart = true;
                        break;
                    }

                    Report(progress, attempt);
                }

                if (restart)
                {
                    restarts++;
                    continue;
                }

                return new CategoryElements(document, attempt.Generation, category, attempt.Elements,
                    attempt.Levels, ignoreGenerationChange && attempt.GenerationChanged);
            }
        }

        private static void Report(IProgress<LoadProgress> progress, LoadAttempt attempt)
        {
            progress?.Report(new LoadProgress(attempt.NextIndex, attempt.Ids.Count));
        }
    }
}
