using RevitNumbering.Core.Contracts;

namespace RevitNumbering.Core.Session
{
    public enum ApplyOutcome
    {
        // The values were written. See Write for what was not written, and why.
        Written,

        // The model differs from what the window showed. Nothing was written. The window got fresh data.
        DataChanged,

        // The window document was closed or is no longer active. Nothing was written.
        DocumentUnavailable,

        // The user cancelled the write. It was rolled back, nothing was written.
        Cancelled,
    }

    public sealed class ApplyResult
    {
        public ApplyResult(ApplyOutcome outcome, WriteResult write = null)
        {
            Outcome = outcome;
            Write = write;
        }

        public ApplyOutcome Outcome { get; }

        // Set only when Outcome is Written.
        public WriteResult Write { get; }
    }
}
