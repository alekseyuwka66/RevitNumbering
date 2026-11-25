using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Model;

namespace RevitNumbering.Tests.Session
{
    /// <summary>Runs the action at once, as if Revit were free.</summary>
    internal sealed class FakeRunner : IRevitRunner
    {
        public bool IsExecuting { get; private set; }

        public bool IsPending => false;

        public Task<T> Run<T>(Func<T> action)
        {
            IsExecuting = true;
            try
            {
                return Task.FromResult(action());
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
            finally
            {
                IsExecuting = false;
            }
        }

        public void CancelPending()
        {
        }
    }

    /// <summary>A model in memory: Read returns copies of Items, Write records what it was asked to write.</summary>
    internal sealed class FakeRevitModel : IRevitModel
    {
        public List<NumberingItem> Items { get; set; } = new List<NumberingItem>();

        public List<LevelInfo> Levels { get; set; } = new List<LevelInfo>();

        /// <summary>false: every call for the document throws, as when it is closed or not active.</summary>
        public bool DocumentAvailable { get; set; } = true;

        /// <summary>null: Write was not called.</summary>
        public List<WriteItem> Written { get; private set; }

        public IReadOnlyList<string> CategoryKeys { get; } = new[] { "Walls", "Doors", "Windows", "Rooms" };

        public CategoryData Read(DocumentId document, string categoryKey)
        {
            RequireDocument();
            return new CategoryData
            {
                Items = Items.Select(Copy).ToList(),
                Levels = Levels.ToList(),
            };
        }

        public List<NumberingItem> ReadItems(DocumentId document, string categoryKey, List<long> ids)
        {
            return Items.Where(item => ids.Contains(item.Id)).Select(Copy).ToList();
        }

        public List<LevelInfo> ReadLevels(DocumentId document)
        {
            return Levels.ToList();
        }

        public List<long> PickElements(DocumentId document, string categoryKey)
        {
            return null;
        }

        public void ShowElement(DocumentId document, long id)
        {
        }

        public WriteResult Write(DocumentId document, string categoryKey, List<WriteItem> items)
        {
            RequireDocument();
            Written = items;
            foreach (WriteItem item in items)
            {
                Items.Single(i => i.Id == item.Id).Value = item.Value;
            }

            return new WriteResult { WrittenCount = items.Count };
        }

        private void RequireDocument()
        {
            if (!DocumentAvailable)
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

    internal sealed class FakeDocuments : IDocuments
    {
        public DocumentInfo Active { get; set; } =
            new DocumentInfo { Id = new DocumentId(1), Kind = DocumentKind.Project, Title = "Project" };

        public event Action ActiveChanged;

        public event Action<DocumentId> Closed;

        public event Action<DocumentId, ModelChanges> Changed;

        public void RaiseActiveChanged()
        {
            ActiveChanged?.Invoke();
        }

        public void RaiseClosed(DocumentId document)
        {
            Closed?.Invoke(document);
        }

        public void RaiseChanged(DocumentId document, ModelChanges changes)
        {
            Changed?.Invoke(document, changes);
        }
    }
}
