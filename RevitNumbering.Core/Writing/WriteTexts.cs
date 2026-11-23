using RevitNumbering.Core.Texts;

namespace RevitNumbering.Core.Writing
{
    /// <summary>
    /// Texts produced by the write module; the literals live in TextCatalog.
    /// </summary>
    public static class WriteTexts
    {
        private const string UnboundSeparator = "; ";

        /// <summary>ItemCount is the number of elements of the attempt.</summary>
        public static string TransactionName(string categoryDisplayName, int itemCount)
        {
            return TextCatalog.Format(TextCatalog.TxName, categoryDisplayName, itemCount);
        }

        public static string AttemptsExhausted(int maxAttempts)
        {
            return TextCatalog.Format(TextCatalog.M0801, maxAttempts);
        }

        /// <summary>
        /// Detail of a failure without elements: distinct texts in the original order joined with "; ";
        /// nothing to show gives the generic message.
        /// </summary>
        public static string UnboundFailure(IReadOnlyList<string> failureTexts)
        {
            var distinct = new List<string>();
            if (failureTexts != null)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string text in failureTexts)
                {
                    if (!string.IsNullOrWhiteSpace(text) && seen.Add(text))
                    {
                        distinct.Add(text);
                    }
                }
            }

            return distinct.Count == 0 ? TextCatalog.M0802 : string.Join(UnboundSeparator, distinct);
        }
    }
}
