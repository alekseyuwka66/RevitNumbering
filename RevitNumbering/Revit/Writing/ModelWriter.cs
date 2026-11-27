using System.Diagnostics;
using Autodesk.Revit.DB;
using RevitNumbering.Core.Contracts;
using RevitNumbering.Revit.Categories;
using RevitNumbering.Revit.Common;
using RevitNumbering.UI.Resources;

namespace RevitNumbering.Revit.Writing
{
    // Writes numbers as one undo step: a transaction group with one transaction per batch of elements.
    // A batch is one commit, so updaters and schedules of the model work once per batch, not once per element.
    // An element that Revit refuses does not stop the others. If a batch commit fails,
    // the batch is written element by element. Shows no Revit dialogs.
    internal static class ModelWriter
    {
        // Elements per transaction. The window redraws only between batches, so a batch must stay short.
        private const int BatchSize = 150;

        // The least time between two pauses that redraw the window and read Cancel.
        // Each pause costs Revit a redraw, which takes longer than a small batch.
        private const int ProgressIntervalMs = 800;

        public static WriteResult Write(Document document, RevitCategory category, List<WriteItem> items,
            IWriteProgress progress)
        {
            var result = new WriteResult();
            Stopwatch watch = Stopwatch.StartNew();

            // Checks before writing. Elements that fail a check are not touched.
            var checkedItems = new List<KeyValuePair<Parameter, WriteItem>>();
            bool workshared = document.IsWorkshared;
            foreach (WriteItem item in items)
            {
                WriteFailure failure = Check(document, category, item, workshared, out Parameter parameter);
                if (failure != null)
                {
                    result.Failures.Add(failure);
                }
                else
                {
                    checkedItems.Add(new KeyValuePair<Parameter, WriteItem>(parameter, item));
                }
            }

            result.ChecksMs = watch.ElapsedMilliseconds;
            long lastProgressAt = watch.ElapsedMilliseconds;
            if (checkedItems.Count > 0)
            {
                using (var group = new TransactionGroup(document, Texts.TransactionName))
                {
                    group.Start();
                    for (int start = 0; start < checkedItems.Count; start += BatchSize)
                    {
                        List<KeyValuePair<Parameter, WriteItem>> batch =
                            checkedItems.GetRange(start, Math.Min(BatchSize, checkedItems.Count - start));

                        long batchStartedAt = watch.ElapsedMilliseconds;
                        bool elementByElement = WriteBatch(document, category, batch, result);
                        long batchMs = watch.ElapsedMilliseconds - batchStartedAt;
                        result.TransactionsMs += batchMs;
                        result.SlowestBatchMs = Math.Max(result.SlowestBatchMs, batchMs);
                        result.Batches++;
                        if (elementByElement)
                        {
                            result.ElementByElementBatches++;
                        }

                        int done = start + batch.Count;
                        // The window redraws and reads Cancel not after every batch, but at most once per ProgressIntervalMs.
                        bool goOn = true;
                        if (progress != null && done < checkedItems.Count
                            && watch.ElapsedMilliseconds - lastProgressAt >= ProgressIntervalMs)
                        {
                            long pausedAt = watch.ElapsedMilliseconds;
                            goOn = progress.Continue(done, checkedItems.Count);
                            lastProgressAt = watch.ElapsedMilliseconds;
                            result.ProgressMs += lastProgressAt - pausedAt;
                        }

                        if (!goOn)
                        {
                            long rollBackStartedAt = watch.ElapsedMilliseconds;
                            group.RollBack();
                            result.FinishMs = watch.ElapsedMilliseconds - rollBackStartedAt;
                            result.Cancelled = true;
                            result.WrittenCount = 0;
                            result.Failures.Clear();
                            return result;
                        }
                    }

                    long finishStartedAt = watch.ElapsedMilliseconds;
                    if (result.WrittenCount > 0)
                    {
                        group.Assimilate();
                    }
                    else
                    {
                        group.RollBack();
                    }

                    result.FinishMs = watch.ElapsedMilliseconds - finishStartedAt;
                }
            }

            return result;
        }

        // The fast path: the whole batch in one transaction. If the commit fails (an updater error,
        // a model failure), the batch is written element by element, so only the bad elements are lost.
        // Returns true when the batch was written element by element.
        private static bool WriteBatch(Document document, RevitCategory category,
            List<KeyValuePair<Parameter, WriteItem>> batch, WriteResult result)
        {
            var rejected = new List<WriteFailure>();
            if (TryWriteTransaction(document, batch, rejected))
            {
                result.WrittenCount += batch.Count - rejected.Count;
                result.Failures.AddRange(rejected);
                return false;
            }

            foreach (KeyValuePair<Parameter, WriteItem> pair in batch)
            {
                // Take the parameter again: after a rolled back transaction the old object may be invalid.
                Element element = document.GetElement(RevitIds.ToElementId(pair.Value.Id));
                Parameter parameter = element != null && element.IsValidObject
                    ? element.get_Parameter(category.NumberParameter)
                    : null;
                WriteFailure failure = parameter == null
                    ? Failure(pair.Value, WriteFailReason.Deleted, null)
                    : WriteOne(document, parameter, pair.Value);
                if (failure == null)
                {
                    result.WrittenCount++;
                }
                else
                {
                    result.Failures.Add(failure);
                }
            }

            return true;
        }

        // One transaction for the batch. rejected gets the elements that Revit refused.
        private static bool TryWriteTransaction(Document document, List<KeyValuePair<Parameter, WriteItem>> batch,
            List<WriteFailure> rejected)
        {
            using (var transaction = new Transaction(document, Texts.TransactionName))
            {
                var handler = new WriteFailureHandler();
                FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
                options.SetFailuresPreprocessor(handler);
                options.SetClearAfterRollback(true);
                transaction.SetFailureHandlingOptions(options);
                transaction.Start();

                foreach (KeyValuePair<Parameter, WriteItem> pair in batch)
                {
                    try
                    {
                        if (!pair.Key.Set(pair.Value.Value))
                        {
                            rejected.Add(Failure(pair.Value, WriteFailReason.Rejected, null));
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.ApplicationException ex)
                    {
                        rejected.Add(Failure(pair.Value, WriteFailReason.Rejected, ex.Message));
                    }
                }

                return transaction.Commit() == TransactionStatus.Committed;
            }
        }

        private static WriteFailure Check(Document document, RevitCategory category, WriteItem item, bool workshared,
            out Parameter parameter)
        {
            parameter = null;
            ElementId id = RevitIds.ToElementId(item.Id);
            Element element = document.GetElement(id);
            if (element == null || !element.IsValidObject)
            {
                return Failure(item, WriteFailReason.Deleted, null);
            }

            if (workshared)
            {
                if (WorksharingUtils.GetCheckoutStatus(document, id, out string owner) == CheckoutStatus.OwnedByOtherUser)
                {
                    return Failure(item, WriteFailReason.OwnedByOther, owner);
                }

                ModelUpdatesStatus updates = WorksharingUtils.GetModelUpdatesStatus(document, id);
                if (updates == ModelUpdatesStatus.UpdatedInCentral || updates == ModelUpdatesStatus.DeletedInCentral)
                {
                    return Failure(item, WriteFailReason.NotUpToDate, null);
                }
            }

            parameter = element.get_Parameter(category.NumberParameter);
            if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.String)
            {
                return Failure(item, WriteFailReason.ReadOnly, null);
            }

            return null;
        }

        // One element in its own transaction. Returns null when it is written.
        private static WriteFailure WriteOne(Document document, Parameter parameter, WriteItem item)
        {
            using (var transaction = new Transaction(document, Texts.TransactionName))
            {
                var handler = new WriteFailureHandler();
                FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
                options.SetFailuresPreprocessor(handler);
                options.SetClearAfterRollback(true);
                transaction.SetFailureHandlingOptions(options);
                transaction.Start();

                bool isSet;
                string rejection = null;
                try
                {
                    isSet = parameter.Set(item.Value);
                }
                catch (Autodesk.Revit.Exceptions.ApplicationException ex)
                {
                    isSet = false;
                    rejection = ex.Message;
                }

                if (!isSet)
                {
                    transaction.RollBack();
                    return Failure(item, WriteFailReason.Rejected, rejection);
                }

                return transaction.Commit() == TransactionStatus.Committed
                    ? null
                    : Failure(item, WriteFailReason.RolledBack, handler.FailureText);
            }
        }

        private static WriteFailure Failure(WriteItem item, WriteFailReason reason, string details)
        {
            return new WriteFailure { Id = item.Id, Reason = reason, Details = details ?? string.Empty };
        }
    }

    // Failure handling of one transaction. Every warning is deleted silently, so the user does not get
    // a series of Revit messages. An error rolls the transaction back, and its text goes to the summary.
    internal sealed class WriteFailureHandler : IFailuresPreprocessor
    {
        // Text of the first failure that rolled the transaction back.
        public string FailureText { get; private set; }

        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            bool rollBack = false;
            foreach (FailureMessageAccessor message in failuresAccessor.GetFailureMessages())
            {
                if (message.GetSeverity() == FailureSeverity.Warning)
                {
                    failuresAccessor.DeleteWarning(message);
                    continue;
                }

                rollBack = true;
                FailureText = FailureText ?? message.GetDescriptionText();
            }

            return rollBack ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
        }
    }
}
