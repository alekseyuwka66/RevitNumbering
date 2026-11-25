using System.Text.Json.Serialization;
using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Orderings;
using RevitNumbering.Core.Settings;
using RevitNumbering.Core.Templates;
using RevitNumbering.UI.Resources;

namespace RevitNumbering.UI.Tests
{
    /// <summary>
    /// Texts of enum values and keys are found by composed keys ("LockReason_OwnedByOther"):
    /// the compiler does not notice a missing or misspelled string, these tests do.
    /// </summary>
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
        public void EveryProblemCode_HasText()
        {
            AssertEveryValueHasText<ProblemCode>("Problem_");
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
        public void EveryOtherEnumShownInTheWindow_HasText()
        {
            AssertEveryValueHasText<RowStatus>("RowStatus_");
            AssertEveryValueHasText<LevelCodePart>("LevelCodePart_");
            AssertEveryValueHasText<SettingsNoticeKind>("SettingsNotice_");
            AssertEveryValueHasText<ImportResult>("ImportResult_");
        }

        [Fact]
        public void TextsWithArguments_AreValidFormatStrings()
        {
            // A stray brace in a text would throw only when the text is shown.
            foreach (ProblemCode code in Enum.GetValues(typeof(ProblemCode)))
            {
                Assert.NotEmpty(TextLookup.Problem(new Problem(code, "first", "", "third")));
            }

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

            foreach (WriteFailReason reason in Enum.GetValues(typeof(WriteFailReason)))
            {
                Assert.NotEmpty(TextLookup.WriteFail(new WriteFailure { Reason = reason, Details = "details" }));
            }

            foreach (SettingsNoticeKind kind in Enum.GetValues(typeof(SettingsNoticeKind)))
            {
                Assert.NotEmpty(TextLookup.Notice(new SettingsNotice(kind, "Doors")));
            }
        }

        [Fact]
        public void NoLevelArgument_IsShownAsText()
        {
            string text = TextLookup.Problem(new Problem(ProblemCode.NeedLevel, "01", ""));

            Assert.Contains("01, " + Texts.NoLevel, text);
        }
    }
}
