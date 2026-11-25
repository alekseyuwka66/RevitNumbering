using Autodesk.Revit.UI;
using RevitNumbering.Core.Contracts;

namespace RevitNumbering.Revit.Runner
{
    /// <summary>
    /// Runs actions of the window in the Revit API context through one ExternalEvent.
    /// Holds one posted call at a time; waiting is asynchronous only (never .Result or .Wait()).
    /// </summary>
    internal sealed class RevitRunner : IRevitRunner, IExternalEventHandler
    {
        private ExternalEvent _event;
        private Func<object> _pending;
        private TaskCompletionSource<object> _result;

        public bool IsExecuting { get; private set; }

        public bool IsPending => _pending != null;

        /// <summary>The application of the running call: needed to pick and to show elements.</summary>
        public UIApplication Application { get; private set; }

        /// <summary>Called at the start of every call, before the action.</summary>
        public Action<UIApplication> BeforeAction { get; set; }

        /// <summary>ExternalEvent can be created only in the API context: call from OnStartup.</summary>
        public void Initialize()
        {
            _event = ExternalEvent.Create(this);
        }

        public async Task<T> Run<T>(Func<T> action)
        {
            if (_pending != null || IsExecuting)
            {
                throw new InvalidOperationException("Only one Revit call at a time is supported.");
            }

            var result = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending = () => action();
            _result = result;

            // Pending: the event raised for a cancelled call has not run yet; it will run this call.
            ExternalEventRequest request = _event.Raise();
            if (request != ExternalEventRequest.Accepted && request != ExternalEventRequest.Pending)
            {
                _pending = null;
                _result = null;
                throw new InvalidOperationException("ExternalEvent.Raise was not accepted: " + request);
            }

            return (T)await result.Task;
        }

        public void CancelPending()
        {
            TaskCompletionSource<object> result = _result;
            if (_pending == null)
            {
                return;
            }

            _pending = null;
            _result = null;
            result.SetCanceled();
        }

        /// <summary>Called by Revit when it is free. An exception of the action goes to the task, not to Revit.</summary>
        public void Execute(UIApplication app)
        {
            Func<object> action = _pending;
            TaskCompletionSource<object> result = _result;
            _pending = null;
            _result = null;
            if (action == null)
            {
                // The call was cancelled before Revit got to it.
                return;
            }

            Application = app;
            IsExecuting = true;
            try
            {
                BeforeAction?.Invoke(app);
                result.SetResult(action());
            }
            catch (Exception ex)
            {
                result.SetException(ex);
            }
            finally
            {
                IsExecuting = false;
            }
        }

        public string GetName()
        {
            return "RevitNumbering";
        }

        public void Dispose()
        {
            CancelPending();
            _event?.Dispose();
            _event = null;
        }
    }
}
