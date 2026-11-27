using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Model;

namespace RevitNumbering.Tests.Session
{
    // Runs the posted action at once, as if Revit were free.
    // With HoldCalls the action waits, as if Revit were busy, until RunPosted.
    internal sealed class FakeRunner : IRevitRunner
    {
        private Action _posted;

        public bool HoldCalls { get; set; }

        public bool Run(Action action)
        {
            if (HoldCalls)
            {
                _posted = action;
            }
            else
            {
                action();
            }

            return true;
        }

        public bool CancelPending()
        {
            bool posted = _posted != null;
            _posted = null;
            return posted;
        }

        public void RunPosted()
        {
            Action action = _posted;
            _posted = null;
            action?.Invoke();
        }
    }

    internal sealed class FakeDocuments : IDocuments
    {
        public DocumentInfo Active { get; private set; } = Project(1);

        public event Action ActiveChanged;

        public event Action<HistoryChange> NumberingHistoryChanged;

        public static DocumentInfo Project(int id)
        {
            return new DocumentInfo { Id = new DocumentId(id), Kind = DocumentKind.Project };
        }

        // The user switched to another document: Revit raises the event in the API context.
        public void Activate(DocumentInfo document)
        {
            Active = document;
            ActiveChanged?.Invoke();
        }

        // The numbering step was undone or redone in Revit.
        public void RaiseHistory(HistoryChange change)
        {
            NumberingHistoryChanged?.Invoke(change);
        }

        // The active document changed, but the event has not come yet.
        public void SwitchUnnoticed(DocumentInfo document)
        {
            Active = document;
        }
    }

    // A model in memory. Read returns copies of Items, Write records what it was asked to write.
    // Like the Revit layer, it serves only the active document.
    internal sealed class FakeRevitModel : IRevitModel
    {
        private readonly FakeDocuments _documents;

        public FakeRevitModel(FakeDocuments documents)
        {
            _documents = documents;
        }

        public List<NumberingItem> Items { get; } = new List<NumberingItem>();

        public List<LevelInfo> Levels { get; } = new List<LevelInfo>();

        // False: every call throws, as when the document is closed.
        public bool DocumentAvailable { get; set; } = true;

        // Null: Write was not called.
        public List<WriteItem> Written { get; private set; }

        public IWriteProgress LastProgress { get; private set; }

        // Items per batch of Write. The progress is called after every batch. 0: one batch.
        public int BatchSize { get; set; }

        public IReadOnlyList<string> CategoryKeys { get; } = new[] { "Walls", "Doors", "Windows", "Rooms" };

        public CategoryData Read(DocumentId document, string categoryKey)
        {
            RequireActive(document);
            return new CategoryData
            {
                Items = Items.Select(Copy).ToList(),
                Levels = Levels.ToList(),
            };
        }

        // What the user picks in the model. null: Esc.
        public List<long> PickResult { get; set; }

        public List<long> PickElements(DocumentId document, string categoryKey)
        {
            return PickResult;
        }

        public void ShowElement(DocumentId document, long id)
        {
        }

        public WriteResult Write(DocumentId document, string categoryKey, List<WriteItem> items,
            IWriteProgress progress)
        {
            RequireActive(document);
            LastProgress = progress;
            Written = new List<WriteItem>();
            var result = new WriteResult();
            var before = new Dictionary<long, string>();
            int size = BatchSize > 0 ? BatchSize : Math.Max(items.Count, 1);
            for (int start = 0; start < items.Count; start += size)
            {
                foreach (WriteItem item in items.GetRange(start, Math.Min(size, items.Count - start)))
                {
                    NumberingItem target = Items.Single(i => i.Id == item.Id);
                    before[item.Id] = target.Value;
                    target.Value = item.Value;
                    Written.Add(item);
                    result.WrittenCount++;
                }

                int done = start + Math.Min(size, items.Count - start);
                if (progress != null && !progress.Continue(done, items.Count) && done < items.Count)
                {
                    foreach (KeyValuePair<long, string> old in before)
                    {
                        Items.Single(i => i.Id == old.Key).Value = old.Value;
                    }

                    Written = new List<WriteItem>();
                    return new WriteResult { Cancelled = true };
                }
            }

            return result;
        }

        private void RequireActive(DocumentId document)
        {
            if (!DocumentAvailable || !document.Equals(_documents.Active.Id))
            {
                throw new DocumentUnavailableException();
            }
        }

        private static NumberingItem Copy(NumberingItem item)
        {
            return new NumberingItem
            {
                Id = item.Id,
                Name = item.Name,
                LevelId = item.LevelId,
                X = item.X,
                Y = item.Y,
                Value = item.Value,
                ConflictGroup = item.ConflictGroup,
                Lock = item.Lock,
                LockedBy = item.LockedBy,
            };
        }
    }
}
