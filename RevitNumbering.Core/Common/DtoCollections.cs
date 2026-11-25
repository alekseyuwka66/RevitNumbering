using System.Collections.ObjectModel;

namespace RevitNumbering.Core.Common
{
    /// <summary>
    /// Defensive copies for immutable DTO: collections are copied into arrays, null becomes empty.
    /// </summary>
    internal static class DtoCollections
    {
        public static IReadOnlyList<T> CopyList<T>(IEnumerable<T> source)
        {
            return source == null ? Array.Empty<T>() : source.ToArray();
        }

        public static IReadOnlyCollection<T> CopyCollection<T>(IEnumerable<T> source)
        {
            return source == null ? Array.Empty<T>() : source.ToArray();
        }

        public static IReadOnlyDictionary<string, string> CopyDictionary(
            IEnumerable<KeyValuePair<string, string>> source)
        {
            var copy = new Dictionary<string, string>(StringComparer.Ordinal);
            if (source != null)
            {
                foreach (var pair in source)
                {
                    copy[pair.Key] = pair.Value;
                }
            }
                
            return new ReadOnlyDictionary<string, string>(copy);
        }
    }
}
