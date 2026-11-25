using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Contracts
{
    /// <summary>
    /// The active document and the events of Revit about documents.
    /// The events are raised inside handlers of Revit events, that is in the API context.
    /// </summary>
    public interface IDocuments
    {
        /// <summary>Never null; Kind is None when no document is open.</summary>
        DocumentInfo Active { get; }

        event Action ActiveChanged;

        event Action<DocumentId> Closed;

        event Action<DocumentId, ModelChanges> Changed;
    }

    /// <summary>What one change of the model touched.</summary>
    public sealed class ModelChanges
    {
        public List<long> Added { get; set; } = new List<long>();

        public List<long> Modified { get; set; } = new List<long>();

        public List<long> Deleted { get; set; } = new List<long>();

        public bool LevelsChanged { get; set; }
    }
}
