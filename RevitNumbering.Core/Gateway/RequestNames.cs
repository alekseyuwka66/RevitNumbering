namespace RevitNumbering.Core.Gateway
{
    /// <summary>
    /// Names of gateway requests as they appear in RequestStateEventArgs.RequestName.
    /// </summary>
    public static class RequestNames
    {
        public const string Load = "Load";
        public const string Selection = "Selection";
        public const string Write = "Write";
    }

    /// <summary>
    /// The request was not accepted: raise returned false or threw (inner may be null).
    /// </summary>
    public sealed class RequestRejectedException : Exception
    {
        public RequestRejectedException(string requestName, Exception inner)
            : base("Request '" + requestName + "' was rejected: the external event was not accepted.", inner)
        {
            RequestName = requestName;
        }

        public string RequestName { get; }
    }
}
