using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Session;
using RevitNumbering.Core.Settings;

namespace RevitNumbering.Tests.Session
{
    // Apply writes only what the window showed, and only into the active project that the window shows.
    public class ApplyProtectionTests : IDisposable
    {
        private readonly string _folder =
            Path.Combine(Path.GetTempPath(), "RevitNumberingTests", Guid.NewGuid().ToString("N"));

        private readonly FakeRunner _runner = new FakeRunner();
        private readonly FakeDocuments _documents = new FakeDocuments();
        private readonly FakeRevitModel _model;
        private readonly NumberingSession _session;

        public ApplyProtectionTests()
        {
            _model = new FakeRevitModel(_documents);
            _model.Levels.Add(Make.Level(10, "01", 0));
            _model.Items.Add(Make.Item(1, 10, 0, 0));
            _model.Items.Add(Make.Item(2, 10, 1000, 0));
            _model.Items.Add(Make.Item(3, 10, 2000, 0, "Д-01-003"));

            var log = new FileLog(Path.Combine(_folder, "Logs"));
            _session = new NumberingSession(_runner, _model, _documents,
                new SettingsStore(Path.Combine(_folder, "settings.json"), log), log);

            // Like the window: load, then calculate on every data change.
            _session.DataChanged += () => _session.Recalculate(null);
            _session.Refresh();
        }

        public void Dispose()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }

        // Set when Apply reports its result. Null until then.
        private ApplyResult _result;

        private void Apply()
        {
            _session.Apply(applied => _result = applied);
        }

        [Fact]
        public void SameData_WritesExactlyTheShownValues()
        {
            Dictionary<long, string> shown = _session.Preview.Rows
                .Where(row => row.Status == RowStatus.WillChange)
                .ToDictionary(row => row.Item.Id, row => row.NewValue);

            Apply();

            Assert.Equal(ApplyOutcome.Written, _result.Outcome);
            Assert.Equal(new long[] { 1, 2 }, _model.Written.Select(item => item.Id));
            Assert.Equal(shown, _model.Written.ToDictionary(item => item.Id, item => item.Value));
            Assert.Equal("Д-01-001", shown[1]);
            // The table shows the real values of the model: nothing is left to change.
            Assert.Equal(0, _session.Preview.ChangeCount);
            Assert.Equal("Д-01-002", _session.Data.Items.Single(item => item.Id == 2).Value);
        }

        // Counts the calls after batches. Stops after stopAfter calls (0: never).
        private sealed class CountingProgress : IWriteProgress
        {
            private readonly int _stopAfter;

            public CountingProgress(int stopAfter)
            {
                _stopAfter = stopAfter;
            }

            public int Calls { get; private set; }

            public bool Continue(int done, int total)
            {
                Calls++;
                return _stopAfter == 0 || Calls < _stopAfter;
            }
        }

        [Fact]
        public void Apply_PassesTheProgressToTheWriter_AndItIsCalledAfterEveryBatch()
        {
            var progress = new CountingProgress(0);
            _model.BatchSize = 1;

            _session.Apply(applied => _result = applied, progress);

            Assert.Same(progress, _model.LastProgress);
            Assert.Equal(2, progress.Calls);
            Assert.False(_result.Write.Cancelled);
            Assert.Equal(2, _result.Write.WrittenCount);
        }

        [Fact]
        public void CancelledWrite_RollsBackEverything_AndTheTableShowsTheModel()
        {
            _model.BatchSize = 1;

            _session.Apply(applied => _result = applied, new CountingProgress(1));

            Assert.Equal(ApplyOutcome.Cancelled, _result.Outcome);
            Assert.Null(_result.Write);
            Assert.Equal("", _session.Data.Items.Single(item => item.Id == 1).Value);
            Assert.Equal("", _session.Data.Items.Single(item => item.Id == 2).Value);
            Assert.Equal(2, _session.Preview.ChangeCount);
            Assert.False(_session.IsBusy);
        }

        [Fact]
        public void ChangedData_WritesNothing_AndReturnsFreshData()
        {
            // The model changes behind the window: a new element takes the first place.
            _model.Items.Add(Make.Item(4, 10, -1000, 0));

            Apply();

            Assert.Equal(ApplyOutcome.DataChanged, _result.Outcome);
            Assert.Null(_model.Written);
            Assert.Equal(4, _session.Data.Items.Count);
            Assert.Equal("Д-01-001", _session.Preview.Rows.Single(row => row.Item.Id == 4).NewValue);
        }

        [Fact]
        public void UnavailableDocument_WritesNothing()
        {
            _model.DocumentAvailable = false;

            Apply();

            Assert.Equal(ApplyOutcome.DocumentUnavailable, _result.Outcome);
            Assert.Null(_model.Written);
            Assert.False(_session.IsBusy);
        }

        [Fact]
        public void DocumentSwitchedWhileApplyWaits_WritesNothing_AndTheWindowFollowsTheActiveProject()
        {
            _runner.HoldCalls = true;
            Apply();
            Assert.True(_session.IsBusy);

            _documents.Activate(FakeDocuments.Project(2));
            _runner.RunPosted();

            Assert.Equal(ApplyOutcome.DocumentUnavailable, _result.Outcome);
            Assert.Null(_model.Written);
            Assert.False(_session.IsBusy);
            Assert.Equal(new DocumentId(2), _session.WindowDocument.Id);
        }

        [Fact]
        public void DocumentSwitchNoticedOnlyInsideTheCall_WritesNothing()
        {
            _runner.HoldCalls = true;
            Apply();

            // No event came: the Revit layer refuses the old document inside the call.
            _documents.SwitchUnnoticed(FakeDocuments.Project(2));
            _runner.RunPosted();

            Assert.Equal(ApplyOutcome.DocumentUnavailable, _result.Outcome);
            Assert.Null(_model.Written);
            Assert.Equal(new DocumentId(2), _session.WindowDocument.Id);
        }

        [Fact]
        public void PickedElements_AreNarrowedByTheVisibleRows()
        {
            _model.PickResult = new List<long> { 1, 2, 3 };
            _session.Pick();

            _session.Recalculate(new HashSet<long> { 1, 2 });

            Assert.Equal(RowStatus.OutOfScope, _session.Preview.Rows.Single(row => row.Item.Id == 3).Status);
            Assert.Equal(2, _session.Preview.Rows.Count(row => row.Status != RowStatus.OutOfScope));
        }

        [Fact]
        public void NumberingUndoneInRevit_ReloadsTheTable_AndTellsTheWindow()
        {
            HistoryChange? reported = null;
            _session.HistoryChanged += change => reported = change;
            _model.Items.Single(item => item.Id == 1).Value = "Д-01-001";

            _documents.RaiseHistory(HistoryChange.Undone);

            Assert.Equal(HistoryChange.Undone, reported);
            Assert.Equal("Д-01-001", _session.Data.Items.Single(item => item.Id == 1).Value);
            Assert.False(_session.IsBusy);
        }

        [Fact]
        public void NumberingUndoneWhileACallWaits_OnlyMarksTheTableAsStale()
        {
            _runner.HoldCalls = true;
            _session.Refresh();
            bool reported = false;
            _session.HistoryChanged += change => reported = true;

            _documents.RaiseHistory(HistoryChange.Undone);

            Assert.False(reported);
            Assert.True(_session.DataMayBeStale);
        }

        [Fact]
        public void UnreadableSettingsFile_IsNeverOverwritten()
        {
            Directory.CreateDirectory(_folder);
            string path = Path.Combine(_folder, "broken.json");
            File.WriteAllText(path, "{ oops");
            var log = new FileLog(Path.Combine(_folder, "Logs"));
            var session = new NumberingSession(_runner, _model, _documents, new SettingsStore(path, log), log);

            Assert.True(session.LoadedSettings.FileUnreadable);
            Assert.True(session.SaveSettings());
            session.Close();

            Assert.Equal("{ oops", File.ReadAllText(path));
        }
    }
}
