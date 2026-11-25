namespace RevitNumbering.Core.Domain.Numbering
{
    /// <summary>
    /// The single value comparison of the plugin: exact, case-sensitive, after trimming.
    /// </summary>
    public static class ValueComparer
    {
        public static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        public static bool AreEqual(string a, string b)
        {
            return string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);
        }
    }
}
