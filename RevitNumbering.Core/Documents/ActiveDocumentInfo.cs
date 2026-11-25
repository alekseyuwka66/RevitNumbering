namespace RevitNumbering.Core.Documents
{
    /// <summary>Whether the plugin can work with the active document (F1-10).</summary>
    public enum DocumentAvailability
    {
        /// <summary>A project: loading and writing are allowed.</summary>
        Ok,

        /// <summary>No open document: "Откройте проект".</summary>
        NoDocument,

        /// <summary>A family document: "Семейства не поддерживаются".</summary>
        FamilyDocument
    }

    /// <summary>
    /// Active document as seen by the window. Key is set only when Availability is Ok.
    /// </summary>
    public sealed class ActiveDocumentInfo
    {
        public static readonly ActiveDocumentInfo None =
            new ActiveDocumentInfo(DocumentAvailability.NoDocument, null, null);

        public ActiveDocumentInfo(DocumentAvailability availability, DocumentKey key, string title)
        {
            if (availability == DocumentAvailability.Ok && key == null)
            {
                throw new ArgumentNullException(nameof(key), "A usable document must have a key.");
            }

            Availability = availability;
            Key = availability == DocumentAvailability.Ok ? key : null;
            Title = title;
        }

        public DocumentAvailability Availability { get; }

        public bool IsAvailable => Availability == DocumentAvailability.Ok;

        /// <summary>null unless Availability is Ok.</summary>
        public DocumentKey Key { get; }

        public string Title { get; }
    }
}
