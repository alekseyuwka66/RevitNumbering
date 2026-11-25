namespace RevitNumbering.Core.Documents
{
    public sealed class DocumentEventArgs : EventArgs
    {
        public DocumentEventArgs(DocumentKey document)
        {
            Document = document;
        }

        public DocumentKey Document { get; }
    }

    /// <summary>
    /// Tracks and checks the active document. Knows nothing about categories, strategies or numbering.
    /// Implemented by the adapter on Revit events; members are called on the window thread,
    /// events are raised through IUiDispatcher.Post.
    /// </summary>
    public interface IActiveDocumentService
    {
        /// <summary>Cached; updated in Revit event handlers.</summary>
        ActiveDocumentInfo Current { get; }

        /// <summary>Whether the document with this key is still the active one.</summary>
        bool IsActive(DocumentKey document);

        /// <summary>Generation of the model: grows on every significant change of the document.</summary>
        long GetGeneration(DocumentKey document);

        event EventHandler<DocumentEventArgs> ActiveChanged;

        event EventHandler<DocumentEventArgs> Closed;

        /// <summary>Significant model change: loaded data becomes stale.</summary>
        event EventHandler<DocumentEventArgs> ModelChanged;
    }
}
