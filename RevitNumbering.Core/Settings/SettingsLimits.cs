namespace RevitNumbering.Core.Settings
{
    /// <summary>
    /// Named constants of the settings module.
    /// </summary>
    internal static class SettingsLimits
    {
        public const int SettingsSchema = 1;

        /// <summary>Retries after the first attempt.</summary>
        public const int SettingsIoRetries = 3;

        /// <summary>Pause before a retry, ms.</summary>
        public const int SettingsIoRetryDelayMs = 100;

        public const double RowToleranceMinMm = 1;

        public const double RowToleranceMaxMm = 100000;

        /// <summary>Smallest allowed word number of the level code.</summary>
        public const int LevelWordNumberMin = 1;
    }
}
