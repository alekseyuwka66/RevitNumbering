using System.Diagnostics;
using Autodesk.Revit.DB;
using RevitNumbering.Core.Documents;
using RevitNumbering.Core.Domain.Categories;
using RevitNumbering.Core.Gateway;
using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Writing;
using RevitNumbering.Revit.Categories;
using RevitNumbering.Revit.Common;
using RevitNumbering.Revit.Documents;
using RevitNumbering.Revit.Gateway;

namespace RevitNumbering.Revit.Writing
{
    /// <summary>Built in the API context at the moment the write request runs (08 §2.5).</summary>
    public sealed class WriteContext
    {
        public WriteContext(Document activeDocument, DocumentKey activeDocumentKey, long activeDocumentGeneration,
            Func<IDisposable> beginOwnWrite)
        {
            ActiveDocument = activeDocument;
            ActiveDocumentKey = activeDocumentKey;
            ActiveDocumentGeneration = activeDocumentGeneration;
            BeginOwnWrite = beginOwnWrite ?? throw new ArgumentNullException(nameof(beginOwnWrite));
        }

        /// <summary>UIApplication.ActiveUIDocument?.Document; null: none.</summary>
        public Document ActiveDocument { get; }

        /// <summary>null: no active document.</summary>
        public DocumentKey ActiveDocumentKey { get; }

        public long ActiveDocumentGeneration { get; }

        /// <summary>Own-write flag of the document service: DocumentChanged inside is skipped.</summary>
        public Func<IDisposable> BeginOwnWrite { get; }
    }

    internal static class WriteParameters
    {
        /// <summary>PRM-FAILURE-WHITELIST.</summary>
        internal static readonly FailureDefinitionId[] FailureWhitelist =
        {
            BuiltInFailures.GeneralFailures.DuplicateValue
        };
    }

    /// <summary>
    /// Writes a plan in one call of the API (08 §3.1). Performs API calls and reports facts;
    /// every decision is made by WriteSession in the core.
    /// </summary>
    public sealed class ModelWriter
    {
        private readonly CategoryRegistry<IRevitCategory> _categories;
        private readonly ILog _log;

        public ModelWriter(CategoryRegistry<IRevitCategory> categories, ILog log)
        {
            _categories = categories ?? throw new ArgumentNullException(nameof(categories));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public WriteReport Write(WriteContext context, WritePlan plan, CancellationToken cancellationToken)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            Stopwatch watch = Stopwatch.StartNew();
            Document document = context.ActiveDocument;
            DocumentKey activeKey = document == null || document.IsFamilyDocument ? null : context.ActiveDocumentKey;
            WriteOutcome? rejected = WriteSession.Admit(plan, activeKey, context.ActiveDocumentGeneration,
                cancellationToken.IsCancellationRequested);
            if (rejected.HasValue)
            {
                return WriteSession.Rejected(rejected.Value);
            }

            IRevitCategory category = _categories.Find(plan.CategoryKey)
                ?? throw new ArgumentException("Unknown category: " + plan.CategoryKey, nameof(plan));

            var session = new WriteSession(plan);
            List<ElementWriteFacts> facts = plan.Items.Select(item => ReadFacts(document, category, item)).ToList();

            WriteReport report;
            using (context.BeginOwnWrite())
            {
                session.Precheck(facts);
                report = RunAttempts(document, category, plan, session);
            }

            _log.Info("Запись " + plan.CategoryKey + ": " + report.Outcome + ", записано " + report.Written.Count
                + ", отказов " + report.Failures.Count + ", попыток " + report.Attempts + ", "
                + watch.ElapsedMilliseconds + " мс");
            return report;
        }

        private WriteReport RunAttempts(Document document, IRevitCategory category, WritePlan plan,
            WriteSession session)
        {
            while (true)
            {
                IReadOnlyList<WriteItem> attempt = session.CurrentAttempt;
                if (attempt.Count == 0)
                {
                    return session.Fail();
                }

                string name = WriteTexts.TransactionName(plan.CategoryDisplayName, attempt.Count);
                var preprocessor = new FailurePreprocessor(attempt.Select(item => item.Id));
                var setFailures = new List<WriteFailure>();

                using (var transaction = new Transaction(document, name))
                {
                    if (transaction.Start() != TransactionStatus.Started)
                    {
                        throw new InvalidOperationException("The write transaction did not start.");
                    }

                    FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(preprocessor);
                    options.SetClearAfterRollback(true);
                    transaction.SetFailureHandlingOptions(options);

                    foreach (WriteItem item in attempt)
                    {
                        if (!TrySet(document, category, item))
                        {
                            setFailures.Add(new WriteFailure(item.Id, WriteFailReason.NotUpToDate, string.Empty));
                        }
                    }

                    if (setFailures.Count > 0)
                    {
                        // AA-84: a value that could not be set rolls the attempt back.
                        transaction.RollBack();
                        if (!session.RolledBack(setFailures, Array.Empty<string>()))
                        {
                            return session.Fail();
                        }

                        continue;
                    }

                    if (transaction.Commit() == TransactionStatus.Committed)
                    {
                        return session.Commit(ReadWritten(document, category, attempt));
                    }
                }

                var failures = attempt
                    .Where(item => preprocessor.FailedElements.ContainsKey(item.Id))
                    .Select(item => new WriteFailure(item.Id, WriteFailReason.RolledBackByWarning,
                        preprocessor.FailedElements[item.Id]))
                    .ToList();
                if (!session.RolledBack(failures, preprocessor.UnboundTexts))
                {
                    return session.Fail();
                }
            }
        }

        private static bool TrySet(Document document, IRevitCategory category, WriteItem item)
        {
            try
            {
                Parameter parameter = document.GetElement(RevitIds.ToElementId(item.Id))?.get_Parameter(category.NumberParameter);
                return parameter != null && parameter.Set(item.NewValue);
            }
            catch (Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                return false;
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                return false;
            }
        }

        private static IReadOnlyList<WrittenValue> ReadWritten(Document document, IRevitCategory category,
            IReadOnlyList<WriteItem> attempt)
        {
            return attempt
                .Select(item => new WrittenValue(item.Id,
                    document.GetElement(RevitIds.ToElementId(item.Id))?.get_Parameter(category.NumberParameter)?.AsString()
                    ?? string.Empty))
                .ToList();
        }

        /// <summary>08 §3.2: facts of one plan element, in plan order.</summary>
        private ElementWriteFacts ReadFacts(Document document, IRevitCategory category, WriteItem item)
        {
            ElementId id = RevitIds.ToElementId(item.Id);
            Element element = document.GetElement(id);
            if (element == null || !element.IsValidObject)
            {
                return new ElementWriteFacts(item.Id, false, false, null, CentralState.Unknown, false, string.Empty);
            }

            bool owned = false;
            string ownerName = null;
            CentralState central = CentralState.NotWorkshared;
            if (document.IsWorkshared)
            {
                try
                {
                    owned = WorksharingUtils.GetCheckoutStatus(document, id, out string owner)
                        == CheckoutStatus.OwnedByOtherUser;
                    ownerName = owned ? owner : null;
                    central = ToCentralState(WorksharingUtils.GetModelUpdatesStatus(document, id));
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException ex)
                {
                    central = CentralState.Unknown;
                    _log.Error("Не удалось проверить состояние элемента " + item.Id, ex);
                }
            }

            Parameter parameter = element.get_Parameter(category.NumberParameter);
            bool writable = parameter != null && !parameter.IsReadOnly && parameter.StorageType == StorageType.String;
            return new ElementWriteFacts(item.Id, true, owned, ownerName, central, writable,
                parameter?.AsString() ?? string.Empty);
        }

        private static CentralState ToCentralState(ModelUpdatesStatus status)
        {
            switch (status)
            {
                case ModelUpdatesStatus.CurrentWithCentral:
                    return CentralState.CurrentWithCentral;
                case ModelUpdatesStatus.NotYetInCentral:
                    return CentralState.NotYetInCentral;
                case ModelUpdatesStatus.UpdatedInCentral:
                    return CentralState.UpdatedInCentral;
                case ModelUpdatesStatus.DeletedInCentral:
                    return CentralState.DeletedInCentral;
                default:
                    return CentralState.Unknown;
            }
        }
    }

    /// <summary>
    /// Failure handling of one transaction (08 §3.4): only the whitelisted warning is deleted; anything else
    /// rolls back and records the failed elements of the attempt. Never resolves failures.
    /// </summary>
    public sealed class FailurePreprocessor : IFailuresPreprocessor
    {
        private readonly HashSet<long> _attemptIds;

        public FailurePreprocessor(IEnumerable<long> attemptIds)
        {
            _attemptIds = new HashSet<long>(attemptIds ?? Enumerable.Empty<long>());
        }

        /// <summary>Attempt id → failure text (the first one).</summary>
        public Dictionary<long, string> FailedElements { get; } = new Dictionary<long, string>();

        /// <summary>Failures without elements of the attempt.</summary>
        public List<string> UnboundTexts { get; } = new List<string>();

        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            bool rollBack = false;
            foreach (FailureMessageAccessor message in failuresAccessor.GetFailureMessages())
            {
                if (message.GetSeverity() == FailureSeverity.Warning && IsWhitelisted(message.GetFailureDefinitionId()))
                {
                    failuresAccessor.DeleteWarning(message);
                    continue;
                }

                rollBack = true;
                string text = message.GetDescriptionText() ?? string.Empty;
                bool bound = false;
                foreach (ElementId id in message.GetFailingElementIds().Concat(message.GetAdditionalElementIds()))
                {
                    long value = RevitIds.ToLong(id);
                    if (_attemptIds.Contains(value))
                    {
                        bound = true;
                        if (!FailedElements.ContainsKey(value))
                        {
                            FailedElements.Add(value, text);
                        }
                    }
                }

                if (!bound)
                {
                    UnboundTexts.Add(text);
                }
            }

            return rollBack ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
        }

        private static bool IsWhitelisted(FailureDefinitionId id)
        {
            return id != null && WriteParameters.FailureWhitelist.Any(allowed => allowed.Guid == id.Guid);
        }
    }

    /// <summary>IModelWriter for the core: puts the write into the request queue and builds the context there.</summary>
    public sealed class RevitModelWriter : IModelWriter
    {
        private readonly RequestQueue _queue;
        private readonly ModelWriter _writer;
        private readonly RevitActiveDocumentService _documents;
        private readonly ApiContext _context;

        public RevitModelWriter(RequestQueue queue, ModelWriter writer, RevitActiveDocumentService documents,
            ApiContext context)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task<WriteReport> WriteAsync(WritePlan plan, CancellationToken cancellationToken)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            return _queue.Enqueue(RequestNames.Write, plan.Document, () => Execute(plan, cancellationToken),
                cancellationToken);
        }

        private WriteReport Execute(WritePlan plan, CancellationToken cancellationToken)
        {
            Document active = _context.RequireApplication().ActiveUIDocument?.Document;
            DocumentKey key = active == null || active.IsFamilyDocument ? null : _documents.KeyOf(active);
            var context = new WriteContext(active, key, _documents.GetGeneration(key), _documents.BeginOwnWrite);
            WriteReport report = _writer.Write(context, plan, cancellationToken);
            if (report.Outcome == WriteOutcome.Committed)
            {
                _documents.MarkWritten(plan.Document);
            }

            return report;
        }
    }
}
