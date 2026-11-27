namespace RevitNumbering.Core.Model
{
    // Opaque number of an open document, given by the Revit layer. Compared by value.
    public sealed class DocumentId : IEquatable<DocumentId>
    {
        public DocumentId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(DocumentId other)
        {
            return other != null && other.Value == Value;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DocumentId);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return "doc-" + Value;
        }
    }

    public enum HistoryChange
    {
        Undone,
        Redone,
    }

    public enum DocumentKind
    {
        None,
        Project,
        Family,
    }

    public sealed class DocumentInfo
    {
        // Null when Kind is None.
        public DocumentId Id { get; set; }

        public DocumentKind Kind { get; set; }
    }
}
