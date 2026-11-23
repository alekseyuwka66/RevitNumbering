using System.Globalization;
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;
using RevitNumbering.Core.Templates;
using RevitNumbering.Core.Writing;

namespace RevitNumbering.Core.Texts
{
    /// <summary>
    /// Catalog of the common UI texts and texts of enumeration values.
    /// Category names live in category descriptors; the traversal strategy name and its
    /// parameter names live in the strategy.
    /// </summary>
    public static class TextCatalog
    {
        // Ribbon and window

        public const string RibbonTab = "Нумерация";

        public const string RibbonPanel = "Нумерация элементов";

        public const string RibbonButton = "Нумерация элементов";

        public const string RibbonTooltip = "Открыть окно нумерации элементов активного документа";

        /// <summary>{0} is the document name.</summary>
        public const string WindowTitle = "Нумерация элементов — {0}";

        // Labels and options

        public const string LblCategory = "Категория";

        public const string LblSearch = "Поиск";

        public const string LblTemplate = "Шаблон номера";

        /// <summary>Contains braces as text, never passed to Format.</summary>
        public const string TemplateHint = "{Уровень} или {Level} — код уровня; {N} — порядковый номер; {N:000} — номер с разрядностью (число нулей). Остальной текст копируется как есть.";

        public const string LblLevelCode = "Код уровня";

        public const string LevelSrcName = "Имя уровня";

        public const string LevelSrcParam = "Параметр уровня";

        public const string LblLevelParam = "Имя параметра";

        public const string LevelPartWhole = "Целиком";

        public const string LevelPartWordn = "Слово №";

        public const string LevelPartLast = "Последнее слово";

        public const string LblScope = "Охват";

        public const string ScopeVisible = "Видимые в таблице";

        public const string ScopeSelected = "Выбранные в модели";

        public const string BtnTakeSelection = "Взять выделение";

        public const string SelectionCount = "В снимке: {0}";

        public const string ChkAppend = "Дополнить";

        public const string LblTraversal = "Порядок обхода";

        public const string LblTolerance = "Допуск ряда, мм";

        public const string LblStrategy = "Конфликты";

        public const string StrategySkip = "Пропускать занятые номера";

        public const string StrategyAllow = "Разрешить повторы";

        // Buttons

        public const string BtnApply = "Применить";

        public const string BtnRefresh = "Обновить";

        public const string BtnSaveConfig = "Сохранить конфигурацию";

        public const string BtnExport = "Экспорт…";

        public const string BtnImport = "Импорт…";

        public const string BtnWrite = "Записать";

        public const string BtnCancel = "Отмена";

        public const string BtnCancelRequest = "Отменить запрос";

        // Table columns

        public const string ColId = "ElementId";

        public const string ColName = "Имя";

        public const string ColLevel = "Уровень";

        public const string ColCurrent = "Текущее значение";

        public const string ColNew = "Новое значение";

        public const string ColStatus = "Статус";

        // Row statuses and fix reasons

        public const string StatusChange = "Изменится";

        public const string StatusUnchanged = "Без изменений";

        /// <summary>{0} is the fix reason text.</summary>
        public const string StatusFixed = "Зафиксирован: {0}";

        public const string StatusConflict = "Конфликт";

        public const string StatusOut = "Вне охвата";

        /// <summary>{0} is the user name.</summary>
        public const string FixOwned = "занят {0}";

        public const string FixReadonly = "параметр только для чтения";

        public const string FixNotPlaced = "Не размещено";

        public const string FixNotEnclosed = "Не окружено";

        public const string FixRedundant = "Избыточное";

        public const string FixNoLevel = "нет уровня";

        public const string NoLevelGroup = "Без уровня";

        // Counters and the list shown before the write

        public const string CountChange = "Изменится: {0}";

        public const string CountConflicts = "Конфликтов: {0}";

        /// <summary>{0} is the value, {1} is the element id.</summary>
        public const string SkippedLine = "{0} занят элементом ID {1} — пропущен";

        public const string ListConflictsTitle = "Перед записью проверьте конфликты и зафиксированные элементы";

        /// <summary>{0} is the value, {1} is the ids of the duplicate.</summary>
        public const string ListDuplicateLine = "{0} повторяет номер элемента ID {1}";

        /// <summary>{0} is the element id, {1} is the fix reason text.</summary>
        public const string ListFixedLine = "ID {0}: зафиксирован — {1}";

        // Template errors and warnings

        public const string ErrUnclosedBrace = "Незакрытая скобка";

        public const string ErrUnknownPlaceholder = "Неизвестная подстановка: {0}";

        /// <summary>Contains braces as text, never passed to Format.</summary>
        public const string ErrNoCounter = "Нет порядкового номера {N}";

        public const string ErrBadWidth = "Неверная разрядность: {0}";

        /// <summary>Contains braces as text, never passed to Format.</summary>
        public const string ErrNeedLevel = "В охвате несколько уровней — добавьте {Уровень}";

        /// <summary>{0} is the comma separated level names.</summary>
        public const string ErrLevelPart = "нет части: {0}";

        /// <summary>{0} is the counter width, {1} is the largest N.</summary>
        public const string WarnWidth = "Разрядности {0} не хватает: на уровне {1} номеров";

        /// <summary>{0} is the value, {1} is the level names.</summary>
        public const string ErrDupInScope = "Повтор номера {0} внутри охвата; уровни: {1}";

        // Messages

        public const string MsgNoProject = "Откройте проект";

        public const string MsgFamily = "Семейства не поддерживаются";

        public const string MsgDocChanged = "Документ сменился или закрыт";

        public const string MsgBusy = "Revit занят — ожидание";

        /// <summary>{0} loaded of {1}.</summary>
        public const string MsgLoading = "Загрузка: {0} из {1}";

        public const string MsgStaleSnapshot = "Модель продолжает меняться — данные могут быть неактуальны. Нажмите «Обновить»";

        public const string MsgWriting = "Записываю {0} элементов…";

        public const string MsgDataChanged = "Данные изменились — проверьте и нажмите „Применить“ ещё раз";

        public const string MsgEmptyCategory = "В категории «{0}» нет элементов";

        public const string MsgSelectionEmpty = "В выделении нет элементов категории «{0}»";

        public const string MsgSelectionGroups = "В выделении нет элементов категории «{0}»: выделены группы или сборки";

        public const string MsgNothingToWrite = "Нечего записывать: новые значения совпадают с текущими";

        /// <summary>{0} is the path of the .bad file.</summary>
        public const string MsgSettingsCorrupted = "Файл настроек повреждён — загружены настройки по умолчанию. Повреждённый файл сохранён как {0}";

        public const string MsgSettingsNewer = "Файл настроек создан более новой версией плагина — настройки открыты только для чтения";

        public const string MsgSettingsSaveFailed = "Не удалось сохранить настройки: файл занят. Повторите позже";

        /// <summary>{0} is the path of the .bak file.</summary>
        public const string MsgImportDone = "Настройки импортированы. Прежний файл сохранён как {0}";

        public const string MsgImportFailed = "Не удалось импортировать: файл повреждён или создан более новой версией";

        public const string AskSaveSettings = "Сохранить изменения настроек?";

        /// <summary>{0} is the log file path.</summary>
        public const string ErrUnexpected = "Непредвиденная ошибка. Подробности записаны в журнал: {0}";

        // Apply summary

        public const string SumTitle = "Нумерация: итог";

        public const string SumChanged = "Изменено: {0}";

        public const string SumFixed = "Зафиксированы до записи: {0}";

        public const string SumFailed = "Не записаны: {0}";

        public const string SumDuplicates = "Повторы номеров: {0}";

        public const string SumNotWritten = "Запись не выполнена: {0}";

        public const string FailOwned = "занят";

        public const string FailOutdated = "устарел";

        public const string FailRollback = "откат из-за предупреждения";

        public const string FailDeleted = "удалён";

        /// <summary>{0} is the category name, {1} is the number of written elements.</summary>
        public const string TxName = "Нумерация: {0} ({1})";

        // Module texts

        /// <summary>{0} is the export file path.</summary>
        public const string M0501 = "Не удалось экспортировать настройки: {0}";

        /// <summary>{0} is the family name, {1} is the type name.</summary>
        public const string M0601 = "{0} : {1}";

        public const string M0801 = "превышено число попыток записи: {0}";

        public const string M0802 = "Revit отменил запись без указания элементов";

        public const string M0901 = "Нумерация элементов";

        public const string M0902 = "Да";

        public const string M0903 = "Нет";

        public const string M0904 = "Файлы настроек (*.json)|*.json";

        public const string M1001 = "Закрыть";

        /// <summary>{0} is the element id, {1} is the failure reason.</summary>
        public const string M1002 = "ID {0}: не записан — {1}";

        public const string M1003 = "Отменить загрузку";

        /// <summary>{0} and {1} are the tolerance limits.</summary>
        public const string M1004 = "Допуск ряда — целое число от {0} до {1} мм";

        public const string ErrorDialogTitle = "Нумерация элементов — ошибка";

        /// <summary>
        /// string.Format with the invariant culture. Missing and null arguments become "";
        /// a template without numbered placeholders is returned as is.
        /// </summary>
        public static string Format(string template, params object[] args)
        {
            if (string.IsNullOrEmpty(template))
            {
                return string.Empty;
            }

            int placeholderCount = CountPlaceholders(template);
            if (placeholderCount == 0)
            {
                return template;
            }

            var values = new object[Math.Max(placeholderCount, args == null ? 0 : args.Length)];
            for (int i = 0; i < values.Length; i++)
            {
                object value = args != null && i < args.Length ? args[i] : null;
                values[i] = value ?? string.Empty;
            }

            try
            {
                return string.Format(CultureInfo.InvariantCulture, template, values);
            }
            catch (FormatException)
            {
                // Braces used as plain text next to a numbered placeholder: show the template unformatted.
                return template;
            }
        }

        public static string FixReasonText(FixReason reason, string ownerName)
        {
            switch (reason)
            {
                case FixReason.None:
                    return string.Empty;
                case FixReason.OwnedByOtherUser:
                    return string.IsNullOrEmpty(ownerName) ? Format(FixOwned).TrimEnd() : Format(FixOwned, ownerName);
                case FixReason.ReadOnlyParameter:
                    return FixReadonly;
                case FixReason.RoomNotPlaced:
                    return FixNotPlaced;
                case FixReason.RoomNotEnclosed:
                    return FixNotEnclosed;
                case FixReason.RoomRedundant:
                    return FixRedundant;
                case FixReason.NoLevel:
                    return FixNoLevel;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
            }
        }

        public static string RowStatusText(RowStatus status, FixReason reason, string ownerName)
        {
            switch (status)
            {
                case RowStatus.WillChange:
                    return StatusChange;
                case RowStatus.Unchanged:
                    return StatusUnchanged;
                case RowStatus.Fixed:
                    return Format(StatusFixed, FixReasonText(reason, ownerName));
                case RowStatus.Conflict:
                    return StatusConflict;
                case RowStatus.OutOfScope:
                    return StatusOut;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }

        public static string WriteFailReasonText(WriteFailReason reason)
        {
            switch (reason)
            {
                case WriteFailReason.OwnedByOtherUser:
                    return FailOwned;
                case WriteFailReason.NotUpToDate:
                    return FailOutdated;
                case WriteFailReason.RolledBackByWarning:
                    return FailRollback;
                case WriteFailReason.Deleted:
                    return FailDeleted;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), reason, null);
            }
        }

        /// <summary>
        /// The only mapping "template issue code → text → arguments".
        /// Call: TemplateIssueText(issue.Code, issue.Arguments).
        /// </summary>
        public static string TemplateIssueText(TemplateIssueCode code, IReadOnlyList<string> arguments)
        {
            switch (code)
            {
                case TemplateIssueCode.UnclosedBrace:
                    return ErrUnclosedBrace;
                case TemplateIssueCode.UnknownPlaceholder:
                    return Format(ErrUnknownPlaceholder, Argument(arguments, 0));
                case TemplateIssueCode.MissingCounter:
                    return ErrNoCounter;
                case TemplateIssueCode.InvalidWidth:
                    return Format(ErrBadWidth, Argument(arguments, 0));
                case TemplateIssueCode.MissingLevelForManyLevels:
                    return ErrNeedLevel;
                case TemplateIssueCode.LevelCodePartMissing:
                    return Format(ErrLevelPart, string.Join(", ", arguments ?? Array.Empty<string>()));
                case TemplateIssueCode.WidthTooSmall:
                    return Format(WarnWidth, Argument(arguments, 0), Argument(arguments, 1));
                default:
                    throw new ArgumentOutOfRangeException(nameof(code), code, null);
            }
        }

        private static string Argument(IReadOnlyList<string> arguments, int index)
        {
            return arguments != null && index < arguments.Count ? arguments[index] ?? string.Empty : string.Empty;
        }

        /// <summary>Highest numbered placeholder ({0}, {1}, …) plus one; 0 when there is none.</summary>
        private static int CountPlaceholders(string template)
        {
            int count = 0;
            for (int i = 0; i < template.Length; i++)
            {
                if (template[i] != '{')
                {
                    continue;
                }

                int j = i + 1;
                int index = 0;
                while (j < template.Length && template[j] >= '0' && template[j] <= '9' && j - i < 4)
                {
                    index = (index * 10) + (template[j] - '0');
                    j++;
                }

                if (j > i + 1 && j < template.Length && template[j] == '}')
                {
                    count = Math.Max(count, index + 1);
                    i = j;
                }
            }

            return count;
        }
    }
}
