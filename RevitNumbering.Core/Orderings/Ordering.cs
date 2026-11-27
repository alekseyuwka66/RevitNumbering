using System.Text.Json.Serialization;
using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Orderings
{
    // How elements are ordered inside one level. The settings object is the strategy itself:
    // its properties are the parameters saved in the settings file.
    // A new strategy needs: a class, a JsonDerivedType line here, an editor DataTemplate in
    // CategorySettingsPanel.xaml, and an "Ordering_" text with its name in Texts.resx.
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(GridOrdering), "Grid")]
    public abstract class Ordering
    {
        // Whether the element has the data this strategy needs.
        public virtual bool CanOrder(NumberingItem item)
        {
            return true;
        }

        // Whether the parameters are inside their limits.
        public virtual bool IsValid()
        {
            return true;
        }

        // Gets only elements that CanOrder accepts. Returns the same elements in numbering order.
        public abstract List<NumberingItem> Order(List<NumberingItem> items);
    }
}
