namespace RevitNumbering.Core.Gateway
{
    public enum RequestState
    {
        Queued,
        Running,
        Completed,
        Cancelled,
        Faulted
    }

    public sealed class RequestStateEventArgs : EventArgs
    {
        public RequestStateEventArgs(RequestState state, string requestName)
        {
            State = state;
            RequestName = requestName;
        }

        public RequestState State { get; }

        public string RequestName { get; }
    }

    public sealed class LoadProgress
    {
        public LoadProgress(int loaded, int total)
        {
            Loaded = loaded;
            Total = total;
        }

        public int Loaded { get; }

        public int Total { get; }
    }

    /// <summary>
    /// State of the requests to the Revit API context (busy notice, cancel button) and the host lifecycle.
    /// Knows nothing about what the requests do.
    /// </summary>
    public interface IRequestService
    {
        event EventHandler<RequestStateEventArgs> RequestStateChanged;

        event EventHandler ApplicationClosing;

        /// <summary>Cancels requests that have not started yet. true: at least one was cancelled.</summary>
        bool CancelPending();
    }
}
