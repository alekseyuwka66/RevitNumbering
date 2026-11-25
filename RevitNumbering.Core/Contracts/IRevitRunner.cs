namespace RevitNumbering.Core.Contracts
{
    /// <summary>
    /// Runs an action in the Revit API context. One call at a time.
    /// Rule: the Revit API is used only inside Run or inside a handler of a Revit event.
    /// </summary>
    public interface IRevitRunner
    {
        /// <summary>
        /// The task completes when Revit has run the action; it is cancelled by CancelPending
        /// and fails with the exception of the action.
        /// </summary>
        Task<T> Run<T>(Func<T> action);

        /// <summary>Our call is running right now.</summary>
        bool IsExecuting { get; }

        /// <summary>A call is posted but has not started yet (Revit is busy).</summary>
        bool IsPending { get; }

        /// <summary>Cancels the posted call if it has not started yet.</summary>
        void CancelPending();
    }
}
