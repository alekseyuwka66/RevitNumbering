using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Settings;

namespace RevitNumbering.UI.Resources
{
    /// <summary>
    /// Texts found by a composed key: "Category_Doors", "LockReason_OwnedByOther" and the like.
    /// A missing string gives the key itself; tests do not let it come to that.
    /// </summary>
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

        /// <summary>"" stands for "no level".</summary>
        public static string Level(string levelName)
        {
            return string.IsNullOrEmpty(levelName) ? Texts.NoLevel : levelName;
        }

        public static string Problem(Problem problem)
        {
            string format = Get("Problem_" + problem.Code);
            switch (problem.Code)
            {
                case ProblemCode.NeedLevel:
                case ProblemCode.LevelPartMissing:
                    return string.Format(format, JoinLevels(problem.Args));
                case ProblemCode.DuplicateInScope:
                    return string.Format(format, problem.Args.FirstOrDefault(), JoinLevels(problem.Args.Skip(1)));
                default:
                    return string.Format(format, problem.Args.Cast<object>().ToArray());
            }
        }

        /// <summary>The text of the Status column.</summary>
        public static string Status(PreviewRow row)
        {
            return row.Status == RowStatus.Locked
                ? string.Format(Get("RowStatus_Locked"), string.Format(Get("LockReason_" + row.Lock), row.Item.LockedBy))
                : Get("RowStatus_" + row.Status);
        }

        public static string WriteFail(WriteFailure failure)
        {
            return string.Format(Get("WriteFail_" + failure.Reason), failure.Details);
        }

        public static string Notice(SettingsNotice notice)
        {
            IEnumerable<string> args = notice.Kind == SettingsNoticeKind.CategoriesReset
                ? notice.Args.Select(Category)
                : notice.Args;
            return string.Format(Get("SettingsNotice_" + notice.Kind), string.Join(", ", args));
        }

        private static string JoinLevels(IEnumerable<string> names)
        {
            return string.Join(", ", names.Select(Level));
        }
    }
}
