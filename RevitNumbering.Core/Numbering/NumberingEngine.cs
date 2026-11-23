using RevitNumbering.Core.Model;
using RevitNumbering.Core.Ordering;

namespace RevitNumbering.Core.Numbering
{
    /// <summary>
    /// Fix classification by raw facts; the order of reasons is the FixReason value order.
    /// </summary>
    public static class FixClassifier
    {
        public static FixReason Classify(ElementData element, CategoryInfo category, bool templateUsesLevel)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Scope selection for the "selected in model" mode.
    /// </summary>
    public static class ScopeSelector
    {
        public static IReadOnlyList<long> FromSelection(SelectionSnapshot selection, CategorySnapshot category)
        {
            throw new NotImplementedException();
        }
    }

    public static class NumberingEngine
    {
        public static NumberingPlan Compute(NumberingInput input, TraversalRegistry traversal)
        {
            throw new NotImplementedException();
        }

        /// <summary>Composition comparison.</summary>
        public static bool SameComposition(NumberingPlan shown, NumberingPlan fresh)
        {
            throw new NotImplementedException();
        }
    }
}
