# Документация по моделям RevitStartup

Этот документ описывает раскладку файлов проекта RevitStartup. Он предназначен для ИИ-агентов, работающих с этим репозиторием. Архитектура решения — в `RevitNumbering/MODELS.md` и в ТЗ (`01_architecture.md`).

## Назначение

RevitStartup — хост плагина: точка входа Revit и лента. Логики плагина здесь нет: при запуске и завершении Revit он передаёт управление корню композиции `RevitNumbering.PluginEntry`.

## Структура проекта

- **Application** — точка входа и лента.
- **Manifest** — манифест `.addin`.
- **Resources** — внутренние ресурсы, иконки.

---

## Правила размещения файлов

### Application
- `ExternalApplication.cs` — `IExternalApplication`: в `OnStartup` вызывает `PluginEntry.Initialize` (там создаются `ExternalEvent` и подписки на события Revit), затем строит ленту; в `OnShutdown` вызывает `PluginEntry.Shutdown`.
- `RibbonManager.cs` — вкладка, панель, кнопки. Кнопка указывает на команду из сборки `RevitNumbering.dll`, лежащей рядом.
- Подписки на события Revit здесь **не делаются** — они в `RevitNumbering/Revit/Gateway`.

### Manifest
- `RevitNumbering.addin` — манифест; ставится установщиком в папку `Addins\<версия>` текущего пользователя.

### Resources
- Иконки (.png, .ico, .svg), изображения.
- Build Action: Embedded Resource. Файлы не копируются в выходную директорию.
