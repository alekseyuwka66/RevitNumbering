using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Contracts
{
    // Reads, picks, shows and writes elements. Call it only in the Revit API context and only for the
    // active document. Otherwise it throws DocumentUnavailableException.
    public interface IRevitModel
    {
        // Category keys in the order of the window list.
        IReadOnlyList<string> CategoryKeys { get; }

        CategoryData Read(DocumentId document, string categoryKey);

        // Returns null when the user presses Esc.
        List<long> PickElements(DocumentId document, string categoryKey);

        void ShowElement(DocumentId document, long id);

        // Writes the values as one undo step, in batches. progress may be null. If it cancels,
        // everything written so far is rolled back and the result has Cancelled set.
        WriteResult Write(DocumentId document, string categoryKey, List<WriteItem> items, IWriteProgress progress);
    }

    // The document is closed or not active. This is the only exception the plugin expects.
    public sealed class DocumentUnavailableException : Exception
    {
        public DocumentUnavailableException()
            : base("The document is closed or not active.")
        {
        }
    }
}
