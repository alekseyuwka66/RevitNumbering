namespace RevitNumbering.Core.Documents
{
    /// <summary>
    /// Opaque document key issued by the gateway. The core only compares keys for equality.
    /// </summary>
    public sealed class DocumentKey : IEquatable<DocumentKey> //ref, del many fields, target - check IsSame document
    {
        public DocumentKey(string value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public string Value { get; }

        public bool Equals(DocumentKey other)
        {
            return other is object && string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as DocumentKey);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(DocumentKey left, DocumentKey right)
        {
            return ReferenceEquals(left, right) || (left is object && left.Equals(right));
        }

        public static bool operator !=(DocumentKey left, DocumentKey right)
        {
            return !(left == right);
        }
    }
}
