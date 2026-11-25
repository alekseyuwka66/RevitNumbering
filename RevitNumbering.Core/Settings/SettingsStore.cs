using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Domain.Categories;
using RevitNumbering.Core.Domain.Ordering;

namespace RevitNumbering.Core.Settings
{
    public enum SettingsLoadStatus
    {
        Ok,
        Missing,
        Corrupted,
        NewerSchemaReadOnly
    }

    public sealed class SettingsLoadResult
    {
        public SettingsLoadResult(NumberingSettings settings, SettingsLoadStatus status)
        {
            Settings = settings;
            Status = status;
        }

        public NumberingSettings Settings { get; }

        public SettingsLoadStatus Status { get; }
    }

    /// <summary>
    /// JSON settings file of one Revit version. Not thread-safe: one instance per Revit session,
    /// called from the window thread. Several Revit processes are coordinated by the lock file.
    /// </summary>
    public sealed class SettingsStore
    {
        private const string BadSuffix = ".bad";
        private const string BackupSuffix = ".bak";
        private const string TempSuffix = ".tmp";
        private const string LockSuffix = ".lock";

        private readonly string _filePath;
        private readonly string _tempFilePath;
        private readonly string _lockFilePath;
        private readonly ILog _log;

        public SettingsStore(string filePath, ILog log)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            if (filePath.Trim().Length == 0 || !Path.IsPathRooted(filePath))
            {
                throw new ArgumentException("The settings path must be an absolute path.", nameof(filePath));
            }

            _filePath = filePath;
            _tempFilePath = filePath + TempSuffix;
            _lockFilePath = filePath + LockSuffix;
            _log = log;
            BadFilePath = filePath + BadSuffix;
            BackupFilePath = filePath + BackupSuffix;
        }

        /// <summary>Where a corrupted file is moved to.</summary>
        public string BadFilePath { get; }

        /// <summary>Where the previous file is kept after an import.</summary>
        public string BackupFilePath { get; }

        /// <summary>true: the file has a newer schema, writing is forbidden.</summary>
        public bool IsReadOnly { get; private set; }

        /// <summary>Never throws: neither the content nor input-output errors give exceptions.</summary>
        public SettingsLoadResult Load()
        {
            SettingsFileContent content;
            try
            {
                content = SettingsFileContent.Classify(
                    SettingsFile.WithRetries(_filePath, () => SettingsFile.ReadOrNull(_filePath)));
            }
            catch (IOException ex)
            {
                _log.Error("Не удалось прочитать файл настроек, загружены умолчания: " + _filePath, ex);
                IsReadOnly = false;
                return new SettingsLoadResult(SettingsDefaults.CreateGeneral(), SettingsLoadStatus.Missing);
            }

            if (content.Status == SettingsLoadStatus.Corrupted)
            {
                try
                {
                    content = SettingsFile.WithRetries(_filePath, MoveCorruptedUnderLock);
                }
                catch (IOException ex)
                {
                    _log.Error("Не удалось перенести повреждённый файл настроек в " + BadFilePath, ex);
                }
            }

            IsReadOnly = content.Status == SettingsLoadStatus.NewerSchemaReadOnly;
            switch (content.Status)
            {
                case SettingsLoadStatus.Ok:
                    return new SettingsLoadResult(Normalize(content.Raw), SettingsLoadStatus.Ok);
                case SettingsLoadStatus.NewerSchemaReadOnly:
                    _log.Info("Файл настроек создан более новой версией схемы, открыт только для чтения: "
                        + _filePath);
                    return new SettingsLoadResult(
                        content.Raw != null ? Normalize(content.Raw) : SettingsDefaults.CreateGeneral(),
                        SettingsLoadStatus.NewerSchemaReadOnly);
                default:
                    return new SettingsLoadResult(SettingsDefaults.CreateGeneral(), content.Status);
            }
        }

        /// <summary>Re-reads the file, replaces own category and changed general fields, writes via a temp file.</summary>
        public void Save(string categoryKey, CategorySettings category, GeneralSettingsChange general)
        {
            ValidateSaveArguments(categoryKey, category, general);

            if (IsReadOnly)
            {
                _log.Info("Настройки не сохранены: файл открыт только для чтения (более новая схема): " + _filePath);
                return;
            }

            try
            {
                SettingsFile.WithRetries(_filePath, () => SaveAttempt(categoryKey, category, general));
            }
            catch (IOException ex)
            {
                _log.Error("Не удалось сохранить настройки: " + _filePath, ex);
                throw;
            }
        }

        /// <summary>Copies the saved file as it is; without a file writes the general defaults.</summary>
        public void Export(string targetPath)
        {
            if (string.IsNullOrEmpty(targetPath))
            {
                throw new ArgumentException("The export path must not be empty.", nameof(targetPath));
            }

            if (string.Equals(Path.GetFullPath(targetPath), Path.GetFullPath(_filePath),
                StringComparison.OrdinalIgnoreCase))
            {
                _log.Info("Экспорт пропущен: выбран сам файл настроек: " + _filePath);
                return;
            }

            try
            {
                SettingsFile.WithRetries(targetPath, () =>
                {
                    byte[] content = SettingsFile.ReadOrNull(_filePath)
                        ?? SettingsJson.Serialize(SettingsDefaults.CreateGeneral());
                    SettingsFile.Write(targetPath, content);
                    return true;
                });
            }
            catch (IOException ex)
            {
                _log.Error("Не удалось экспортировать настройки в " + targetPath, ex);
                throw;
            }
        }

        /// <summary>Replaces all settings with the file; the previous file becomes the backup.</summary>
        public SettingsLoadResult Import(string sourcePath)
        {
            SettingsFileContent content = ReadImportSource(sourcePath);
            SettingsLoadStatus status = content.Status;
            string refusal = null;
            if (status == SettingsLoadStatus.Missing)
            {
                refusal = "файл не найден или недоступен";
            }
            else if (status == SettingsLoadStatus.Corrupted)
            {
                refusal = "файл повреждён";
            }
            else if (status == SettingsLoadStatus.NewerSchemaReadOnly)
            {
                refusal = "файл создан более новой версией схемы";
            }
            else if (content.Raw.Categories != null)
            {
                foreach (var pair in content.Raw.Categories)
                {
                    string problem = SettingsRules.FindCategoryProblem(pair.Value);
                    if (problem != null)
                    {
                        status = SettingsLoadStatus.Corrupted;
                        refusal = "недопустимая запись категории «" + pair.Key + "»: " + problem;
                        break;
                    }
                }
            }

            if (refusal != null)
            {
                _log.Info("Импорт настроек отклонён (" + refusal + "): " + sourcePath);
                return new SettingsLoadResult(null, status);
            }

            NumberingSettings imported = content.Raw;
            imported.SchemaVersion = SettingsLimits.SettingsSchema;
            byte[] bytes = SettingsJson.Serialize(imported);
            try
            {
                SettingsFile.WithRetries(_filePath, () => ImportAttempt(bytes));
            }
            catch (IOException ex)
            {
                _log.Error("Не удалось записать импортированные настройки: " + _filePath, ex);
                throw;
            }

            IsReadOnly = false;
            _log.Info("Настройки импортированы из " + sourcePath + "; прежний файл сохранён как " + BackupFilePath);
            return new SettingsLoadResult(Normalize(imported), SettingsLoadStatus.Ok);
        }

        /// <summary>
        /// Category defaults: the template from the category, strategy parameters from the strategy
        /// completed by the category overrides of the composition root (F6-05). No category key is known here.
        /// </summary>
        public static CategorySettings Defaults(INumberingCategory category, ITraversalStrategy strategy,
            CategoryStrategyDefaults strategyDefaults)
        {
            if (category == null)
            {
                throw new ArgumentNullException(nameof(category));
            }

            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            return new CategorySettings
            {
                Template = category.DefaultTemplate,
                LevelCodeSource = SettingsDefaults.DefaultLevelCodeSource,
                LevelParameterName = SettingsDefaults.DefaultLevelParameterName,
                LevelCodePart = SettingsDefaults.DefaultLevelCodePart,
                LevelWordNumber = SettingsDefaults.DefaultLevelWordNumber,
                ConflictStrategy = SettingsDefaults.DefaultStrategy,
                AppendMode = SettingsDefaults.DefaultAppend,
                Traversal = new TraversalSettings
                {
                    StrategyKey = strategy.Key,
                    Parameters = (strategyDefaults ?? new CategoryStrategyDefaults()).Resolve(category, strategy)
                }
            };
        }

        private static void ValidateSaveArguments(string categoryKey, CategorySettings category,
            GeneralSettingsChange general)
        {
            if ((categoryKey == null) != (category == null))
            {
                throw new ArgumentException("The category key and the category settings must be given together.");
            }

            if (categoryKey != null)
            {
                if (categoryKey.Trim().Length == 0)
                {
                    throw new ArgumentException("The category key must not be empty.", nameof(categoryKey));
                }

                string problem = SettingsRules.FindCategoryProblem(category);
                if (problem != null)
                {
                    throw new ArgumentException("Invalid category settings: " + problem, nameof(category));
                }
            }

            if (general == null)
            {
                return;
            }

            if (general.LastCategoryKey != null && general.LastCategoryKey.Length == 0)
            {
                throw new ArgumentException("The last category key must not be empty.", nameof(general));
            }

            if (general.Traversal != null && string.IsNullOrEmpty(general.Traversal.StrategyKey))
            {
                throw new ArgumentException("The traversal strategy key must not be empty.", nameof(general));
            }

            if (general.Window != null && !SettingsRules.IsWindowValid(general.Window))
            {
                throw new ArgumentException("Invalid window placement.", nameof(general));
            }
        }

        /// <summary>One attempt of Save; IOException repeats the whole attempt.</summary>
        private bool SaveAttempt(string categoryKey, CategorySettings category, GeneralSettingsChange general)
        {
            EnsureDirectory();
            using (SettingsFile.AcquireLock(_lockFilePath))
            {
                try
                {
                    SettingsFileContent content = SettingsFileContent.Classify(SettingsFile.ReadOrNull(_filePath));
                    NumberingSettings basis;
                    switch (content.Status)
                    {
                        case SettingsLoadStatus.NewerSchemaReadOnly:
                            IsReadOnly = true;
                            _log.Info("Настройки не сохранены: файл создан более новой версией схемы: " + _filePath);
                            return false;
                        case SettingsLoadStatus.Corrupted:
                            MoveToBad(content.Error);
                            basis = SettingsDefaults.CreateGeneral();
                            break;
                        case SettingsLoadStatus.Ok:
                            basis = content.Raw;
                            break;
                        default:
                            basis = SettingsDefaults.CreateGeneral();
                            break;
                    }

                    Merge(basis, categoryKey, category, general);
                    SettingsFile.Write(_tempFilePath, SettingsJson.Serialize(basis));
                    if (File.Exists(_filePath))
                    {
                        File.Replace(_tempFilePath, _filePath, null);
                    }
                    else
                    {
                        File.Move(_tempFilePath, _filePath);
                    }

                    return true;
                }
                finally
                {
                    DeleteTempFile();
                }
            }
        }

        private static void Merge(NumberingSettings basis, string categoryKey, CategorySettings category,
            GeneralSettingsChange general)
        {
            basis.SchemaVersion = SettingsLimits.SettingsSchema;

            // Records of other categories are rewritten as they are, in the file order.
            var categories = new Dictionary<string, CategorySettings>(StringComparer.Ordinal);
            if (basis.Categories != null)
            {
                foreach (var pair in basis.Categories)
                {
                    categories[pair.Key] = pair.Value;
                }
            }

            if (categoryKey != null)
            {
                categories[categoryKey] = SettingsRules.CopyOwnCategory(category);
            }

            basis.Categories = categories;

            if (general == null)
            {
                return;
            }

            if (general.Traversal != null)
            {
                basis.Traversal = SettingsRules.Copy(general.Traversal);
            }

            if (general.LastCategoryKey != null)
            {
                basis.LastCategoryKey = general.LastCategoryKey;
            }

            if (general.Window != null)
            {
                basis.Window = SettingsRules.Copy(general.Window);
            }
        }

        /// <summary>One attempt of the import write.</summary>
        private bool ImportAttempt(byte[] bytes)
        {
            EnsureDirectory();
            using (SettingsFile.AcquireLock(_lockFilePath))
            {
                try
                {
                    SettingsFile.Write(_tempFilePath, bytes);
                    if (File.Exists(_filePath))
                    {
                        File.Replace(_tempFilePath, _filePath, BackupFilePath);
                    }
                    else
                    {
                        // The previous settings were the defaults.
                        SettingsFile.Write(BackupFilePath, SettingsJson.Serialize(SettingsDefaults.CreateGeneral()));
                        File.Move(_tempFilePath, _filePath);
                    }

                    return true;
                }
                finally
                {
                    DeleteTempFile();
                }
            }
        }

        private SettingsFileContent ReadImportSource(string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                return SettingsFileContent.Classify(null);
            }

            try
            {
                return SettingsFileContent.Classify(
                    SettingsFile.WithRetries(sourcePath, () => SettingsFile.ReadOrNull(sourcePath)));
            }
            catch (IOException)
            {
                return SettingsFileContent.Classify(null);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                // A path that is not a valid file name is the same as a missing file.
                return SettingsFileContent.Classify(null);
            }
        }

        /// <summary>
        /// Moves a corrupted file to .bad under the lock. The file is re-read first:
        /// another Revit may have saved a good one in the meantime, then nothing is moved.
        /// </summary>
        private SettingsFileContent MoveCorruptedUnderLock()
        {
            using (SettingsFile.AcquireLock(_lockFilePath))
            {
                SettingsFileContent content = SettingsFileContent.Classify(SettingsFile.ReadOrNull(_filePath));
                if (content.Status == SettingsLoadStatus.Corrupted)
                {
                    MoveToBad(content.Error);
                }

                return content;
            }
        }

        /// <summary>Copy with overwrite, then delete the original. Called under the lock.</summary>
        private void MoveToBad(Exception error)
        {
            File.Copy(_filePath, BadFilePath, true);
            File.Delete(_filePath);
            _log.Error("Файл настроек повреждён: " + _filePath + "; сохранён как " + BadFilePath, error);
        }

        private NumberingSettings Normalize(NumberingSettings raw)
        {
            return SettingsRules.Normalize(raw, (key, problem) =>
                _log.Info("Запись категории «" + key + "» в файле настроек пропущена (" + problem + "): "
                    + _filePath));
        }

        private void EnsureDirectory()
        {
            string directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private void DeleteTempFile()
        {
            try
            {
                if (File.Exists(_tempFilePath))
                {
                    File.Delete(_tempFilePath);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _log.Error("Не удалось удалить временный файл настроек: " + _tempFilePath, ex);
            }
        }
    }
}
