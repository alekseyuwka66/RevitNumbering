namespace RevitNumbering.Core.Model
{
    /// <summary>Opaque number of an open document, issued by the Revit layer. Compared by value.</summary>
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

    public enum DocumentKind
    {
        None,
        Project,
        Family,
    }

    public sealed class DocumentInfo
    {
        /// <summary>null when Kind is None.</summary>
        public DocumentId Id { get; set; }

        public DocumentKind Kind { get; set; }

        public string Title { get; set; }
    }
}
