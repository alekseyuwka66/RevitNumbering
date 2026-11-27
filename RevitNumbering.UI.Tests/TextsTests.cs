using System.Globalization;
using System.Text.Json.Serialization;
using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Orderings;
using RevitNumbering.Core.Settings;
using RevitNumbering.UI.Resources;

namespace RevitNumbering.UI.Tests
{
    // Texts of enum values and keys are found by composed keys, like "LockReason_OwnedByOther".
    // The compiler does not see a missing or misspelled text, these tests do.
    public class TextsTests
    {
        private static void AssertHasText(string key)
        {
            Assert.False(string.IsNullOrEmpty(Texts.ResourceManager.GetString(key)), "No text for " + key);
        }

        private static void AssertEveryValueHasText<TEnum>(string prefix)
        {
            foreach (object value in Enum.GetValues(typeof(TEnum)))
            {
                AssertHasText(prefix + value);
            }
        }

        [Fact]
        public void EveryLockReason_HasText()
        {
            AssertEveryValueHasText<LockReason>("LockReason_");
        }

        [Fact]
        public void EveryProblemKind_HasText()
        {
            AssertEveryValueHasText<ProblemKind>("Problem_");
        }

        [Fact]
        public void EveryWriteFailReason_HasText()
        {
            AssertEveryValueHasText<WriteFailReason>("WriteFail_");
        }

        [Fact]
        public void EveryCorner_HasText()
        {
            AssertEveryValueHasText<Corner>("Corner_");
        }

        [Fact]
        public void EveryOrderingStrategy_HasText()
        {
            object[] attributes = typeof(Ordering).GetCustomAttributes(typeof(JsonDerivedTypeAttribute), false);

            Assert.NotEmpty(attributes);
            foreach (JsonDerivedTypeAttribute attribute in attributes)
            {
                AssertHasText("Ordering_" + attribute.TypeDiscriminator);
            }
        }

        [Fact]
        public void EveryCategoryOfDefaults_HasText()
        {
            List<CategorySettings> categories = SettingsStore.ReadDefaults().Categories;

            Assert.NotEmpty(categories);
            foreach (CategorySettings category in categories)
            {
                AssertHasText("Category_" + category.CategoryKey);
            }
        }

        [Fact]
        public void TransactionName_HasText()
        {
            AssertHasText("TransactionName");
        }

        [Fact]
        public void EveryOtherEnumShownInTheWindow_HasText()
        {
            AssertEveryValueHasText<RowStatus>("RowStatus_");
        }

        [Fact]
        public void TextsWithArguments_AreValidFormatStrings()
        {
            // A stray brace in a text would throw only when the text is shown.
            foreach (LockReason reason in Enum.GetValues(typeof(LockReason)))
            {
                var row = new PreviewRow
                {
                    Item = new NumberingItem { LockedBy = "user" },
                    Status = RowStatus.Locked,
                    Lock = reason,
                };
                Assert.NotEmpty(TextLookup.Status(row));
            }

            foreach (ProblemKind kind in Enum.GetValues(typeof(ProblemKind)))
            {
                Assert.NotEmpty(TextLookup.Problem(new Problem(kind, "Д-01-001", new List<string> { "01", "" })));
            }

            foreach (WriteFailReason reason in Enum.GetValues(typeof(WriteFailReason)))
            {
                Assert.NotEmpty(TextLookup.WriteFail(new WriteFailure { Reason = reason, Details = "details" }));
            }

            foreach (string format in new[]
            {
                Texts.WidthTooSmall, Texts.SettingsUnreadable, Texts.SettingsCategoriesReset,
                Texts.SettingsSaveFailed, Texts.SkippedHint,
            })
            {
                Assert.NotEmpty(string.Format(format, "first", 2, "third"));
            }
        }

        [Fact]
        public void RowsHoldingANumber_SortBeforeEveryOtherStatus()
        {
            var item = new NumberingItem { LockedBy = "user" };
            string outOfScope = TextLookup.Status(new PreviewRow { Item = item, Status = RowStatus.OutOfScope }, true);
            string locked = TextLookup.Status(
                new PreviewRow { Item = item, Status = RowStatus.Locked, Lock = LockReason.ReadOnly }, true);

            foreach (string name in new[] { "ru-RU", "en-US", string.Empty })
            {
                CompareInfo compare = CultureInfo.GetCultureInfo(name).CompareInfo;
                foreach (RowStatus status in Enum.GetValues(typeof(RowStatus)))
                {
                    string other = TextLookup.Status(
                        new PreviewRow { Item = item, Status = status, Lock = LockReason.ReadOnly });
                    Assert.True(compare.Compare(outOfScope, other) < 0, name + ": " + outOfScope + " / " + other);
                    Assert.True(compare.Compare(locked, other) < 0, name + ": " + locked + " / " + other);
                }
            }
        }

        [Fact]
        public void NoLevelArgument_IsShownAsText()
        {
            string text = TextLookup.Problem(new Problem(ProblemKind.NeedLevel, levels: new List<string> { "01", "" }));

            Assert.Contains("01, " + Texts.NoLevel, text);
        }
    }
}
