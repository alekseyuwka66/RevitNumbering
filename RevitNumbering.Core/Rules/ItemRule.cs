using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;

namespace RevitNumbering.Core.Rules
{
    /// <summary>
    /// A rule that locks an element of the scope: it keeps its value, takes no place in the queue
    /// and its number stays occupied. A new rule = a class + a line in NumberingCalculator.Rules.
    /// </summary>
    public abstract class ItemRule
    {
        /// <summary>None: the element takes part in numbering.</summary>
        public abstract LockReason Check(NumberingItem item, NumberingRequest request);
    }

    /// <summary>Reasons found by the Revit layer: owned by another user, read-only, room states.</summary>
    public sealed class RevitLockRule : ItemRule
    {
        public override LockReason Check(NumberingItem item, NumberingRequest request)
        {
            return item.Lock;
        }
    }

    /// <summary>An element without a level cannot get a number with the level placeholder.</summary>
    public sealed class NoLevelRule : ItemRule
    {
        public override LockReason Check(NumberingItem item, NumberingRequest request)
        {
            return request.Template.HasLevel && request.FindLevel(item.LevelId) == null
                ? LockReason.NoLevel
                : LockReason.None;
        }
    }

    /// <summary>The ordering strategy cannot place the element, for example it has no point.</summary>
    public sealed class OrderingRule : ItemRule
    {
        public override LockReason Check(NumberingItem item, NumberingRequest request)
        {
            return request.Settings.Ordering.CanOrder(item) ? LockReason.None : LockReason.NotOrderable;
        }
    }
}
