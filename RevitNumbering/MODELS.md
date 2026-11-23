# Документация по моделям RevitNumbering

Этот документ описывает раскладку файлов проекта RevitNumbering и его место в решении. Он предназначен для ИИ-агентов, работающих с этим репозиторием. Источник архитектуры — ТЗ (`01_architecture.md`); при расхождении действует ТЗ.

## Решение целиком

| Проект | Цель | Что лежит | Чего быть не должно |
|---|---|---|---|
| `RevitStartup` | net48 / net8.0-windows | Точка входа `IExternalApplication`, лента; вызывает `PluginEntry` | Логики плагина, подписок на события Revit |
| `RevitNumbering` (этот) | net48 / net8.0-windows, Revit API + WPF | Корень композиции, команды, XAML-представления, адаптер Revit | Решающей логики (номера, порядок, конфликты, статусы), моделей представления |
| `RevitNumbering.Core` | netstandard2.0, без Revit API и WPF | Ядро, DTO, интерфейс шлюза, настройки, журнал, тексты, модели представления | Ссылок на `Autodesk.Revit.*` и `System.Windows.*` |
| `RevitNumbering.Tests` | net48; net8.0 | Модульные тесты `RevitNumbering.Core` | Ссылок на Revit API |

Направление ссылок: `RevitStartup` → `RevitNumbering` → `RevitNumbering.Core`. Правило: **адаптер собирает факты, ядро принимает решения**.

## Структура проекта RevitNumbering

- **`PluginEntry.cs`** (корень проекта) — корень композиции: создаёт `ExternalEvent`, шлюз, реестры категорий и стратегий, хранилище настроек, журнал, единственное окно. Единственное долгоживущее состояние плагина.
- **Commands** — классы `IExternalCommand` (команды ленты). Только вызывают `PluginEntry`.
- **UI** — только представления WPF и их обвязка.
- **Revit** — адаптер платформы: все обращения к Revit API.
- **DevTools** — команды разработчика (генератор тестовой модели, замеры); компилируются только с символом `DEVTOOLS` (`/p:DevTools=true`) и не попадают в установщик.
- **Resources** — внутренние ресурсы, иконки.

---

## Правила размещения файлов

### UI
- `Windows/` — окна WPF (.xaml, .xaml.cs). Код окна — только инициализация и привязка `DataContext`; логики в `.xaml.cs` нет.
- `Controls/` — пользовательские элементы управления.
- `Converters/` — конвертеры привязок (например, статус строки → цвет, bool → Visibility).
- `Services/` — реализации интерфейсов ядра для WPF: `WpfUiDispatcher : IUiDispatcher`, `WpfDialogService : IDialogService`.
- Модели представления (ViewModel) здесь **не лежат** — они в `RevitNumbering.Core/Presentation` и не знают о WPF.
- Тексты интерфейса в XAML не пишутся: только привязки к свойствам моделей представления (тексты — каталог `RevitNumbering.Core/Texts`).

### Revit
Адаптер платформы. Читает модель в DTO ядра, выполняет запросы в контексте API, записывает результат.
- `Gateway/` — `RevitGateway : IRevitGateway`, обработчик `ExternalEvent`, карта «документ → ключ», подписка и отписка на события Revit (только здесь).
- `Reading/` — `ModelReader`: чтение категории порциями в `ElementData`.
- `Writing/` — `ModelWriter`, `FailurePreprocessor : IFailuresPreprocessor`.
- `Categories/` — описатели `IRevitCategory` (по одному классу на категорию), `CategoryRegistry`, правила исключения `IElementExclusion`.
- Различия версий Revit API — только здесь, через `#if REVIT2024_OR_GREATER` и т. п.
- Перевод единиц (футы → мм, м²) — только здесь.

### Resources
- Иконки (.png, .ico, .svg), изображения.
- Build Action: Embedded Resource. Файлы не копируются в выходную директорию.

### Commands
- Классы, реализующие `IExternalCommand` (команды, вызываемые из ленты). Команда не содержит логики: проверяет контекст и вызывает `PluginEntry.ShowWindow`.

---

## Структура проекта RevitNumbering.Core (создаётся по ТЗ)

- `Model/` — DTO (`ElementData`, `CategoryInfo`, `CategorySnapshot`, `DocumentKey` …).
- `Gateway/` — `IRevitGateway`, `RequestQueue` (логика очереди без Revit).
- `Templates/`, `Ordering/`, `Numbering/`, `Writing/` — решающая логика (шаблон, порядок обхода, номера и конфликты, решение о повторе и сводка).
- `Settings/` — схема JSON и `SettingsStore`.
- `Texts/` — каталог текстов интерфейса (русский).
- `Logging/` — `ILog`, `FileLog`, `SafeRunner`.
- `Presentation/` — модели представления (CommunityToolkit.Mvvm), `IUiDispatcher`, `IDialogService`, `IClock`.

---

## Ключевые паттерны

- **MVVM:** представления — в `RevitNumbering/UI`, модели представления — в `RevitNumbering.Core/Presentation`; основа — CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`), свой `RelayCommand` не пишется.
- **Доступ к Revit API:** только через `IRevitGateway` и очередь запросов на `ExternalEvent`; немодальное окно не вызывает API напрямую.
- **Внедрение зависимостей:** через конструктор, сборка объектов — в `PluginEntry`; без DI-контейнера и объектов-одиночек (сторонние библиотеки, кроме CommunityToolkit.Mvvm, не подключаются).
- **Решающая логика:** только в `RevitNumbering.Core`, покрыта тестами без Revit.
- **Новая категория:** один класс `IRevitCategory` в `Revit/Categories` + одна строка регистрации в `PluginEntry`.
- **Ошибки:** каждая точка входа (команда, обработчик окна, обработчик `ExternalEvent` и событий Revit) обёрнута в `SafeRunner`.
- **Данные плагина:** в выходную папку при сборке данные не копируются; настройки пользователя — `settings.json` в папке установки плагина своей версии Revit (`%APPDATA%\Autodesk\Revit\Addins\<версия>\RevitNumbering\`), его создаёт и пишет только `SettingsStore`, в MSI он не входит; журнал — `%LOCALAPPDATA%\RevitNumbering\Logs`.
