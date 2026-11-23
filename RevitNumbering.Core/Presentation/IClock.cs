namespace RevitNumbering.Core.Presentation
{
    public interface IClock
    {
        DateTime UtcNow { get; }

        /// <summary>Timer of the busy notice.</summary>
        Task Delay(TimeSpan delay, CancellationToken cancellationToken);
    }
}
