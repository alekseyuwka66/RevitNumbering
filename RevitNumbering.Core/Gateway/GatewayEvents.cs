using RevitNumbering.Core.Model;

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

    public sealed class DocumentEventArgs : EventArgs
    {
        public DocumentEventArgs(DocumentKey document)
        {
            Document = document;
        }

        public DocumentKey Document { get; }
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
}
