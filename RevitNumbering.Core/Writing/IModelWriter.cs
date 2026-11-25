namespace RevitNumbering.Core.Writing
{
    /// <summary>
    /// Writes a plan into the model. The adapter only performs API calls and reports facts;
    /// every decision (admission, precheck, retries) is made by WriteSession in the core.
    /// </summary>
    public interface IModelWriter
    {
        Task<WriteReport> WriteAsync(WritePlan plan, CancellationToken cancellationToken);
    }
}
