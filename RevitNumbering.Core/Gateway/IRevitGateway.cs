using RevitNumbering.Core.Model;
using RevitNumbering.Core.Writing;

namespace RevitNumbering.Core.Gateway
{
    /// <summary>
    /// The only way to Revit API from the core and view models. Members are called on the window
    /// thread outside the API context; events are raised through IUiDispatcher.Post.
    /// Invariant: every task ends with exactly one of Completed / Cancelled / Faulted on all paths.
    /// </summary>
    public interface IRevitGateway
    {
        /// <summary>Cached; updated in Revit event handlers.</summary>
        ActiveDocumentInfo GetActiveDocument();

        IReadOnlyList<CategoryInfo> Categories { get; }

        event EventHandler<DocumentEventArgs> ActiveDocumentChanged;

        event EventHandler<DocumentEventArgs> DocumentClosed;

        /// <summary>Significant model change.</summary>
        event EventHandler<DocumentEventArgs> DataStale;

        event EventHandler ApplicationClosing;

        event EventHandler<RequestStateEventArgs> RequestStateChanged;

        Task<CategorySnapshot> LoadCategoryAsync(DocumentKey document, string categoryKey,
            IProgress<LoadProgress> progress, CancellationToken cancellationToken);

        Task<SelectionSnapshot> TakeSelectionAsync(DocumentKey document, CancellationToken cancellationToken);

        Task<WriteReport> WriteAsync(WritePlan plan, CancellationToken cancellationToken);

        /// <summary>Cancels a request that has not started yet. true: cancelled.</summary>
        bool CancelPending();
    }
}
