namespace RevitNumbering.Core.Presentation
{
    public enum UiPriority
    {
        Normal,
        Background
    }

    /// <summary>
    /// Access to the window thread for view models (WPF implementation lives in RevitNumbering/UI).
    /// </summary>
    public interface IUiDispatcher
    {
        void Post(Action action, UiPriority priority);

        /// <summary>Lets the window render (Background priority).</summary>
        Task YieldAsync();
    }
}
