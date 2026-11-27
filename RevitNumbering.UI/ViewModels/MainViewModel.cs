using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Session;
using RevitNumbering.Core.Settings;
using RevitNumbering.UI.Resources;
using RevitNumbering.UI.Views;

namespace RevitNumbering.UI.ViewModels
{
    // The model of the main window. It works only with the session.
    // Every command runs through SafeRunner, because an exception that leaves a command would bring Revit down.
    public sealed partial class MainViewModel : ObservableObject
    {
        private static readonly IReadOnlyList<TableRow> NoRows = new List<TableRow>();

        private readonly NumberingSession _session;
        private readonly FileLog _log;

        private IReadOnlyList<TableRow> _rows = NoRows;

        // Rows are filtered on every change of the text.
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _statusText = string.Empty;

        [ObservableProperty]
        private bool _isSearchByColumn;

        [ObservableProperty]
        private Option<SearchColumn> _selectedSearchColumn;

        // The result of the last operation. It stays in the status line until the next operation.
        private string _message = string.Empty;

        private bool _scopeCleared;

        private bool _clearingSearch;

        public MainViewModel(NumberingSession session, FileLog log)
        {
            _session = session;
            _log = log;

            Categories = session.CategoryKeys
                .Select(key => new Option<string>(key, TextLookup.Category(key)))
                .ToList();
            SearchColumns = new List<Option<SearchColumn>>
            {
                new Option<SearchColumn>(SearchColumn.Id, Texts.ColumnId),
                new Option<SearchColumn>(SearchColumn.Name, Texts.ColumnName),
                new Option<SearchColumn>(SearchColumn.Level, Texts.ColumnLevel),
                new Option<SearchColumn>(SearchColumn.Current, Texts.ColumnCurrent),
            };
            _selectedSearchColumn = SearchColumns[0];
            Settings = new CategorySettingsViewModel();
            Settings.Attach(session.Settings);
            Settings.Changed += OnSettingsChanged;

            _session.DataChanged += OnDataChanged;
            _session.StateChanged += OnStateChanged;
            _session.Failed += OnError;
            _session.HistoryChanged += OnHistoryChanged;
        }

        // The window is hidden while the user picks in the model. It comes back when the call ends.
        private bool _windowHidden;

        private bool _isWriting;

        private bool _cancelRequested;

        // Set by the window itself.
        public Dialogs Dialogs { get; set; }

        public List<Option<string>> Categories { get; }

        public List<Option<SearchColumn>> SearchColumns { get; }

        public bool IsSearchAll
        {
            get => !IsSearchByColumn;
            set
            {
                if (value)
                {
                    IsSearchByColumn = false;
                }
            }
        }

        public CategorySettingsViewModel Settings { get; }

        // The collection is replaced as a whole after every recalculation.
        public IReadOnlyList<TableRow> Rows => _rows;

        public Option<string> SelectedCategory
        {
            get => Categories.Find(option => option.Value == _session.CategoryKey);
            set
            {
                if (value != null && value.Value != _session.CategoryKey)
                {
                    SafeRunner.Run(_log, "SelectCategory", () => SelectCategory(value.Value), OnError);
                }
            }
        }

        public bool CanCancel => _isWriting && _session.IsBusy;

        // While a call is posted or running, only Cancel is available.
        public bool CanEdit => !_session.IsBusy;

        public bool CanPick => CanEdit && _session.Data != null && _session.IsWindowDocumentActive;

        public bool HasPick => CanEdit && _session.Picked != null;

        public bool CanApply => CanEdit && _session.IsWindowDocumentActive
            && _session.Preview != null && _session.Preview.CanApply;

        public bool IsRefreshVisible => _session.ActiveDocument.Kind == DocumentKind.Project;

        public bool CanRefresh => CanEdit;

        // The table shows another document than the active one, until the window follows it. It is greyed out.
        public bool IsTableStale => _session.Data != null && !_session.IsWindowDocumentActive;

        public string PlaceholderText
        {
            get
            {
                switch (_session.ActiveDocument.Kind)
                {
                    case DocumentKind.Family:
                        return Texts.PlaceholderFamily;
                    case DocumentKind.None:
                        return Texts.PlaceholderNoProject;
                }

                if (_session.Data == null)
                {
                    return _session.IsBusy ? Texts.PlaceholderLoading : Texts.PlaceholderNoData;
                }

                if (_session.Data.Items.Count == 0)
                {
                    return Texts.PlaceholderEmptyCategory;
                }

                return _rows.Count == 0 ? Texts.PlaceholderNothingFound : string.Empty;
            }
        }

        public string ScopeText
        {
            get
            {
                Preview preview = _session.Preview;
                if (preview == null)
                {
                    return string.Empty;
                }

                int inScope = preview.Rows.Count(row => row.Status != RowStatus.OutOfScope);
                List<long> picked = _session.Picked;
                if (picked == null)
                {
                    return _scopeCleared ? Texts.ScopeNone : string.Format(Texts.ScopeVisible, inScope);
                }

                if (!IsSearching)
                {
                    return string.Format(Texts.ScopePicked, inScope);
                }

                var pickedIds = new HashSet<long>(picked);
                int pickedCount = _session.Data.Items.Count(item => pickedIds.Contains(item.Id));
                return string.Format(Texts.ScopePickedFound, pickedCount, inScope);
            }
        }

        private bool IsSearching => (SearchText ?? string.Empty).Trim().Length > 0;

        public string CountersText
        {
            get
            {
                Preview preview = _session.Preview;
                return preview == null
                    ? string.Empty
                    : string.Format(Texts.Counters, preview.ChangeCount, preview.LockedCount, preview.Skipped.Count);
            }
        }

        public string SkippedHint => _session.Preview?.Skipped.Count > 0 ? Texts.SkippedHint : string.Empty;

        public void Close()
        {
            SafeRunner.Run(_log, "Close", CloseSession);
        }

        private void CloseSession()
        {
            _session.DataChanged -= OnDataChanged;
            _session.StateChanged -= OnStateChanged;
            _session.Failed -= OnError;
            _session.HistoryChanged -= OnHistoryChanged;
            _session.Close();
        }

        // Window commands: the generated XxxCommand calls the method, and the method runs the work through SafeRunner.
        [RelayCommand]
        private void Open() => SafeRunner.Run(_log, nameof(Open), OpenCore, OnError);

        [RelayCommand]
        private void Pick() => SafeRunner.Run(_log, nameof(Pick), PickCore, OnError);

        [RelayCommand]
        private void ResetPick() => SafeRunner.Run(_log, nameof(ResetPick), ResetPickCore, OnError);

        [RelayCommand]
        private void Refresh() => SafeRunner.Run(_log, nameof(Refresh), RefreshCore, OnError);

        [RelayCommand]
        private void Apply() => SafeRunner.Run(_log, nameof(Apply), ApplyCore, OnError);

        [RelayCommand]
        private void Cancel() => SafeRunner.Run(_log, nameof(Cancel), CancelCore, OnError);

        [RelayCommand]
        private void ShowElement(TableRow row) => SafeRunner.Run(_log, nameof(ShowElement), () => ShowElementCore(row), OnError);

        partial void OnSearchTextChanged(string value)
        {
            if (_clearingSearch)
            {
                return;
            }

            _scopeCleared = false;
            SafeRunner.Run(_log, "Search", RebuildRows, OnError);
        }

        partial void OnIsSearchByColumnChanged(bool value)
        {
            OnPropertyChanged(nameof(IsSearchAll));
            SafeRunner.Run(_log, "Search", RebuildRows, OnError);
        }

        partial void OnSelectedSearchColumnChanged(Option<SearchColumn> value)
        {
            SafeRunner.Run(_log, "Search", RebuildRows, OnError);
        }

        private void OnHistoryChanged(HistoryChange change)
        {
            SafeRunner.Run(_log, "HistoryChanged", () => ShowHistoryChanged(change), OnError);
        }

        private void ShowHistoryChanged(HistoryChange change)
        {
            _message = change == HistoryChange.Undone ? Texts.StatusUndone : Texts.StatusRedone;
            StatusText = Texts.StatusLoading;
        }

        private void OnError(Exception exception)
        {
            Dialogs?.ShowError(_log.FilePath);
        }

        // Starts an operation that calls Revit. The status is drawn before the call is posted,
        // because Revit may take the thread for a long time as soon as it gets the call.
        private void Start(string status, Action operation)
        {
            _message = string.Empty;
            StatusText = status;
            RefreshState();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
            operation();
        }

        private void OpenCore()
        {
            ReportLoadedSettings();
            Start(Texts.StatusLoading, _session.Refresh);
        }

        private void ReportLoadedSettings()
        {
            SettingsLoadResult loaded = _session.LoadedSettings;
            if (loaded.FileUnreadable)
            {
                Dialogs.ShowMessage(string.Format(Texts.SettingsUnreadable, _session.SettingsFilePath));
            }

            if (loaded.ResetCategories.Count > 0)
            {
                Dialogs.ShowMessage(string.Format(Texts.SettingsCategoriesReset,
                    string.Join(", ", loaded.ResetCategories.Select(TextLookup.Category))));
            }
        }

        private void SelectCategory(string categoryKey)
        {
            Start(Texts.StatusLoading, () => _session.SelectCategory(categoryKey));
            Settings.Attach(_session.Settings);
            RebuildRows();
        }

        private void ResetPickCore()
        {
            _scopeCleared = false;
            _session.ResetPick();
        }

        private void ClearSearch()
        {
            _clearingSearch = true;
            SearchText = string.Empty;
            _clearingSearch = false;
            _scopeCleared = true;
            RebuildRows();
        }

        private void RefreshCore()
        {
            _scopeCleared = false;
            Start(Texts.StatusLoading, _session.Refresh);
        }

        private void PickCore()
        {
            if (CanPick)
            {
                // The window is hidden while the user picks in the model. ShowState brings it back
                // when the call ends (picked, Esc, error or refusal).
                _windowHidden = true;
                Dialogs?.HideWindow();
                Start(Texts.StatusPicking, _session.Pick);
            }
        }

        private void ShowElementCore(TableRow row)
        {
            if (row != null && !_session.IsBusy && !IsTableStale)
            {
                _session.ShowElement(row.Id);

                // Minimize the window, so the element is visible in Revit.
                Dialogs?.MinimizeWindow();
            }
        }

        private void ApplyCore()
        {
            if (!CanApply)
            {
                return;
            }

            // Keep the names and the locked rows for the summary: the data is replaced during the call.
            List<NumberingItem> itemsBefore = _session.Data.Items;
            List<PreviewRow> locked = _session.Preview.Rows.Where(row => row.Status == RowStatus.Locked).ToList();
            _isWriting = true;
            _cancelRequested = false;
            Start(string.Format(Texts.StatusWriting, _session.Preview.ChangeCount),
                () => _session.Apply(result => SafeRunner.Run(_log, "Applied",
                    () => ShowApplied(result, itemsBefore, locked), OnError), new WriteProgress(this)));
        }

        private void ShowApplied(ApplyResult result, List<NumberingItem> itemsBefore, List<PreviewRow> locked)
        {
            switch (result.Outcome)
            {
                case ApplyOutcome.Written:
                    ClearSearch();
                    ShowMessage(string.Format(Texts.StatusWritten,
                        result.Write.WrittenCount, result.Write.Failures.Count + locked.Count));
                    Dialogs.ShowSummary(BuildSummary(result.Write, itemsBefore, locked));
                    if (!_session.SaveSettings())
                    {
                        Dialogs.ShowMessage(string.Format(Texts.SettingsSaveFailed, _log.FilePath));
                    }

                    break;
                case ApplyOutcome.DataChanged:
                    ShowMessage(Texts.StatusDataChanged);
                    break;
                case ApplyOutcome.DocumentUnavailable:
                    ShowMessage(Texts.StatusDocumentChanged);
                    break;
                case ApplyOutcome.Cancelled:
                    ShowMessage(Texts.StatusCancelled);
                    break;
            }
        }

        private void CancelCore()
        {
            if (!_isWriting)
            {
                return;
            }

            if (_session.IsRunning)
            {
                _cancelRequested = true;
                return;
            }

            if (_session.CancelPending())
            {
                ShowMessage(Texts.StatusCancelled);
            }
        }

        // Progress of the write. Revit and the window share one thread: between batches the window can
        // draw the status and take a click on Cancel. This is safe only because the window is disabled
        // while busy (CanEdit is false), so the processed messages cannot start another operation.
        private sealed class WriteProgress : IWriteProgress
        {
            private readonly MainViewModel _owner;

            public WriteProgress(MainViewModel owner)
            {
                _owner = owner;
            }

            public bool Continue(int done, int total)
            {
                _owner.StatusText = string.Format(Texts.StatusWriteProgress, done, total);
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Input);
                if (!_owner._cancelRequested)
                {
                    return true;
                }

                _owner.StatusText = Texts.StatusStopping;
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
                return false;
            }
        }

        private void ShowMessage(string message)
        {
            _message = message;
            StatusText = message;
        }

        private void OnDataChanged()
        {
            SafeRunner.Run(_log, "DataChanged", RebuildRows, OnError);
        }

        private void OnStateChanged()
        {
            SafeRunner.Run(_log, "StateChanged", ShowState, OnError);
        }

        // While a call runs, its status stays. After it, the result of the last operation is shown.
        private void ShowState()
        {
            if (!_session.IsBusy)
            {
                _isWriting = false;
                StatusText = _session.DataMayBeStale ? Texts.StatusStale : _message;
                if (_windowHidden)
                {
                    _windowHidden = false;
                    Dialogs?.RestoreWindow();
                }
            }

            RefreshState();
        }

        private void OnSettingsChanged()
        {
            SafeRunner.Run(_log, "SettingsChanged", RebuildRows, OnError);
        }

        // Search -> visible ids -> recalculation -> rows. The search works on shown texts, so it lives here.
        // The session gets only the visible ids. This is the only place where the preview is calculated.
        private void RebuildRows()
        {
            CategoryData data = _session.Data;
            if (data == null)
            {
                _rows = NoRows;
                Settings.ShowProblems(null);
                RefreshState();
                return;
            }

            var levelNames = new Dictionary<long, string>();
            foreach (LevelInfo level in data.Levels)
            {
                levelNames[level.Id] = level.Name;
            }

            // Texts that do not depend on the calculation, for every element.
            var all = new List<TableRow>(data.Items.Count);
            foreach (NumberingItem item in data.Items)
            {
                all.Add(new TableRow
                {
                    Id = item.Id,
                    Name = item.Name,
                    Level = item.LevelId != null && levelNames.TryGetValue(item.LevelId.Value, out string levelName)
                        ? levelName
                        : Texts.NoLevel,
                    Current = item.Value,
                });
            }

            List<TableRow> visible = all;

            string searchText = SearchText ?? string.Empty;
            bool isSearching = IsSearching;
            if (isSearching)
            {
                SearchColumn? column = IsSearchByColumn ? SelectedSearchColumn?.Value : (SearchColumn?)null;
                visible = visible.Where(row => RowFilter.Matches(row, searchText, column)).ToList();
            }

            HashSet<long> visibleIds = null;
            if (isSearching)
            {
                visibleIds = new HashSet<long>(visible.Select(row => row.Id));
            }
            else if (_scopeCleared && _session.Picked == null)
            {
                visibleIds = new HashSet<long>();
            }

            _session.Recalculate(visibleIds);

            Preview preview = _session.Preview;
            var holderIds = new HashSet<long>(preview.Skipped.Select(skipped => skipped.OccupiedById));

            // The preview has one row per element, in the same order as the data.
            for (int i = 0; i < all.Count; i++)
            {
                all[i].New = preview.Rows[i].NewValue;
                all[i].Status = TextLookup.Status(preview.Rows[i], holderIds.Contains(preview.Rows[i].Item.Id));
            }

            _rows = visible;
            Settings.ShowProblems(preview);
            RefreshState();
        }

        // Every property is computed from the session, so refresh all bindings at once.
        private void RefreshState()
        {
            OnPropertyChanged(string.Empty);
        }

        private static SummaryData BuildSummary(WriteResult write, List<NumberingItem> itemsBefore,
            List<PreviewRow> locked)
        {
            var summary = new SummaryData
            {
                WrittenText = string.Format(Texts.SummaryWritten, write.WrittenCount),
                NotWrittenText = string.Format(Texts.SummaryNotWritten, write.Failures.Count + locked.Count),
            };
            foreach (PreviewRow row in locked)
            {
                summary.Failures.Add(string.Format(Texts.SummaryLine, row.Item.Id, row.Item.Name, TextLookup.Lock(row)));
            }

            foreach (WriteFailure failure in write.Failures)
            {
                string name = itemsBefore.Find(item => item.Id == failure.Id)?.Name ?? string.Empty;
                summary.Failures.Add(string.Format(Texts.SummaryLine, failure.Id, name, TextLookup.WriteFail(failure)));
            }

            return summary;
        }
    }
}
