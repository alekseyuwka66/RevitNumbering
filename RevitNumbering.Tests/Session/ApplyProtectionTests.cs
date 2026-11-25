using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Session;
using RevitNumbering.Core.Settings;

namespace RevitNumbering.Tests.Session
{
    /// <summary>Apply writes only what the window showed, only into the document of the window.</summary>
    public class ApplyProtectionTests : IDisposable
    {
        private readonly string _folder =
            Path.Combine(Path.GetTempPath(), "RevitNumberingTests", Guid.NewGuid().ToString("N"));

        private readonly FakeRevitModel _model = new FakeRevitModel();
        private readonly FakeDocuments _documents = new FakeDocuments();
        private readonly NumberingSession _session;

        public ApplyProtectionTests()
        {
            _model.Levels.Add(Make.Level(10, "01", 0));
            _model.Items.Add(Make.Item(1, 10, 0, 0));
            _model.Items.Add(Make.Item(2, 10, 1000, 0));
            _model.Items.Add(Make.Item(3, 10, 2000, 0, "Д-01-003"));

            var log = new FakeLog();
            _session = new NumberingSession(new FakeRunner(), _model, _documents,
                new SettingsStore(Path.Combine(_folder, "settings.json"), log), log);
        }

        public void Dispose()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }

        [Fact]
        public async Task SameData_WritesExactlyTheShownValues()
        {
            await _session.OpenAsync();
            Dictionary<long, string> shown = _session.Preview.Rows
                .Where(row => row.Status == RowStatus.WillChange)
                .ToDictionary(row => row.Item.Id, row => row.NewValue);

            ApplyResult result = await _session.ApplyAsync();

            Assert.Equal(ApplyOutcome.Written, result.Outcome);
            Assert.Equal(new long[] { 1, 2 }, _model.Written.Select(item => item.Id));
            Assert.Equal(shown, _model.Written.ToDictionary(item => item.Id, item => item.Value));
            Assert.Equal("Д-01-001", shown[1]);
            // The table shows the actual values of the model: nothing is left to change.
            Assert.Equal(0, _session.Preview.ChangeCount);
            Assert.Equal("Д-01-002", _session.Data.Items.Single(item => item.Id == 2).Value);
        }

        [Fact]
        public async Task ChangedData_WritesNothing_AndReturnsFreshData()
        {
            await _session.OpenAsync();
            // The model changes behind the window: an element appears and takes the first place.
            _model.Items.Add(Make.Item(4, 10, -1000, 0));

            ApplyResult result = await _session.ApplyAsync();

            Assert.Equal(ApplyOutcome.DataChanged, result.Outcome);
            Assert.Null(_model.Written);
            Assert.Equal(4, _session.Data.Items.Count);
            Assert.Equal("Д-01-001", _session.Preview.Rows.Single(row => row.Item.Id == 4).NewValue);
        }

        [Fact]
        public async Task UnavailableDocument_WritesNothing()
        {
            await _session.OpenAsync();
            _model.DocumentAvailable = false;

            ApplyResult result = await _session.ApplyAsync();

            Assert.Equal(ApplyOutcome.DocumentUnavailable, result.Outcome);
            Assert.Null(_model.Written);
        }

        [Fact]
        public async Task AnotherActiveDocument_WritesNothing()
        {
            await _session.OpenAsync();
            _documents.Active = new DocumentInfo { Id = new DocumentId(2), Kind = DocumentKind.Project, Title = "Other" };
            _documents.RaiseActiveChanged();

            ApplyResult result = await _session.ApplyAsync();

            Assert.False(_session.IsWindowDocumentActive);
            Assert.NotEqual(ApplyOutcome.Written, result.Outcome);
            Assert.Null(_model.Written);
        }
    }
}
