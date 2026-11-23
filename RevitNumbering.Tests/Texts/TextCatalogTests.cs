using System.Reflection;
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Templates;
using RevitNumbering.Core.Texts;
using RevitNumbering.Core.Writing;

namespace RevitNumbering.Tests.Texts
{
    public class TextCatalogTests
    {
        private static readonly string[] Arguments = { "X", "Y" };

        [Fact]
        public void EveryEnumValueHasText()
        {
            foreach (FixReason reason in Enum.GetValues(typeof(FixReason)))
            {
                string text = TextCatalog.FixReasonText(reason, "X");
                Assert.Equal(reason == FixReason.None, text.Length == 0);
            }

            Assert.Equal(string.Empty, TextCatalog.FixReasonText(FixReason.None, null));

            foreach (WriteFailReason reason in Enum.GetValues(typeof(WriteFailReason)))
            {
                Assert.NotEqual(string.Empty, TextCatalog.WriteFailReasonText(reason));
            }

            foreach (TemplateIssueCode code in Enum.GetValues(typeof(TemplateIssueCode)))
            {
                Assert.NotEqual(string.Empty, TextCatalog.TemplateIssueText(code, Arguments));
            }

            foreach (RowStatus status in Enum.GetValues(typeof(RowStatus)))
            {
                foreach (FixReason reason in Enum.GetValues(typeof(FixReason)))
                {
                    Assert.NotEqual(string.Empty, TextCatalog.RowStatusText(status, reason, "X"));
                }
            }
        }

        [Fact]
        public void UndeclaredEnumValue_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TextCatalog.FixReasonText((FixReason)99, null));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => TextCatalog.RowStatusText((RowStatus)99, FixReason.None, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => TextCatalog.WriteFailReasonText((WriteFailReason)99));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => TextCatalog.TemplateIssueText((TemplateIssueCode)99, Arguments));
        }

        [Fact]
        public void EnumValueTexts()
        {
            Assert.Equal("занят Иванов", TextCatalog.FixReasonText(FixReason.OwnedByOtherUser, "Иванов"));
            Assert.Equal("занят", TextCatalog.FixReasonText(FixReason.OwnedByOtherUser, null));
            Assert.Equal("занят", TextCatalog.FixReasonText(FixReason.OwnedByOtherUser, string.Empty));
            Assert.Equal("Зафиксирован: нет уровня",
                TextCatalog.RowStatusText(RowStatus.Fixed, FixReason.NoLevel, null));
            Assert.Equal("Зафиксирован: занят Петров",
                TextCatalog.RowStatusText(RowStatus.Fixed, FixReason.OwnedByOtherUser, "Петров"));
            Assert.Equal("удалён", TextCatalog.WriteFailReasonText(WriteFailReason.Deleted));
        }

        [Theory]
        [InlineData(FixReason.ReadOnlyParameter, "параметр только для чтения")]
        [InlineData(FixReason.RoomNotPlaced, "Не размещено")]
        [InlineData(FixReason.RoomNotEnclosed, "Не окружено")]
        [InlineData(FixReason.RoomRedundant, "Избыточное")]
        [InlineData(FixReason.NoLevel, "нет уровня")]
        public void FixReasonTexts(FixReason reason, string expected)
        {
            Assert.Equal(expected, TextCatalog.FixReasonText(reason, "Иванов"));
        }

        [Theory]
        [InlineData(RowStatus.WillChange, "Изменится")]
        [InlineData(RowStatus.Unchanged, "Без изменений")]
        [InlineData(RowStatus.Conflict, "Конфликт")]
        [InlineData(RowStatus.OutOfScope, "Вне охвата")]
        public void RowStatusTexts(RowStatus status, string expected)
        {
            Assert.Equal(expected, TextCatalog.RowStatusText(status, FixReason.None, null));
        }

        [Theory]
        [InlineData(WriteFailReason.OwnedByOtherUser, "занят")]
        [InlineData(WriteFailReason.NotUpToDate, "устарел")]
        [InlineData(WriteFailReason.RolledBackByWarning, "откат из-за предупреждения")]
        [InlineData(WriteFailReason.Deleted, "удалён")]
        public void WriteFailReasonTexts(WriteFailReason reason, string expected)
        {
            Assert.Equal(expected, TextCatalog.WriteFailReasonText(reason));
        }

        [Theory]
        [InlineData(TemplateIssueCode.MissingCounter, new string[0], "Нет порядкового номера {N}")]
        [InlineData(TemplateIssueCode.MissingLevelForManyLevels, new[] { "Этаж 1", "Без уровня" },
            "В охвате несколько уровней — добавьте {Уровень}")]
        [InlineData(TemplateIssueCode.InvalidWidth, new[] { "{N:0a}" }, "Неверная разрядность: {N:0a}")]
        [InlineData(TemplateIssueCode.UnknownPlaceholder, new[] { "{Этаж}" }, "Неизвестная подстановка: {Этаж}")]
        [InlineData(TemplateIssueCode.WidthTooSmall, new[] { "2", "143" },
            "Разрядности 2 не хватает: на уровне 143 номеров")]
        [InlineData(TemplateIssueCode.InvalidWidth, new string[0], "Неверная разрядность: ")]
        [InlineData(TemplateIssueCode.LevelCodePartMissing, new[] { "Кровля", "Подвал" }, "нет части: Кровля, Подвал")]
        [InlineData(TemplateIssueCode.LevelCodePartMissing, null, "нет части: ")]
        [InlineData(TemplateIssueCode.UnclosedBrace, null, "Незакрытая скобка")]
        public void TemplateIssueTexts(TemplateIssueCode code, string[] arguments, string expected)
        {
            Assert.Equal(expected, TextCatalog.TemplateIssueText(code, arguments));
        }

        [Fact]
        public void FourSeparateMessages()
        {
            Assert.Equal("Документ сменился или закрыт", TextCatalog.MsgDocChanged);
            Assert.Equal("Revit занят — ожидание", TextCatalog.MsgBusy);
            Assert.Equal(
                "Файл настроек повреждён — загружены настройки по умолчанию. Повреждённый файл сохранён как {0}",
                TextCatalog.MsgSettingsCorrupted);
            Assert.Equal("Непредвиденная ошибка. Подробности записаны в журнал: {0}", TextCatalog.ErrUnexpected);

            string[] messages =
            {
                TextCatalog.MsgDocChanged, TextCatalog.MsgBusy, TextCatalog.MsgSettingsCorrupted,
                TextCatalog.ErrUnexpected
            };
            Assert.Equal(messages.Length, messages.Distinct(StringComparer.Ordinal).Count());
            foreach (string message in messages)
            {
                Assert.DoesNotContain("Exception", message, StringComparison.Ordinal);
                Assert.DoesNotContain(" at ", message, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Format_MissingAndNullArguments_BecomeEmpty()
        {
            Assert.Equal("Загрузка: 5 из ", TextCatalog.Format(TextCatalog.MsgLoading, 5));
            Assert.Equal("Загрузка:  из ", TextCatalog.Format(TextCatalog.MsgLoading));
            Assert.Equal("Загрузка:  из ", TextCatalog.Format(TextCatalog.MsgLoading, null));
            Assert.Equal("Загрузка:  из 7", TextCatalog.Format(TextCatalog.MsgLoading, null, 7));
            Assert.Equal(string.Empty, TextCatalog.Format(null, 1));
        }

        [Fact]
        public void Format_UsesInvariantCulture()
        {
            Assert.Equal("Изменено: 1.5", TextCatalog.Format(TextCatalog.SumChanged, 1.5));
        }

        [Theory]
        [InlineData(TextCatalog.ErrNoCounter)]
        [InlineData(TextCatalog.ErrNeedLevel)]
        [InlineData(TextCatalog.TemplateHint)]
        public void Format_TextWithBracesAsText_IsReturnedAsIs(string text)
        {
            Assert.Equal(text, TextCatalog.Format(text, "X"));
        }

        [Fact]
        public void Catalog_ConstantsAreNotEmpty_AndCategoryAndStrategyNamesAreNotInCatalog()
        {
            FieldInfo[] constants = typeof(TextCatalog)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .ToArray();

            // All common texts except category, strategy and developer-tool texts.
            Assert.Equal(110, constants.Length);
            foreach (FieldInfo field in constants)
            {
                Assert.False(string.IsNullOrWhiteSpace((string)field.GetRawConstantValue()), field.Name);
                Assert.False(field.Name.StartsWith("Cat", StringComparison.Ordinal), field.Name);
                Assert.False(field.Name.StartsWith("Trav", StringComparison.Ordinal), field.Name);
                Assert.False(field.Name.StartsWith("Corner", StringComparison.Ordinal), field.Name);
                Assert.False(field.Name.StartsWith("Dir", StringComparison.Ordinal), field.Name);
            }
        }

        [Theory]
        [InlineData(TextCatalog.BtnExport, "Экспорт…")]
        [InlineData(TextCatalog.LevelPartWordn, "Слово №")]
        [InlineData(TextCatalog.MsgDataChanged, "Данные изменились — проверьте и нажмите „Применить“ ещё раз")]
        [InlineData(TextCatalog.MsgStaleSnapshot,
            "Модель продолжает меняться — данные могут быть неактуальны. Нажмите «Обновить»")]
        [InlineData(TextCatalog.MsgWriting, "Записываю {0} элементов…")]
        [InlineData(TextCatalog.TxName, "Нумерация: {0} ({1})")]
        [InlineData(TextCatalog.M0904, "Файлы настроек (*.json)|*.json")]
        [InlineData(TextCatalog.ErrorDialogTitle, "Нумерация элементов — ошибка")]
        public void Catalog_TextsWithSpecialCharacters(string actual, string expected)
        {
            Assert.Equal(expected, actual);
        }
    }
}
