using RevitNumbering.Core.Contracts;
using RevitNumbering.Core.Model;
using RevitNumbering.Core.Numbering;

namespace RevitNumbering.Core.Session
{
    public enum ApplyOutcome
    {
        /// <summary>The values were written; see Write for what was skipped and why.</summary>
        Written,

        /// <summary>The model differs from what the window showed: nothing was written, the window got fresh data.</summary>
        DataChanged,

        /// <summary>The document of the window was closed or stopped being the active one: nothing was written.</summary>
        DocumentUnavailable,

        /// <summary>The user cancelled the call before Revit started it.</summary>
        Cancelled,
    }

    /// <summary>Result of Apply.</summary>
    public sealed class ApplyResult
    {
        public ApplyOutcome Outcome { get; private set; }

        /// <summary>Set when Outcome is Written.</summary>
        public WriteResult Write { get; private set; }

        /// <summary>The category as it is in the model after the call (Written, DataChanged).</summary>
        internal CategoryData Fresh { get; private set; }

        /// <summary>The preview of the fresh data (DataChanged).</summary>
        internal Preview FreshPreview { get; private set; }

        internal static ApplyResult Written(WriteResult write, CategoryData fresh)
        {
            return new ApplyResult { Outcome = ApplyOutcome.Written, Write = write, Fresh = fresh };
        }

        internal static ApplyResult DataChanged(CategoryData fresh, Preview preview)
        {
            return new ApplyResult { Outcome = ApplyOutcome.DataChanged, Fresh = fresh, FreshPreview = preview };
        }

        internal static ApplyResult DocumentUnavailable()
        {
            return new ApplyResult { Outcome = ApplyOutcome.DocumentUnavailable };
        }

        internal static ApplyResult Cancelled()
        {
            return new ApplyResult { Outcome = ApplyOutcome.Cancelled };
        }
    }
}
