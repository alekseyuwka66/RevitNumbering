using RevitNumbering.Core.Domain.Elements;

namespace RevitNumbering.Core.Domain.Numbering
{
    /// <summary>
    /// Scope of numbering: ids of a scope (view, selection, ...) intersected with the loaded elements.
    /// Excluded elements are not loaded, so they never get into the scope.
    /// </summary>
    public static class ScopeSelector
    {
        /// <summary>
        /// Scope ids that are loaded elements, in element order, without repeats.
        /// scopeIds == null: no limit, every element.
        /// </summary>
        public static IReadOnlyList<long> Intersect(IReadOnlyCollection<long> scopeIds,
            IReadOnlyList<NumberableElement> elements)
        {
            if (elements == null)
            {
                throw new ArgumentNullException(nameof(elements));
            }

            if (scopeIds == null)
            {
                return elements.Select(element => element.Id).Distinct().ToArray();
            }

            var selected = new HashSet<long>(scopeIds);
            var result = new List<long>();
            foreach (NumberableElement element in elements)
            {
                if (selected.Remove(element.Id))
                {
                    result.Add(element.Id);
                }
            }

            return result;
        }
    }
}
