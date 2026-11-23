using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Gateway
{
    /// <summary>
    /// Queue logic without Revit: order, states, cancellation, completion on document close.
    /// </summary>
    public sealed class RequestQueue
    {
        public RequestQueue(Func<bool> raise)
        {
            throw new NotImplementedException();
        }

#pragma warning disable CS0067 // stub until the implementation is added
        public event EventHandler<RequestStateEventArgs> StateChanged;
#pragma warning restore CS0067

        public Task<T> Enqueue<T>(string name, DocumentKey document, Func<T> work, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public void ExecutePending()
        {
            throw new NotImplementedException();
        }

        public bool CancelPending()
        {
            throw new NotImplementedException();
        }

        public void CancelForDocument(DocumentKey document)
        {
            throw new NotImplementedException();
        }

        public void CancelAll()
        {
            throw new NotImplementedException();
        }
    }
}
