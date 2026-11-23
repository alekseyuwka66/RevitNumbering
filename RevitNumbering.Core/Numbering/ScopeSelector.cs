using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Numbering
{
    /// <summary>
    /// Scope of the "selected in model" mode: the selection snapshot intersected with the category
    /// snapshot. Excluded elements are not in the snapshot.
    /// </summary>
    public static class ScopeSelector
    {
        /// <summary>Selected ids that are category elements, in snapshot order, without repeats.</summary>
        public static IReadOnlyList<long> FromSelection(SelectionSnapshot selection, CategorySnapshot category)
        {
            if (selection == null)
            {
                throw new ArgumentNullException(nameof(selection));
            }

            if (category == null)
            {
                throw new ArgumentNullException(nameof(category));
            }

            // A selection of another document gives an empty scope.
            if (!Equals(selection.Document, category.Document))
            {
                return Array.Empty<long>();
            }

            var selected = new HashSet<long>(selection.ElementIds);
            var result = new List<long>();
            foreach (ElementData element in category.Elements)
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
