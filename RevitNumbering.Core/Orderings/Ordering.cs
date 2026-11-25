using System.Text.Json.Serialization;
using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Orderings
{
    /// <summary>
    /// A strategy of ordering elements inside one level. The settings object is the implementation:
    /// its properties are the parameters stored in the settings file.
    /// A new strategy = a class + one JsonDerivedType line here.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(GridOrdering), "Grid")]
    public abstract class Ordering
    {
        /// <summary>Whether the element has the data the strategy needs.</summary>
        public virtual bool CanOrder(NumberingItem item)
        {
            return true;
        }

        /// <summary>Whether the parameters are within their limits.</summary>
        public virtual bool IsValid()
        {
            return true;
        }

        /// <summary>Gets only the elements accepted by CanOrder; returns the same elements in numbering order.</summary>
        public abstract List<NumberingItem> Order(List<NumberingItem> items);
    }
}
