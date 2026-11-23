namespace RevitNumbering.Core.Writing
{
    /// <summary>
    /// Parameters of the write module that the core decides with.
    /// </summary>
    public static class WritingParameters
    {
        /// <summary>Transactions per write, including the first one.</summary>
        public const int WriteMaxAttempts = 5;

        /// <summary>Write target for 10 000 elements without worksharing, ms.</summary>
        public const int ApplyTargetMs = 2500;
    }
}
