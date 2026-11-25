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
    /// <summary>
    /// The model of the main window. Works only with the coordinator; every command goes through SafeRunner,
    /// because an exception that leaves a command would bring Revit down.
    /// </summary>
    public sealed class MainViewModel : ObservableObject
    {
        private const int MaxHintRanges = 5;
        private static readonly TimeSpan RevitBusyDelay = TimeSpan.FromSeconds(1);
        private static readonly IReadOnlyList<TableRow> NoRows = new List<TableRow>();

        private readonly NumberingSession _session;
        private readonly ILog _log;
        private readonly DispatcherTimer _waitTimer;
        private readonly List<SettingsNotice> _pendingNotices = new List<SettingsNotice>();

        private IReadOnlyList<TableRow> _rows = NoRows;
        private string _searchText = string.Empty;
        private string _statusText = string.Empty;
        private bool _isOpened;
        private bool _isBusy;
        private bool _isWaitingForRevit;
        private bool _cancelRequested;

        public MainViewModel(NumberingSession session, ILog log)
        {
            _session = session;
            _log = log;
            _waitTimer = new DispatcherTimer { Interval = RevitBusyDelay };
            _waitTimer.Tick += OnWaitTimerTick;

            Categories = session.CategoryKeys
                .Select(key => new Option<string>(key, TextLookup.Category(key)))
                .ToList();
            Settings = new CategorySettingsViewModel();
            Settings.Changed += OnSettingsChanged;

            _session.DataChanged += OnDataChanged;
            _session.DocumentStateChanged += OnDocumentStateChanged;
            _session.SettingsNotice += OnSettingsNotice;

            OpenCommand = Async("Open", OpenAsync);
            PickCommand = Async("Pick", PickAsync);
            ResetPickCommand = Sync("ResetPick", () => _session.ResetPick());
            RefreshCommand = Async("Refresh", RefreshAsync);
            ApplyCommand = Async("Apply", ApplyAsync);
            CancelCommand = Sync("Cancel", Cancel);
            SaveCommand = Sync("Save", Save);
            ExportCommand = Sync("Export", Export);
            ImportCommand = Sync("Import", Import);
            ShowElementCommand = new AsyncRelayCommand<TableRow>(row =>
                SafeRunner.RunAsync(_log, "Show", () => ShowElementAsync(row), OnError));

            // Settings are read before the window is shown: it needs its saved placement.
            SafeRunner.Run(_log, "Initialize", () =>
            {
                _session.Initialize();
                Settings.Attach(_session.Settings);
            }, null);
        }

        /// <summary>Dialogs of the window; set by the window itself.</summary>
        public Dialogs Dialogs { get; set; }

        public IAsyncRelayCommand OpenCommand { get; }

        public IAsyncRelayCommand PickCommand { get; }

        public IRelayCommand ResetPickCommand { get; }

        public IAsyncRelayCommand RefreshCommand { get; }

        public IAsyncRelayCommand ApplyCommand { get; }

        public IRelayCommand CancelCommand { get; }

        public IRelayCommand SaveCommand { get; }

        public IRelayCommand ExportCommand { get; }

        public IRelayCommand ImportCommand { get; }

        /// <summary>A mouse click on a row: zoom to the element in Revit and select it.</summary>
        public IAsyncRelayCommand<TableRow> ShowElementCommand { get; }

        public List<Option<string>> Categories { get; }

        public CategorySettingsViewModel Settings { get; }

        /// <summary>
        /// Rows left after the search; the collection is replaced as a whole.
        /// Empty while a family document or no document is active.
        /// </summary>
        public IReadOnlyList<TableRow> Rows =>
            _session.ActiveDocument.Kind == DocumentKind.Project ? _rows : NoRows;

        public Option<string> SelectedCategory
        {
            get => Categories.Find(option => option.Value == _session.CategoryKey);
            set
            {
                if (value != null && value.Value != _session.CategoryKey)
                {
                    _ = SafeRunner.RunAsync(_log, "SelectCategory", () => SelectCategoryAsync(value.Value), OnError);
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value ?? string.Empty;
                SafeRunner.Run(_log, "Search", RebuildRows, OnError);
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value ?? string.Empty);
        }

        /// <summary>Not empty: the embedded defaults are incomplete, the window must not work.</summary>
        public string BlockedText => _session.StartupProblems.Count == 0
            ? string.Empty
            : Texts.StartupBlocked + "\n" + string.Join("\n", _session.StartupProblems.Select(TextLookup.Problem));

        public bool IsBlocked => _session.StartupProblems.Count > 0;

        /// <summary>The category, the search and the settings can be edited.</summary>
        public bool CanEdit => !_isBusy && !IsBlocked;

        public bool CanPick => CanEdit && _session.Data != null && _session.IsWindowDocumentActive;

        public bool HasPick => CanEdit && _session.Picked != null;

        public bool CanApply => CanEdit && _session.IsWindowDocumentActive
            && _session.Preview != null && _session.Preview.CanApply;

        /// <summary>Revit has not started the call for a second: Cancel replaces Apply.</summary>
        public bool IsCancelVisible => _isWaitingForRevit;

        public bool IsApplyVisible => !_isWaitingForRevit;

        /// <summary>The active project is not the one in the table, or the table may be stale.</summary>
        public bool IsRefreshVisible => !IsBlocked
            && _session.ActiveDocument.Kind == DocumentKind.Project
            && (!_session.IsWindowDocumentActive || _session.DataMayBeStale);

        public bool CanRefresh => !_isBusy;

        /// <summary>The table shows another document than the active one: it is greyed out.</summary>
        public bool IsTableStale => _session.Data != null && !_session.IsWindowDocumentActive;

        /// <summary>Grey text in the middle of the empty table.</summary>
        public string PlaceholderText
        {
            get
            {
                if (IsBlocked)
                {
                    return string.Empty;
                }

                switch (_session.ActiveDocument.Kind)
                {
                    case DocumentKind.Family:
                        return Texts.PlaceholderFamily;
                    case DocumentKind.None:
                        return Texts.PlaceholderNoProject;
                }

                if (_session.Data == null)
                {
                    return _isBusy ? Texts.PlaceholderLoading : Texts.PlaceholderNoData;
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
                Preview preview = ShownPreview;
                if (preview == null)
                {
                    return string.Empty;
                }

                int inScope = preview.Rows.Count(row => row.Status != RowStatus.OutOfScope);
                return string.Format(_session.Picked != null ? Texts.ScopePicked : Texts.ScopeVisible, inScope);
            }
        }

        public string CountersText
        {
            get
            {
                Preview preview = ShownPreview;
                return preview == null
                    ? string.Empty
                    : string.Format(Texts.Counters, preview.ChangeCount, preview.LockedCount, preview.Skipped.Count);
            }
        }

        /// <summary>Explains before writing why some numbers were stepped over.</summary>
        public string SkippedHint
        {
            get
            {
                List<SkippedNumber> skipped = ShownPreview?.Skipped;
                return skipped == null || skipped.Count == 0
                    ? string.Empty
                    : string.Format(Texts.SkippedHint, FoldRanges(skipped));
            }
        }

        /// <summary>The preview behind the shown rows; null while the table is empty.</summary>
        private Preview ShownPreview =>
            _session.ActiveDocument.Kind == DocumentKind.Project ? _session.Preview : null;

        public WindowPlacement SavedPlacement => _session.WindowPlacement;

        /// <summary>Asks about unsaved settings. false: the user cancelled closing.</summary>
        public bool ConfirmClose()
        {
            bool close = true;
            SafeRunner.Run(_log, "Closing", () =>
            {
                if (!_session.HasUnsavedSettings)
                {
                    return;
                }

                bool? answer = Dialogs.AskSave();
                if (answer == null)
                {
                    close = false;
                }
                else if (answer == true)
                {
                    _session.SaveSettings();
                }
            }, OnError);
            return close;
        }

        /// <summary>The window is closed: stop listening and remember its placement.</summary>
        public void Close(WindowPlacement placement)
        {
            SafeRunner.Run(_log, "Close", () =>
            {
                _waitTimer.Stop();
                _session.DataChanged -= OnDataChanged;
                _session.DocumentStateChanged -= OnDocumentStateChanged;
                _session.SettingsNotice -= OnSettingsNotice;
                _session.Close(placement);
            }, null);
        }

        private IAsyncRelayCommand Async(string name, Func<Task> action)
        {
            return new AsyncRelayCommand(() => SafeRunner.RunAsync(_log, name, action, OnError));
        }

        private IRelayCommand Sync(string name, Action action)
        {
            return new RelayCommand(() => SafeRunner.Run(_log, name, action, OnError));
        }

        private void OnError(Exception exception)
        {
            Dialogs?.ShowError(_log.FilePath);
        }

        private async Task OpenAsync()
        {
            _isOpened = true;
            foreach (SettingsNotice notice in _pendingNotices)
            {
                Dialogs.ShowMessage(TextLookup.Notice(notice));
            }

            _pendingNotices.Clear();
            if (IsBlocked)
            {
                RefreshState();
                return;
            }

            await RunBusyAsync(Texts.StatusLoading, () => _session.OpenAsync());
        }

        private async Task SelectCategoryAsync(string categoryKey)
        {
            await RunBusyAsync(Texts.StatusLoading, () => _session.SelectCategoryAsync(categoryKey));
            Settings.Attach(_session.Settings);
            RebuildRows();
        }

        private Task RefreshAsync()
        {
            return RunBusyAsync(Texts.StatusLoading, () => _session.RefreshAsync());
        }

        private Task PickAsync()
        {
            return CanPick ? RunBusyAsync(Texts.StatusPicking, () => _session.PickAsync()) : Task.CompletedTask;
        }

        private Task ShowElementAsync(TableRow row)
        {
            // Works only with the mouse and only while the table shows the active document.
            return row == null || _isBusy || IsTableStale
                ? Task.CompletedTask
                : RunBusyAsync(string.Empty, () => _session.ShowElementAsync(row.Id));
        }

        private async Task ApplyAsync()
        {
            if (!CanApply)
            {
                return;
            }

            // Names for the summary: elements may be gone from the model after the call.
            List<NumberingItem> itemsBefore = _session.Data.Items;
            ApplyResult result = null;
            await RunBusyAsync(string.Format(Texts.StatusWriting, _session.Preview.ChangeCount),
                async () => result = await _session.ApplyAsync());

            switch (result.Outcome)
            {
                case ApplyOutcome.Written:
                    StatusText = string.Format(Texts.StatusWritten,
                        result.Write.WrittenCount, result.Write.Failures.Count);
                    Dialogs.ShowSummary(BuildSummary(result.Write, itemsBefore));
                    break;
                case ApplyOutcome.DataChanged:
                    StatusText = Texts.StatusDataChanged;
                    break;
                case ApplyOutcome.DocumentUnavailable:
                    StatusText = Texts.StatusDocumentChanged;
                    break;
                case ApplyOutcome.Cancelled:
                    StatusText = Texts.StatusCancelled;
                    break;
            }
        }

        private void Cancel()
        {
            _cancelRequested = true;
            _session.CancelPending();
        }

        private void Save()
        {
            if (_session.SaveSettings())
            {
                StatusText = Texts.StatusSaved;
            }
        }

        private void Export()
        {
            string path = Dialogs.PickExportPath();
            if (path != null && _session.ExportSettings(path))
            {
                StatusText = string.Format(Texts.StatusExported, path);
            }
        }

        private void Import()
        {
            string path = Dialogs.PickImportPath();
            if (path == null)
            {
                return;
            }

            ImportResult result = _session.ImportSettings(path);
            string text = TextLookup.Get("ImportResult_" + result);
            if (result == ImportResult.Imported)
            {
                Settings.Attach(_session.Settings);
                RebuildRows();
                StatusText = text;
            }
            else
            {
                Dialogs.ShowMessage(text);
            }
        }

        /// <summary>
        /// One call to Revit: buttons are disabled, the status is drawn before the call, and after a second
        /// of waiting for Revit the Cancel button appears.
        /// </summary>
        private async Task RunBusyAsync(string status, Func<Task> action)
        {
            _isBusy = true;
            _cancelRequested = false;
            StatusText = status;
            RefreshState();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
            _waitTimer.Start();
            try
            {
                await action();
            }
            finally
            {
                _waitTimer.Stop();
                _isBusy = false;
                _isWaitingForRevit = false;
                StatusText = _cancelRequested ? Texts.StatusCancelled : DocumentStatus();
                RefreshState();
            }
        }

        private void OnWaitTimerTick(object sender, EventArgs e)
        {
            SafeRunner.Run(_log, "WaitTimer", () =>
            {
                _waitTimer.Stop();
                if (_session.IsPending)
                {
                    _isWaitingForRevit = true;
                    StatusText = Texts.StatusRevitBusy;
                    RefreshState();
                }
            }, null);
        }

        private void OnDataChanged()
        {
            SafeRunner.Run(_log, "DataChanged", RebuildRows, OnError);
        }

        private void OnDocumentStateChanged()
        {
            SafeRunner.Run(_log, "DocumentStateChanged", () =>
            {
                if (!_isBusy)
                {
                    StatusText = DocumentStatus();
                }

                RefreshState();
            }, OnError);
        }

        private void OnSettingsChanged()
        {
            SafeRunner.Run(_log, "SettingsChanged", RebuildRows, OnError);
        }

        private void OnSettingsNotice(SettingsNotice notice)
        {
            if (_isOpened)
            {
                SafeRunner.Run(_log, "SettingsNotice", () => Dialogs.ShowMessage(TextLookup.Notice(notice)), null);
            }
            else
            {
                // The window is not shown yet.
                _pendingNotices.Add(notice);
            }
        }

        /// <summary>The standing status of the table, when no operation reports its own.</summary>
        private string DocumentStatus()
        {
            if (_session.DataMayBeStale)
            {
                return Texts.StatusStale;
            }

            return IsTableStale && _session.ActiveDocument.Kind == DocumentKind.Project
                ? Texts.StatusOtherDocument
                : string.Empty;
        }

        /// <summary>
        /// Search → visible ids → recalculation → rows. The search works over shown texts, so it lives here;
        /// the coordinator gets only the set of visible ids.
        /// </summary>
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

            bool isSearching = _searchText.Trim().Length > 0;
            List<TableRow> visible = isSearching ? all.Where(row => RowFilter.Matches(row, _searchText)).ToList() : all;
            _session.Recalculate(isSearching ? new HashSet<long>(visible.Select(row => row.Id)) : null);

            // The preview has a row per element in the same order as the data.
            Preview preview = _session.Preview;
            for (int i = 0; i < all.Count; i++)
            {
                all[i].New = preview.Rows[i].NewValue;
                all[i].Status = TextLookup.Status(preview.Rows[i]);
            }

            _rows = visible;
            Settings.ShowProblems(preview);
            RefreshState();
        }

        /// <summary>Every property is computed from the session: refresh all bindings at once.</summary>
        private void RefreshState()
        {
            OnPropertyChanged(string.Empty);
        }

        private static SummaryData BuildSummary(WriteResult write, List<NumberingItem> itemsBefore)
        {
            var summary = new SummaryData
            {
                WrittenText = string.Format(Texts.SummaryWritten, write.WrittenCount),
                NotWrittenText = string.Format(Texts.SummaryNotWritten, write.Failures.Count),
            };
            foreach (WriteFailure failure in write.Failures)
            {
                string name = itemsBefore.Find(item => item.Id == failure.Id)?.Name ?? string.Empty;
                summary.Failures.Add(string.Format(Texts.SummaryLine, failure.Id, name, TextLookup.WriteFail(failure)));
            }

            return summary;
        }

        /// <summary>"Д-1-001 … Д-1-010, Д-1-015": numbers that follow each other on one level become a range.</summary>
        private static string FoldRanges(List<SkippedNumber> skipped)
        {
            var ranges = new List<string>();
            int start = 0;
            for (int i = 1; i <= skipped.Count; i++)
            {
                bool continues = i < skipped.Count
                    && skipped[i].LevelId == skipped[i - 1].LevelId
                    && skipped[i].Number == skipped[i - 1].Number + 1;
                if (continues)
                {
                    continue;
                }

                ranges.Add(i - 1 == start
                    ? skipped[start].Value
                    : string.Format(Texts.SkippedRange, skipped[start].Value, skipped[i - 1].Value));
                start = i;
            }

            if (ranges.Count > MaxHintRanges)
            {
                int more = ranges.Count - MaxHintRanges;
                ranges = ranges.Take(MaxHintRanges).ToList();
                ranges.Add(string.Format(Texts.SkippedMore, more));
            }

            return string.Join(", ", ranges);
        }
    }
}
