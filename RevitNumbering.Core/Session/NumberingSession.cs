using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Settings;

namespace RevitNumbering.Core.Session
{
    // The coordinator: the only class that knows all modules. View models talk only to it.
    // One session lives as long as one window. The window and Revit share one thread, so a command
    // posts its work through the runner, and the work runs in the API context when Revit is free.
    // The window follows the active project: its data always comes from the active project.
    public sealed class NumberingSession
    {
        private static readonly DocumentInfo NoDocument = new DocumentInfo { Kind = DocumentKind.None };

        private readonly IRevitRunner _runner;
        private readonly IRevitModel _model;
        private readonly IDocuments _documents;
        private readonly SettingsStore _store;
        private readonly FileLog _log;
        private readonly NumberingSettings _settings;

        // Elements picked in the model, per category.
        private readonly List<CategoryPick> _picks = new List<CategoryPick>();

        // Rows visible after the search. Null: all rows.
        private HashSet<long> _visibleIds;

        // Runs after the posted work. It also runs when the work is dropped because the document changed,
        // but not after an unexpected exception.
        private Action _after;

        // Reads the settings first. If that fails, nothing is subscribed to Revit.
        public NumberingSession(IRevitRunner runner, IRevitModel model, IDocuments documents,
            SettingsStore store, FileLog log)
        {
            _runner = runner;
            _model = model;
            _documents = documents;
            _store = store;
            _log = log;

            LoadedSettings = store.Load(model.CategoryKeys);
            _settings = LoadedSettings.Settings;
            CategoryKey = CategoryKeys.Contains(_settings.LastCategory) ? _settings.LastCategory : CategoryKeys[0];
            WindowDocument = NoDocument;
            _documents.ActiveChanged += OnActiveChanged;
            _documents.NumberingHistoryChanged += OnNumberingHistoryChanged;
        }

        public event Action DataChanged;

        // The session became busy or free, or the table may be stale: refresh the buttons and the status.
        public event Action StateChanged;

        public event Action<Exception> Failed;

        // The table is going to be reloaded because the numbering was undone or redone in Revit.
        public event Action<HistoryChange> HistoryChanged;

        // What loading the settings gave. The window warns the user about an unreadable file.
        public SettingsLoadResult LoadedSettings { get; }

        public string SettingsFilePath => _store.FilePath;

        // The document that the window data came from. Kind is None when there is no data.
        public DocumentInfo WindowDocument { get; private set; }

        public DocumentInfo ActiveDocument => _documents.Active;

        public bool IsWindowDocumentActive =>
            WindowDocument.Id != null && WindowDocument.Id.Equals(_documents.Active.Id);

        public IReadOnlyList<string> CategoryKeys => _model.CategoryKeys;

        public string CategoryKey { get; private set; }

        public CategorySettings Settings => _settings.Find(CategoryKey);

        // Elements picked in the model for the current category. Null: the scope is the visible rows.
        public List<long> Picked => _picks.Find(pick => pick.CategoryKey == CategoryKey)?.Ids;

        public CategoryData Data { get; private set; }

        // Set by Recalculate. Null until then, and when there is no data.
        public Preview Preview { get; private set; }

        public bool IsBusy { get; private set; }

        // The posted call has started. CancelPending can no longer drop it.
        public bool IsRunning { get; private set; }

        // A Revit event handler failed, so the table may differ from the model until Refresh.
        public bool DataMayBeStale { get; private set; }

        public void Refresh()
        {
            Post("Load", LoadActive);
        }

        public void SelectCategory(string key)
        {
            if (IsBusy || !CategoryKeys.Contains(key))
            {
                return;
            }

            CategoryKey = key;
            _settings.LastCategory = key;
            _visibleIds = null;

            // The table must not show the old category while the new one is being read.
            Data = null;
            Preview = null;
            DataChanged?.Invoke();
            Refresh();
        }

        // No Revit call here. visibleIds null means all rows.
        public void Recalculate(HashSet<long> visibleIds)
        {
            _visibleIds = visibleIds;
            Preview = Data == null ? null : NumberingCalculator.Calculate(Data, Scope(Data), Settings);
        }

        public void Pick()
        {
            if (Data == null || !IsWindowDocumentActive)
            {
                return;
            }

            DocumentId document = WindowDocument.Id;
            string category = CategoryKey;
            Post("Pick", () => KeepPick(category, _model.PickElements(document, category)));
        }

        public void ResetPick()
        {
            _picks.RemoveAll(pick => pick.CategoryKey == CategoryKey);
            DataChanged?.Invoke();
        }

        public void ShowElement(long id)
        {
            if (Data == null || !IsWindowDocumentActive)
            {
                return;
            }

            DocumentId document = WindowDocument.Id;
            Post("Show", () => _model.ShowElement(document, id));
        }

        // One call to Revit: read again, calculate again, compare with what the window shows, then write.
        // Nothing is written if the model differs from what the user saw. done gets the result.
        public void Apply(Action<ApplyResult> done, IWriteProgress progress = null)
        {
            Preview shown = Preview;
            if (shown == null || !shown.CanApply || !IsWindowDocumentActive)
            {
                return;
            }

            DocumentId document = WindowDocument.Id;
            string category = CategoryKey;

            // It stays DocumentUnavailable if the document is gone before or during the call.
            var result = new ApplyResult(ApplyOutcome.DocumentUnavailable);
            Post("Apply", () => result = ReadCompareWrite(document, category, shown, progress), () => done(result));
        }

        public bool CancelPending()
        {
            if (!_runner.CancelPending())
            {
                return false;
            }

            _after = null;
            IsBusy = false;
            StateChanged?.Invoke();
            return true;
        }

        // Returns false when writing failed (see the log). An unreadable file is left as it is,
        // so the user can fix or delete it.
        public bool SaveSettings()
        {
            return LoadedSettings.FileUnreadable || _store.Save(_settings);
        }

        public void Close()
        {
            _documents.ActiveChanged -= OnActiveChanged;
            _documents.NumberingHistoryChanged -= OnNumberingHistoryChanged;
            _runner.CancelPending();
            SaveSettings();
        }

        // The body of Apply. Runs in the API context.
        private ApplyResult ReadCompareWrite(DocumentId document, string category, Preview shown,
            IWriteProgress progress)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            CategoryData fresh = _model.Read(document, category);
            long readMs = watch.ElapsedMilliseconds;
            Preview plan = NumberingCalculator.Calculate(fresh, Scope(fresh), Settings);
            bool same = plan.SameAs(shown);
            long calculateMs = watch.ElapsedMilliseconds - readMs;
            if (!same)
            {
                _log.Warning("[Apply] the model differs from the window, nothing is written (read " + readMs
                    + " ms, calculate " + calculateMs + " ms)");
                SetData(fresh);
                return new ApplyResult(ApplyOutcome.DataChanged);
            }

            List<WriteItem> items = plan.ToWrite();
            long writeStartedAt = watch.ElapsedMilliseconds;
            WriteResult written = _model.Write(document, category, items, progress);
            long writeMs = watch.ElapsedMilliseconds - writeStartedAt;

            // After the write, the table shows the real values from the model.
            long rereadStartedAt = watch.ElapsedMilliseconds;
            SetData(_model.Read(document, category));
            long rereadMs = watch.ElapsedMilliseconds - rereadStartedAt;

            string timings = "read " + readMs + " ms, calculate " + calculateMs + " ms, write " + writeMs
                + " ms, reread and update " + rereadMs + " ms; checks " + written.ChecksMs + " ms, transactions "
                + written.TransactionsMs + " ms in " + written.Batches + " batches (element by element "
                + written.ElementByElementBatches + "), slowest batch " + written.SlowestBatchMs
                + " ms, window pauses " + written.ProgressMs + " ms, finish " + written.FinishMs + " ms";

            if (written.Cancelled)
            {
                _log.Info("[Apply] " + category + ": cancelled by the user, rolled back; " + timings);
                return new ApplyResult(ApplyOutcome.Cancelled);
            }

            _log.Info("[Apply] " + category + ": to write " + items.Count + ", written " + written.WrittenCount
                + ", failed " + written.Failures.Count + "; " + timings);
            LogFailureDetails(written.Failures);
            return new ApplyResult(ApplyOutcome.Written, written);
        }

        // Only failures with a Revit text go to the log. The user already sees the other reasons in the summary.
        private void LogFailureDetails(List<WriteFailure> failures)
        {
            foreach (WriteFailure failure in failures.Where(failure => !string.IsNullOrEmpty(failure.Details)))
            {
                _log.Warning("[Apply] not written: " + failure.Id + ", " + failure.Reason + " " + failure.Details);
            }
        }

        private void KeepPick(string category, List<long> ids)
        {
            if (ids == null)
            {
                return;
            }

            _picks.RemoveAll(pick => pick.CategoryKey == category);
            _picks.Add(new CategoryPick { CategoryKey = category, Ids = ids });
            DataChanged?.Invoke();
        }

        private void Post(string operation, Action work, Action after = null)
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            _after = after;
            StateChanged?.Invoke();
            if (!_runner.Run(() => Execute(operation, work)))
            {
                _log.Warning("[" + operation + "] Revit refused the call");
                _after = null;
                IsBusy = false;
                StateChanged?.Invoke();
            }
        }

        private void Execute(string operation, Action work)
        {
            bool ended = false;
            IsRunning = true;
            StateChanged?.Invoke();
            SafeRunner.Run(_log, operation, () =>
            {
                RunInDocument(operation, work);
                ended = true;
            }, ex => Failed?.Invoke(ex));

            Action after = _after;
            _after = null;
            IsRunning = false;
            IsBusy = false;

            // The active document may have changed while the work waited or ran.
            SafeRunner.Run(_log, "FollowActiveDocument", FollowActiveDocument, _ => MarkDataMayBeStale());
            StateChanged?.Invoke();
            if (ended)
            {
                after?.Invoke();
            }
        }

        private void RunInDocument(string operation, Action work)
        {
            try
            {
                work();
            }
            catch (DocumentUnavailableException)
            {
                // A closed or inactive document is a normal end of the work, not a failure.
                _log.Warning("[" + operation + "] the document is closed or not active");
            }
        }

        // Another project is loaded at once. Without a project the table is empty.
        private void FollowActiveDocument()
        {
            if (_documents.Active.Kind != DocumentKind.Project)
            {
                if (Data != null)
                {
                    ClearData();
                }
            }
            else if (!IsWindowDocumentActive)
            {
                LoadActive();
            }
        }

        private void LoadActive()
        {
            DocumentInfo active = _documents.Active;
            if (active.Kind != DocumentKind.Project)
            {
                ClearData();
                return;
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            CategoryData data = _model.Read(active.Id, CategoryKey);
            long readMs = watch.ElapsedMilliseconds;
            if (!active.Id.Equals(WindowDocument.Id))
            {
                // Picks belong to the previous document.
                _picks.Clear();
            }

            WindowDocument = active;
            DataMayBeStale = false;
            SetData(data);
            _log.Info("[Load] " + CategoryKey + ": " + data.Items.Count + " elements, read " + readMs
                + " ms, data handlers " + (watch.ElapsedMilliseconds - readMs) + " ms");
        }

        private void ClearData()
        {
            WindowDocument = NoDocument;
            Data = null;
            Preview = null;
            _picks.Clear();
            DataChanged?.Invoke();
        }

        // Items are kept sorted by id. The preview rows use the same order.
        private void SetData(CategoryData data)
        {
            data.Items.Sort((a, b) => a.Id.CompareTo(b.Id));
            Data = data;
            Preview = null;
            DataChanged?.Invoke();
        }

        // Elements picked in the model, narrowed to the visible rows. Without a pick: the visible rows, or all elements.
        private HashSet<long> Scope(CategoryData data)
        {
            List<long> picked = Picked;
            if (picked != null)
            {
                var scope = new HashSet<long>(picked);
                if (_visibleIds != null)
                {
                    scope.IntersectWith(_visibleIds);
                }

                return scope;
            }

            return _visibleIds ?? new HashSet<long>(data.Items.Select(item => item.Id));
        }

        private void MarkDataMayBeStale()
        {
            DataMayBeStale = true;
            StateChanged?.Invoke();
        }

        private void OnActiveChanged()
        {
            SafeRunner.Run(_log, "ActiveChanged", HandleActiveChanged, _ => MarkDataMayBeStale());
        }

        private void OnNumberingHistoryChanged(HistoryChange change)
        {
            SafeRunner.Run(_log, "NumberingHistoryChanged", () => HandleNumberingHistoryChanged(change),
                _ => MarkDataMayBeStale());
        }

        private void HandleNumberingHistoryChanged(HistoryChange change)
        {
            if (!IsWindowDocumentActive)
            {
                return;
            }

            // A posted call reads the model itself when it runs.
            if (IsBusy)
            {
                MarkDataMayBeStale();
                return;
            }

            HistoryChanged?.Invoke(change);
            Refresh();
        }

        private void HandleActiveChanged()
        {
            // Our own call is running (Revit raised the event inside it). It follows the document when it ends.
            if (IsBusy && !_runner.CancelPending())
            {
                return;
            }

            // A posted call belonged to the previous document. It was dropped, and its caller is told.
            Action after = _after;
            _after = null;
            IsBusy = false;
            FollowActiveDocument();
            StateChanged?.Invoke();
            after?.Invoke();
        }

        private sealed class CategoryPick
        {
            public string CategoryKey { get; set; }

            public List<long> Ids { get; set; }
        }
    }
}
