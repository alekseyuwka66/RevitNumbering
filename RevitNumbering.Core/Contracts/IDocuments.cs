using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Contracts
{
    // The active Revit document. ActiveChanged is raised inside Revit event handlers, in the API context.
    public interface IDocuments
    {
        // Never null. Kind is None when no document is open.
        DocumentInfo Active { get; }

        event Action ActiveChanged;

        // The numbering step of this plugin was undone or redone in the active document.
        // Raised inside a Revit event handler, in the API context.
        event Action<HistoryChange> NumberingHistoryChanged;
    }
}
