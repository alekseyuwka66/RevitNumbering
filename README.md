# RevitNumbering — каркас плагина Revit

Каркас решения для плагина Revit «Нумерация элементов». Одна кодовая база собирается под **Revit 2022, 2024 и 2025**. Код плагина ещё не написан: в проектах лежат только точка входа, кнопка на ленте и пустое окно WPF на MVVM (CommunityToolkit.Mvvm).

## 1. Структура

Решение `RevitNumbering.sln`:

| Проект | Назначение |
|---|---|
| `RevitStartup\` | Точка входа в Revit. `Application\ExternalApplication.cs` — `IExternalApplication`; `Application\RibbonManager.cs` — вкладка, панель и кнопка на ленте; `Manifest\RevitNumbering.addin` — манифест. Ссылается на `RevitNumbering`, поэтому обе сборки собираются в одну папку |
| `RevitNumbering\` | Модуль плагина: корень композиции, команды Revit (`Commands\`), представления WPF (`UI\`), адаптер Revit API (`Revit\`). Решающая логика и модели представления будут в проекте `RevitNumbering.Core` (добавляется по ТЗ). Правила раскладки файлов описаны в `MODELS.md` |
| `InstallerWIX\` | WiX v3: один MSI для Revit 2022, 2024 и 2025 |

Целевые среды:

| Конфигурация | Revit | .NET | Пакет API |
|---|---|---|---|
| `R2022` | 2022 | net48 | `Revit_All_Main_Versions_API_x64` 2022.0.0 |
| `R2024` | 2024 | net48 | `Revit_All_Main_Versions_API_x64` 2024.0.0 |
| `R2025` | 2025 | net8.0-windows | `Revit_All_Main_Versions_API_x64` 2025.0.0 |
| `Debug` | 2022 | net48 | как `R2022` |
| `BuildNET48` | мета: собирает `R2022` и `R2024` | | |
| `BuildNET8` | мета: собирает `R2025` | | |

Символы условной компиляции: `REVIT2022`, `REVIT2024`, `REVIT2025`, `REVIT2024_OR_GREATER`, `REVIT2025_OR_GREATER`. Например, `ElementId.Value` (long) появился в 2024, до этого — `IntegerValue`.

Пакеты: сборки Revit API подключаются только для компиляции (`ExcludeAssets="runtime"`) и в выходную папку не копируются. Из сторонних библиотек используется только `CommunityToolkit.Mvvm` 8.4.0.

## 2. Сборка

Нужны Visual Studio 2022+ (MSBuild), .NET SDK 8+ и WiX Toolset v3.11+.

```powershell
.\build.ps1
```

Скрипт собирает `BuildNET48` и `BuildNET8`, затем `InstallerWIX`. Готовый MSI: `InstallerWIX\bin\Release\InstallerWIX.msi`.

Одна версия без установщика:

```powershell
msbuild RevitStartup\RevitStartup.csproj /restore /p:Configuration=R2024 /p:Platform=x64
```

Результат: `RevitStartup\bin\x64\<конфигурация>\<tfm>\` (`RevitStartup.dll`, `RevitNumbering.dll` и зависимости).

## 3. Установка и запуск

Через MSI: установщик кладёт файлы в `%APPDATA%\Autodesk\Revit\Addins\<версия>\RevitNumbering\`, а манифест — в `%APPDATA%\Autodesk\Revit\Addins\<версия>\`. Ставятся только те версии Revit, которые есть на машине.

Вручную:
1. Скопируйте содержимое `RevitStartup\bin\x64\R20xx\<tfm>\` в `%APPDATA%\Autodesk\Revit\Addins\20xx\RevitNumbering\`.
2. Скопируйте `RevitStartup\Manifest\RevitNumbering.addin` в `%APPDATA%\Autodesk\Revit\Addins\20xx\`.
3. Запустите Revit. Кнопка появится на вкладке `RevitNumbering`.

## 4. Что настроить под продукт

- `Directory.Build.props` — `Version`, `Copyright`, `Company`, `Product`. Попадают в метаданные сборок и MSI.
- `RevitStartup\Application\RibbonManager.cs` — имена вкладки и панели, текст кнопки, полное имя класса команды. Сейчас это временные значения.
- `RevitStartup\Manifest\RevitNumbering.addin` — `VendorId` и `VendorDescription`.
- `InstallerWIX\Product.wxs` — `Manufacturer`.

## 5. CI

`.github\workflows\release.yml`: при push в `master` проект собирается через `build.ps1`, затем создаются тег и релиз с MSI. Версия берётся из `Directory.Build.props`.
