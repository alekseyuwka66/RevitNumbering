namespace RevitNumbering.Core.Domain.Elements
{
    /// <summary>
    /// Why a scope element is fixed. An open set: the common reasons are declared here,
    /// a category declares its own reasons next to its element class (see RoomElement).
    /// The order of checks is set by NumberableElement.Classify, not by the reason itself.
    /// null everywhere means "not fixed".
    /// </summary>
    public sealed class FixReason : IEquatable<FixReason>
    {
        public static readonly FixReason OwnedByOtherUser = new FixReason("owned", "занят {0}");

        public static readonly FixReason ReadOnlyParameter = new FixReason("readonly", "параметр только для чтения");

        public static readonly FixReason NoLevel = new FixReason("nolevel", "нет уровня");

        public FixReason(string key, string text)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Fix reason key must not be empty.", nameof(key));
            }

            Key = key;
            Text = text ?? string.Empty;
        }

        /// <summary>Unique key; reasons are compared by it.</summary>
        public string Key { get; }

        /// <summary>Text for the table; {0} is the owner name where it applies.</summary>
        public string Text { get; }

        public bool Equals(FixReason other)
        {
            return other is object && string.Equals(Key, other.Key, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FixReason);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Key);
        }

        public override string ToString()
        {
            return Key;
        }

        public static bool operator ==(FixReason left, FixReason right)
        {
            return ReferenceEquals(left, right) || (left is object && left.Equals(right));
        }

        public static bool operator !=(FixReason left, FixReason right)
        {
            return !(left == right);
        }
    }
}
