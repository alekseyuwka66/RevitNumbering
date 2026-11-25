using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Contracts
{
    /// <summary>
    /// Reading, picking, showing and writing elements. Every method is called only in the Revit API context:
    /// inside IRevitRunner.Run or inside a handler of a Revit event.
    /// </summary>
    public interface IRevitModel
    {
        /// <summary>Registered categories in the order of the window list.</summary>
        IReadOnlyList<string> CategoryKeys { get; }

        /// <summary>All elements of the category and the levels. The document must be the active one.</summary>
        CategoryData Read(DocumentId document, string categoryKey);

        /// <summary>The given elements that belong to the category and are not excluded.</summary>
        List<NumberingItem> ReadItems(DocumentId document, string categoryKey, List<long> ids);

        List<LevelInfo> ReadLevels(DocumentId document);

        /// <summary>Lets the user pick elements of the category. Only inside Run. null: the user pressed Esc.</summary>
        List<long> PickElements(DocumentId document, string categoryKey);

        /// <summary>Zooms to the element and selects it. Only inside Run.</summary>
        void ShowElement(DocumentId document, long id);

        /// <summary>Writes the values as one step of the undo history. Only inside Run.</summary>
        WriteResult Write(DocumentId document, string categoryKey, List<WriteItem> items);
    }

    /// <summary>The document is closed or is not the active one. The only expected exception of the plugin.</summary>
    public sealed class DocumentUnavailableException : Exception
    {
        public DocumentUnavailableException()
            : base("The document is closed or not active.")
        {
        }
    }
}
