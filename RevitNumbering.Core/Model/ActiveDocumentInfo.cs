namespace RevitNumbering.Core.Model
{
    public enum DocumentKind
    {
        None,
        Project,
        Family
    }

    /// <summary>
    /// Active document as seen by the window. Key is null when Kind is None.
    /// </summary>
    public sealed class ActiveDocumentInfo
    {
        public ActiveDocumentInfo(DocumentKey key, DocumentKind kind, string title)
        {
            Key = key;
            Kind = kind;
            Title = title;
        }

        public DocumentKey Key { get; }

        public DocumentKind Kind { get; }

        public string Title { get; }
    }
}
