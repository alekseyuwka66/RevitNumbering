using System.Diagnostics;
using Autodesk.Revit.DB;
using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Logging;
using RevitNumbering.Revit.Categories;
using RevitNumbering.Revit.Common;

namespace RevitNumbering.Revit.Writing
{
    /// <summary>
    /// Writes numbers as one step of the undo history: a transaction group with a transaction per element,
    /// so that an element Revit refuses does not stop the others. Shows no Revit dialogs.
    /// </summary>
    internal sealed class ModelWriter
    {
        /// <summary>The name of the step in the undo history of Revit.</summary>
        private const string TransactionName = "Нумерация элементов";

        private readonly ILog _log;

        public ModelWriter(ILog log)
        {
            _log = log;
        }

        public WriteResult Write(Document document, RevitCategory category, List<WriteItem> items)
        {
            var result = new WriteResult();
            Stopwatch watch = Stopwatch.StartNew();

            // Checks before writing: such elements are not touched at all.
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

            long checkMs = watch.ElapsedMilliseconds;
            if (checkedItems.Count > 0)
            {
                using (var group = new TransactionGroup(document, TransactionName))
                {
                    group.Start();
                    foreach (KeyValuePair<Parameter, WriteItem> pair in checkedItems)
                    {
                        WriteFailure failure = WriteOne(document, pair.Key, pair.Value);
                        if (failure == null)
                        {
                            result.WrittenCount++;
                        }
                        else
                        {
                            result.Failures.Add(failure);
                        }
                    }

                    if (result.WrittenCount > 0)
                    {
                        // One step in the undo history.
                        group.Assimilate();
                    }
                    else
                    {
                        group.RollBack();
                    }
                }
            }

            _log.Info("[Write] " + category.Key + ": " + items.Count + " requested, " + result.WrittenCount
                + " written, " + result.Failures.Count + " failed; checks " + checkMs + " ms, transactions "
                + (watch.ElapsedMilliseconds - checkMs) + " ms");
            return result;
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

        /// <summary>One element in its own transaction. null: written.</summary>
        private static WriteFailure WriteOne(Document document, Parameter parameter, WriteItem item)
        {
            using (var transaction = new Transaction(document, TransactionName))
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

    /// <summary>
    /// Failure handling of one transaction: the "duplicate value" warning is deleted silently; any other
    /// warning or error rolls the transaction back and its text is kept. Failures are never resolved.
    /// </summary>
    internal sealed class WriteFailureHandler : IFailuresPreprocessor
    {
        /// <summary>Text of the first failure that rolled the transaction back.</summary>
        public string FailureText { get; private set; }

        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            bool rollBack = false;
            foreach (FailureMessageAccessor message in failuresAccessor.GetFailureMessages())
            {
                if (message.GetSeverity() == FailureSeverity.Warning
                    && message.GetFailureDefinitionId()?.Guid == BuiltInFailures.GeneralFailures.DuplicateValue.Guid)
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
