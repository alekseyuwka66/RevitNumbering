using RevitNumbering.Core.Model;

namespace RevitNumbering.Core.Gateway
{
    /// <summary>
    /// Queue logic without Revit: order, states, cancellation, completion on document close.
    /// Invariant: every task returned by Enqueue ends with exactly one of Completed / Cancelled / Faulted.
    /// </summary>
    public sealed class RequestQueue
    {
        private readonly object _sync = new object();
        private readonly List<Request> _queue = new List<Request>();
        private readonly Func<bool> _raise;
        private bool _raisePending;

        /// <param name="raise">ExternalEvent.Raise in the adapter; false means the event was not accepted.</param>
        public RequestQueue(Func<bool> raise)
        {
            _raise = raise ?? throw new ArgumentNullException(nameof(raise));
        }

        public event EventHandler<RequestStateEventArgs> StateChanged;

        public Task<T> Enqueue<T>(string name, DocumentKey document, Func<T> work, CancellationToken ct)
        {
            if (work == null)
            {
                throw new ArgumentNullException(nameof(work));
            }

            var request = new Request<T>(name, document, work);
            if (ct.IsCancellationRequested)
            {
                request.Cancel(ct);
                return request.Task;
            }

            bool needRaise;
            lock (_sync)
            {
                _queue.Add(request);
                needRaise = !_raisePending;
                if (needRaise)
                {
                    _raisePending = true;
                }
            }

            OnStateChanged(RequestState.Queued, name);

            if (needRaise && !TryRaise(out Exception raiseError))
            {
                bool removed;
                lock (_sync)
                {
                    _raisePending = false;
                    removed = _queue.Remove(request);
                }

                if (removed)
                {
                    Reject(request, raiseError);
                }

                return request.Task;
            }

            if (ct.CanBeCanceled)
            {
                request.Registration = ct.Register(() => CancelQueued(request, ct));
                if (request.Task.IsCompleted)
                {
                    request.Registration.Dispose();
                }
            }

            return request.Task;
        }

        /// <summary>Runs one queued request. Called from IExternalEventHandler.Execute (API context).</summary>
        public void ExecutePending()
        {
            Request request;
            lock (_sync)
            {
                _raisePending = false;
                if (_queue.Count == 0)
                {
                    return;
                }

                request = _queue[0];
                _queue.RemoveAt(0);
            }

            bool finished = false;
            try
            {
                OnStateChanged(RequestState.Running, request.Name);
                RequestState final = request.Execute();
                finished = true;
                request.Registration.Dispose();
                OnStateChanged(final, request.Name);
            }
            finally
            {
                if (!finished)
                {
                    // Failure of the queue itself: the current request must not stay pending.
                    request.Fault(new InvalidOperationException("Request queue failure."));
                    request.Registration.Dispose();
                }

                RaiseForRemaining();
            }
        }

        /// <summary>Cancels every request that has not started yet. true: at least one was cancelled.</summary>
        public bool CancelPending()
        {
            return CancelQueued(request => true) > 0;
        }

        public void CancelForDocument(DocumentKey document)
        {
            CancelQueued(request => Equals(request.Document, document));
        }

        public void CancelAll()
        {
            CancelQueued(request => true);
        }

        private int CancelQueued(Func<Request, bool> filter)
        {
            List<Request> cancelled;
            lock (_sync)
            {
                cancelled = _queue.Where(filter).ToList();
                foreach (Request request in cancelled)
                {
                    _queue.Remove(request);
                }
            }

            foreach (Request request in cancelled)
            {
                request.Cancel(CancellationToken.None);
                request.Registration.Dispose();
                OnStateChanged(RequestState.Cancelled, request.Name);
            }

            return cancelled.Count;
        }

        private void CancelQueued(Request request, CancellationToken ct)
        {
            bool removed;
            lock (_sync)
            {
                removed = _queue.Remove(request);
            }

            if (removed)
            {
                request.Cancel(ct);
                OnStateChanged(RequestState.Cancelled, request.Name);
            }
        }

        private void RaiseForRemaining()
        {
            lock (_sync)
            {
                if (_queue.Count == 0 || _raisePending)
                {
                    return;
                }

                _raisePending = true;
            }

            if (TryRaise(out Exception raiseError))
            {
                return;
            }

            List<Request> rejected;
            lock (_sync)
            {
                _raisePending = false;
                rejected = _queue.ToList();
                _queue.Clear();
            }

            foreach (Request request in rejected)
            {
                Reject(request, raiseError);
            }
        }

        private bool TryRaise(out Exception error)
        {
            error = null;
            try
            {
                return _raise();
            }
            catch (Exception exception)
            {
                error = exception;
                return false;
            }
        }

        private void Reject(Request request, Exception raiseError)
        {
            request.Fault(new RequestRejectedException(request.Name, raiseError));
            request.Registration.Dispose();
            OnStateChanged(RequestState.Faulted, request.Name);
        }

        private void OnStateChanged(RequestState state, string name)
        {
            StateChanged?.Invoke(this, new RequestStateEventArgs(state, name));
        }

        private abstract class Request
        {
            protected Request(string name, DocumentKey document)
            {
                Name = name;
                Document = document;
            }

            public string Name { get; }

            public DocumentKey Document { get; }

            public CancellationTokenRegistration Registration { get; set; }

            /// <summary>Runs the work and completes the task; returns the final state.</summary>
            public abstract RequestState Execute();

            public abstract void Cancel(CancellationToken ct);

            public abstract void Fault(Exception exception);
        }

        private sealed class Request<T> : Request
        {
            private readonly Func<T> _work;
            private readonly TaskCompletionSource<T> _completion =
                new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Request(string name, DocumentKey document, Func<T> work)
                : base(name, document)
            {
                _work = work;
            }

            public Task<T> Task => _completion.Task;

            public override RequestState Execute()
            {
                T result;
                try
                {
                    result = _work();
                }
                catch (OperationCanceledException exception)
                {
                    Cancel(exception.CancellationToken);
                    return RequestState.Cancelled;
                }
                catch (Exception exception)
                {
                    _completion.TrySetException(exception);
                    return RequestState.Faulted;
                }

                _completion.TrySetResult(result);
                return RequestState.Completed;
            }

            public override void Cancel(CancellationToken ct)
            {
                if (ct.IsCancellationRequested)
                {
                    _completion.TrySetCanceled(ct);
                }
                else
                {
                    _completion.TrySetCanceled();
                }
            }

            public override void Fault(Exception exception)
            {
                _completion.TrySetException(exception);
            }
        }
    }
}
