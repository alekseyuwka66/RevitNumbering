namespace RevitNumbering.Core.Settings
{
    /// <summary>
    /// A settings file as read from disk: its state and, when it could be parsed, its content.
    /// </summary>
    internal sealed class SettingsFileContent
    {
        private SettingsFileContent(SettingsLoadStatus status, NumberingSettings raw, Exception error)
        {
            Status = status;
            Raw = raw;
            Error = error;
        }

        public SettingsLoadStatus Status { get; }

        /// <summary>Parsed object without normalization; null when missing, corrupted or not parseable.</summary>
        public NumberingSettings Raw { get; }

        /// <summary>Why the file is corrupted; null for the other states.</summary>
        public Exception Error { get; }

        /// <summary>Classification of file bytes, the first matching row of the classification table.</summary>
        public static SettingsFileContent Classify(byte[] bytes)
        {
            if (bytes == null)
            {
                return new SettingsFileContent(SettingsLoadStatus.Missing, null, null);
            }

            SettingsSchemaProbe probe;
            try
            {
                probe = SettingsJson.Deserialize<SettingsSchemaProbe>(bytes);
            }
            catch (Exception ex)
            {
                return Corrupted(ex);
            }

            if (probe == null)
            {
                return Corrupted(new InvalidDataException("файл не содержит объекта настроек"));
            }

            if (probe.SchemaVersion > SettingsLimits.SettingsSchema)
            {
                return new SettingsFileContent(SettingsLoadStatus.NewerSchemaReadOnly, TryParse(bytes), null);
            }

            if (probe.SchemaVersion < SettingsLimits.SettingsSchema)
            {
                return Corrupted(new InvalidDataException("версия схемы не задана или меньше 1"));
            }

            NumberingSettings raw;
            try
            {
                raw = SettingsJson.Deserialize<NumberingSettings>(bytes);
            }
            catch (Exception ex)
            {
                return Corrupted(ex);
            }

            return raw == null
                ? Corrupted(new InvalidDataException("файл не содержит объекта настроек"))
                : new SettingsFileContent(SettingsLoadStatus.Ok, raw, null);
        }

        private static SettingsFileContent Corrupted(Exception error)
        {
            return new SettingsFileContent(SettingsLoadStatus.Corrupted, null, error);
        }

        private static NumberingSettings TryParse(byte[] bytes)
        {
            try
            {
                return SettingsJson.Deserialize<NumberingSettings>(bytes);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// File input-output of the settings module: reading, retries and the lock file.
    /// </summary>
    internal static class SettingsFile
    {
        /// <summary>All bytes of the file; null when the file or its folder does not exist.</summary>
        public static byte[] ReadOrNull(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                using (var buffer = new MemoryStream())
                {
                    stream.CopyTo(buffer);
                    return buffer.ToArray();
                }
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }

        public static void Write(string path, byte[] content)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(content, 0, content.Length);
                stream.Flush(true);
            }
        }

        /// <summary>Exclusive lock of all changes of the settings files; the lock file is never deleted.</summary>
        public static FileStream AcquireLock(string lockPath)
        {
            return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        /// <summary>
        /// One attempt and retries after a pause. Only IOException is retried;
        /// UnauthorizedAccessException is wrapped into IOException without retries.
        /// </summary>
        public static T WithRetries<T>(string path, Func<T> action)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return action();
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new IOException("Нет доступа к файлу настроек: " + path, ex);
                }
                catch (IOException) when (attempt < SettingsLimits.SettingsIoRetries)
                {
                    Thread.Sleep(SettingsLimits.SettingsIoRetryDelayMs);
                }
            }
        }
    }
}
