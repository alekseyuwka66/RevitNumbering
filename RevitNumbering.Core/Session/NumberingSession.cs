using System.Diagnostics;
using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Settings;

namespace RevitNumbering.Core.Session
{
    /// <summary>
    /// The coordinator: the only class that knows every module. View models work only with it.
    /// One session serves one opening of the window. Everything runs on the main thread of Revit;
    /// async is used only to wait for a call in the Revit API context.
    /// </summary>
    public sealed class NumberingSession
    {
        private static readonly DocumentInfo NoDocument = new DocumentInfo { Kind = DocumentKind.None };

        private readonly IRevitRunner _runner;
        private readonly IRevitModel _model;
        private readonly IDocuments _documents;
        private readonly SettingsStore _store;
        private readonly ILog _log;

        /// <summary>Elements picked in the model, per category.</summary>
        private readonly List<CategoryPick> _picks = new List<CategoryPick>();

        private NumberingSettings _settings;
        private string _savedCategories;

        /// <summary>Rows visible in the window; null: all.</summary>
        private HashSet<long> _visibleIds;

        /// <summary>The pending call was cancelled because the document changed, not by the user.</summary>
        private bool _cancelledByDocument;

        public NumberingSession(IRevitRunner runner, IRevitModel model, IDocuments documents,
            SettingsStore settings, ILog log)
        {
            _runner = runner;
            _model = model;
            _documents = documents;
            _store = settings;
            _log = log;

            WindowDocument = NoDocument;
            StartupProblems = new List<Problem>();
            _documents.ActiveChanged += OnActiveChanged;
            _documents.Closed += OnDocumentClosed;
            _documents.Changed += OnModelChanged;
        }

        /// <summary>The data and the preview were updated, also from the model.</summary>
        public event Action DataChanged;

        /// <summary>The document of the window stopped or started being the active one, was closed or replaced.</summary>
        public event Action DocumentStateChanged;

        /// <summary>The settings were not read or not written.</summary>
        public event Action<SettingsNotice> SettingsNotice;

        /// <summary>The document the window data came from; Kind is None when there is no data.</summary>
        public DocumentInfo WindowDocument { get; private set; }

        public DocumentInfo ActiveDocument => _documents.Active;

        public bool IsWindowDocumentActive =>
            WindowDocument.Id != null && WindowDocument.Id.Equals(_documents.Active.Id);

        public IReadOnlyList<string> CategoryKeys => _model.CategoryKeys;

        public string CategoryKey { get; private set; }

        /// <summary>Settings of the current category; the settings panel edits this object.</summary>
        public CategorySettings Settings => _settings?.Find(CategoryKey);

        /// <summary>Elements picked in the model for the current category; null: the scope is the visible rows.</summary>
        public List<long> Picked => _picks.Find(pick => pick.CategoryKey == CategoryKey)?.Ids;

        public CategoryData Data { get; private set; }

        public Preview Preview { get; private set; }

        /// <summary>A call to Revit is posted or running.</summary>
        public bool IsBusy { get; private set; }

        /// <summary>The call is posted but Revit has not started it yet.</summary>
        public bool IsPending => _runner.IsPending;

        /// <summary>A handler of a model change failed: the table may differ from the model until Refresh.</summary>
        public bool DataMayBeStale { get; private set; }

        public bool HasUnsavedSettings => _settings != null && SerializeCategories() != _savedCategories;

        /// <summary>Not empty: the embedded defaults are incomplete and the window must not work.</summary>
        public List<Problem> StartupProblems { get; private set; }

        /// <summary>Saved position and size of the window; null: centered.</summary>
        public WindowPlacement WindowPlacement => _settings?.Window;

        /// <summary>When the window opens: settings, the last category, the active document.</summary>
        public async Task OpenAsync()
        {
            StartupProblems = SettingsStore.CheckDefaults(CategoryKeys);
            if (StartupProblems.Count > 0)
            {
                _log.Warning("[Open] embedded defaults are incomplete, the window is blocked");
                return;
            }

            SettingsLoadResult loaded = _store.Load(CategoryKeys);
            _settings = loaded.Settings;
            _savedCategories = SerializeCategories();
            CategoryKey = CategoryKeys.Contains(_settings.LastCategory) ? _settings.LastCategory : CategoryKeys[0];
            foreach (SettingsNotice notice in loaded.Notices)
            {
                SettingsNotice?.Invoke(notice);
            }

            await LoadAsync();
        }

        /// <summary>Loads the category from the active document; its own pick is restored.</summary>
        public async Task SelectCategoryAsync(string key)
        {
            if (IsBusy || _settings == null || !CategoryKeys.Contains(key))
            {
                return;
            }

            CategoryKey = key;
            _settings.LastCategory = key;
            _visibleIds = null;
            await LoadAsync();
        }

        /// <summary>Loads the active document with the current settings of the window.</summary>
        public async Task RefreshAsync()
        {
            if (IsBusy || _settings == null)
            {
                return;
            }

            await LoadAsync();
        }

        /// <summary>After the search, the template or the settings changed. No Revit call. visibleIds null: all rows.</summary>
        public void Recalculate(HashSet<long> visibleIds)
        {
            _visibleIds = visibleIds;
            Preview = Data == null ? null : NumberingCalculator.Calculate(BuildRequest(Data));
        }

        /// <summary>Lets the user pick elements of the category in the model; Esc changes nothing.</summary>
        public async Task PickAsync()
        {
            if (IsBusy || Data == null)
            {
                return;
            }

            DocumentId document = WindowDocument.Id;
            string category = CategoryKey;
            List<long> ids = await RunAsync("Pick", () => _model.PickElements(document, category));
            if (ids == null)
            {
                return;
            }

            _picks.RemoveAll(pick => pick.CategoryKey == category);
            _picks.Add(new CategoryPick { CategoryKey = category, Ids = ids });
            Recalculate(_visibleIds);
            DataChanged?.Invoke();
        }

        /// <summary>Back to the scope of visible rows.</summary>
        public void ResetPick()
        {
            _picks.RemoveAll(pick => pick.CategoryKey == CategoryKey);
            Recalculate(_visibleIds);
            DataChanged?.Invoke();
        }

        /// <summary>Zooms to the element in Revit and selects it. Does not touch the scope.</summary>
        public async Task ShowElementAsync(long id)
        {
            if (IsBusy || Data == null || !IsWindowDocumentActive)
            {
                return;
            }

            DocumentId document = WindowDocument.Id;
            await RunAsync("Show", () =>
            {
                _model.ShowElement(document, id);
                return true;
            });
        }

        /// <summary>
        /// One call to Revit: read again, calculate again, compare with what the window shows, write.
        /// Nothing is written when the model differs from what the user saw.
        /// </summary>
        public async Task<ApplyResult> ApplyAsync()
        {
            Preview shown = Preview;
            if (IsBusy || shown == null || !shown.CanApply || !IsWindowDocumentActive)
            {
                return ApplyResult.Cancelled();
            }

            DocumentId document = WindowDocument.Id;
            string category = CategoryKey;
            Stopwatch watch = Stopwatch.StartNew();
            ApplyResult result;
            IsBusy = true;
            _cancelledByDocument = false;
            try
            {
                result = await _runner.Run(() =>
                {
                    long waitedMs = watch.ElapsedMilliseconds;
                    CategoryData fresh = _model.Read(document, category);
                    Preview plan = NumberingCalculator.Calculate(BuildRequest(fresh));
                    long readMs = watch.ElapsedMilliseconds - waitedMs;
                    if (!plan.SameAs(shown))
                    {
                        return ApplyResult.DataChanged(fresh, plan);
                    }

                    List<WriteItem> items = plan.ToWrite();
                    WriteResult written = _model.Write(document, category, items);
                    long writeMs = watch.ElapsedMilliseconds - waitedMs - readMs;

                    // After the write the table shows the actual values of the model.
                    CategoryData actual = _model.Read(document, category);
                    _log.Info("[Apply] " + category + ": to write " + items.Count + ", written " + written.WrittenCount
                        + ", failed " + written.Failures.Count + "; waited " + waitedMs + " ms, read " + readMs
                        + " ms, write " + writeMs + " ms, reread " + (watch.ElapsedMilliseconds - waitedMs - readMs - writeMs)
                        + " ms");
                    return ApplyResult.Written(written, actual);
                });
            }
            catch (DocumentUnavailableException)
            {
                result = ApplyResult.DocumentUnavailable();
            }
            catch (OperationCanceledException)
            {
                result = _cancelledByDocument ? ApplyResult.DocumentUnavailable() : ApplyResult.Cancelled();
            }
            finally
            {
                IsBusy = false;
            }

            switch (result.Outcome)
            {
                case ApplyOutcome.Written:
                    foreach (WriteFailure failure in result.Write.Failures)
                    {
                        _log.Warning("[Apply] not written: " + failure.Id + ", " + failure.Reason + " " + failure.Details);
                    }

                    SetData(result.Fresh);
                    SaveSettings();
                    DataChanged?.Invoke();
                    break;
                case ApplyOutcome.DataChanged:
                    _log.Warning("[Apply] the model differs from the window, nothing is written");
                    Data = result.Fresh;
                    Preview = result.FreshPreview;
                    DataChanged?.Invoke();
                    break;
                case ApplyOutcome.DocumentUnavailable:
                    _log.Warning("[Apply] the document is closed or not active, nothing is written");
                    DocumentStateChanged?.Invoke();
                    break;
            }

            return result;
        }

        /// <summary>Cancels the posted call if Revit has not started it yet.</summary>
        public void CancelPending()
        {
            _runner.CancelPending();
        }

        /// <summary>Writes the whole settings file. false: not saved, a notice is raised.</summary>
        public bool SaveSettings()
        {
            if (_settings == null)
            {
                return false;
            }

            if (_store.Save(_settings))
            {
                _savedCategories = SerializeCategories();
                return true;
            }

            SettingsNotice?.Invoke(new SettingsNotice(
                _store.IsReadOnly ? SettingsNoticeKind.NewerSchema : SettingsNoticeKind.SaveFailed, _store.FilePath));
            return false;
        }

        /// <summary>Writes the current settings to the chosen file. false: not written, a notice is raised.</summary>
        public bool ExportSettings(string path)
        {
            if (_settings != null && _store.Export(_settings, path))
            {
                return true;
            }

            SettingsNotice?.Invoke(new SettingsNotice(SettingsNoticeKind.SaveFailed, path));
            return false;
        }

        /// <summary>Replaces all settings with a valid file; an invalid file changes nothing.</summary>
        public ImportResult ImportSettings(string path)
        {
            if (_settings == null)
            {
                return ImportResult.Invalid;
            }

            ImportResult result = _store.Import(path, CategoryKeys, out NumberingSettings imported);
            if (result == ImportResult.Imported)
            {
                // The window stays on its category and keeps its position.
                imported.LastCategory = CategoryKey;
                imported.Window = _settings.Window;
                _settings = imported;
                _savedCategories = SerializeCategories();
                Recalculate(_visibleIds);
            }

            return result;
        }

        /// <summary>
        /// When the window closes: remembers its placement and the last category, cancels the posted call
        /// and stops listening to Revit. Unsaved category settings are not written here.
        /// </summary>
        public void Close(WindowPlacement placement)
        {
            _documents.ActiveChanged -= OnActiveChanged;
            _documents.Closed -= OnDocumentClosed;
            _documents.Changed -= OnModelChanged;
            _runner.CancelPending();

            if (_settings == null || _store.IsReadOnly)
            {
                return;
            }

            NumberingSettings saved = HasUnsavedSettings ? _store.Load(CategoryKeys).Settings : _settings;
            saved.LastCategory = CategoryKey;
            saved.Window = placement ?? saved.Window;
            _store.Save(saved);
        }

        /// <summary>Called when a handler of a Revit event failed.</summary>
        public void MarkDataMayBeStale()
        {
            DataMayBeStale = true;
            DocumentStateChanged?.Invoke();
        }

        /// <summary>Loads the current category of the active document through the runner.</summary>
        private async Task LoadAsync()
        {
            DocumentInfo active = _documents.Active;
            if (active.Kind != DocumentKind.Project)
            {
                ClearData(active);
                return;
            }

            string category = CategoryKey;
            Stopwatch watch = Stopwatch.StartNew();
            CategoryData data = await RunAsync("Load", () => _model.Read(active.Id, category));
            if (data != null)
            {
                AcceptLoaded(active, category, data, watch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Loads the active document right now. Called from handlers of Revit events, where the API
        /// context is already available.
        /// </summary>
        private void LoadInContext()
        {
            DocumentInfo active = _documents.Active;
            if (active.Kind != DocumentKind.Project || _settings == null)
            {
                ClearData(active);
                return;
            }

            Stopwatch watch = Stopwatch.StartNew();
            CategoryData data = _model.Read(active.Id, CategoryKey);
            AcceptLoaded(active, CategoryKey, data, watch.ElapsedMilliseconds);
        }

        private void AcceptLoaded(DocumentInfo document, string category, CategoryData data, long elapsedMs)
        {
            if (!document.Id.Equals(WindowDocument.Id))
            {
                // Picks belong to the previous document.
                _picks.Clear();
            }

            WindowDocument = document;
            DataMayBeStale = false;
            SetData(data);
            _log.Info("[Load] " + category + ": " + data.Items.Count + " elements, " + elapsedMs + " ms");
            DocumentStateChanged?.Invoke();
            DataChanged?.Invoke();
        }

        private void ClearData(DocumentInfo active)
        {
            WindowDocument = active.Kind == DocumentKind.Family ? active : NoDocument;
            Data = null;
            Preview = null;
            _picks.Clear();
            DocumentStateChanged?.Invoke();
            DataChanged?.Invoke();
        }

        private void SetData(CategoryData data)
        {
            data.Items.Sort((a, b) => a.Id.CompareTo(b.Id));
            Data = data;
            Recalculate(_visibleIds);
        }

        /// <summary>
        /// One call through the runner. Returns default when the call was cancelled or the document
        /// is no longer available.
        /// </summary>
        private async Task<T> RunAsync<T>(string operation, Func<T> action)
        {
            IsBusy = true;
            _cancelledByDocument = false;
            try
            {
                return await _runner.Run(action);
            }
            catch (OperationCanceledException)
            {
                _log.Info("[" + operation + "] cancelled before Revit started it");
                return default;
            }
            catch (DocumentUnavailableException)
            {
                _log.Warning("[" + operation + "] the document is closed or not active");
                IsBusy = false;
                DocumentStateChanged?.Invoke();
                return default;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private NumberingRequest BuildRequest(CategoryData data)
        {
            List<long> picked = Picked;
            IEnumerable<long> scope = picked ?? (IEnumerable<long>)_visibleIds ?? data.Items.Select(item => item.Id);
            return new NumberingRequest
            {
                Items = data.Items,
                Levels = data.Levels,
                Scope = new HashSet<long>(scope),
                Settings = Settings,
            };
        }

        private string SerializeCategories()
        {
            return SettingsStore.Serialize(new NumberingSettings { Categories = _settings.Categories });
        }

        /// <summary>The user switched to another document or view.</summary>
        private void OnActiveChanged()
        {
            SafeRunner.Run(_log, "ActiveChanged", () =>
            {
                CancelPendingForDocument();
                if (Data == null && !_runner.IsExecuting)
                {
                    // The window has no document: the active project becomes its document at once.
                    LoadInContext();
                }
                else
                {
                    DocumentStateChanged?.Invoke();
                }
            }, _ => MarkDataMayBeStale());
        }

        private void OnDocumentClosed(DocumentId document)
        {
            SafeRunner.Run(_log, "DocumentClosed", () =>
            {
                if (!document.Equals(WindowDocument.Id))
                {
                    return;
                }

                CancelPendingForDocument();
                DocumentInfo active = _documents.Active;
                if (active.Kind == DocumentKind.Project && !document.Equals(active.Id) && !_runner.IsExecuting)
                {
                    // The list is cleared and the active project is loaded at once.
                    WindowDocument = NoDocument;
                    LoadInContext();
                }
                else
                {
                    ClearData(document.Equals(active.Id) ? NoDocument : active);
                }
            }, _ => MarkDataMayBeStale());
        }

        /// <summary>
        /// Live update. The event comes in the API context, so the changed elements are read right here.
        /// </summary>
        private void OnModelChanged(DocumentId document, ModelChanges changes)
        {
            // Our own write raises an event per element; the category is read again after it.
            if (_runner.IsExecuting || Data == null || !document.Equals(WindowDocument.Id))
            {
                return;
            }

            SafeRunner.Run(_log, "ModelChanged", () =>
            {
                var touched = new HashSet<long>(changes.Deleted);
                touched.UnionWith(changes.Added);
                touched.UnionWith(changes.Modified);
                int removed = Data.Items.RemoveAll(item => touched.Contains(item.Id));

                // Changed elements are read again; those that left the category are not returned.
                List<long> toRead = changes.Added.Concat(changes.Modified).ToList();
                List<NumberingItem> fresh = toRead.Count == 0
                    ? new List<NumberingItem>()
                    : _model.ReadItems(document, CategoryKey, toRead);
                Data.Items.AddRange(fresh);

                if (changes.LevelsChanged)
                {
                    Data.Levels = _model.ReadLevels(document);
                }

                if (removed == 0 && fresh.Count == 0 && !changes.LevelsChanged)
                {
                    return;
                }

                SetData(Data);
                DataChanged?.Invoke();
            }, _ => MarkDataMayBeStale());
        }

        /// <summary>A call that waits for Revit belongs to the previous state of documents: cancel it at once.</summary>
        private void CancelPendingForDocument()
        {
            if (_runner.IsPending)
            {
                _cancelledByDocument = true;
                _runner.CancelPending();
            }
        }

        private sealed class CategoryPick
        {
            public string CategoryKey { get; set; }

            public List<long> Ids { get; set; }
        }
    }
}
