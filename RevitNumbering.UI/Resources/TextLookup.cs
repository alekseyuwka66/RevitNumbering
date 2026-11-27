using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;

namespace RevitNumbering.UI.Resources
{
    // Texts found by a composed key, like "Category_Doors" or "LockReason_OwnedByOther".
    // A missing text gives the key itself. Tests make sure that does not happen.
    public static class TextLookup
    {
        public static string Get(string key)
        {
            return Texts.ResourceManager.GetString(key, Texts.Culture) ?? key;
        }

        public static string Category(string categoryKey)
        {
            return Get("Category_" + categoryKey);
        }

        // An empty name means "no level".
        public static string Level(string levelName)
        {
            return string.IsNullOrEmpty(levelName) ? Texts.NoLevel : levelName;
        }

        // Every "Problem_*" text takes the same arguments: {0} the number (may be empty), {1} the levels.
        public static string Problem(Problem problem)
        {
            return string.Format(Get("Problem_" + problem.Kind), problem.Text, string.Join(", ", problem.Levels.Select(Level)));
        }

        public static string Status(PreviewRow row, bool holdsNumber = false)
        {
            if (row.Status == RowStatus.Locked)
            {
                return string.Format(Get(holdsNumber ? "RowStatus_LockedHolder" : "RowStatus_Locked"), Lock(row));
            }

            return Get(holdsNumber && row.Status == RowStatus.OutOfScope
                ? "RowStatus_OutOfScopeHolder"
                : "RowStatus_" + row.Status);
        }

        public static string Lock(PreviewRow row)
        {
            return string.Format(Get("LockReason_" + row.Lock), row.Item.LockedBy);
        }

        public static string WriteFail(WriteFailure failure)
        {
            return string.Format(Get("WriteFail_" + failure.Reason), failure.Details);
        }
    }
}
