using System.Windows.Threading;
using RevitNumbering.Core.Logging;
using RevitNumbering.Core.Presentation;

namespace RevitNumbering.UI.Services
{
    /// <summary>IUiDispatcher over the dispatcher of the main Revit thread.</summary>
    public sealed class WpfUiDispatcher : IUiDispatcher
    {
        private readonly Dispatcher _dispatcher;
        private readonly ILog _log;

        public WpfUiDispatcher(Dispatcher dispatcher, ILog log)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void Post(Action action, UiPriority priority)
        {
            if (action == null)
            {
                return;
            }

            DispatcherPriority dispatcherPriority =
                priority == UiPriority.Background ? DispatcherPriority.Background : DispatcherPriority.Normal;
            _dispatcher.BeginInvoke(new Action(() => SafeRunner.Run(_log, "Post", action, null)), dispatcherPriority);
        }

        public async Task YieldAsync()
        {
            await Dispatcher.Yield(DispatcherPriority.Background);
        }
    }
}
