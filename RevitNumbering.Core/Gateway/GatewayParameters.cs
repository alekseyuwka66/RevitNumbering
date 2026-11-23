namespace RevitNumbering.Core.Gateway
{
    /// <summary>
    /// Parameters of the gateway and of loading in slices.
    /// </summary>
    public static class GatewayParameters
    {
        /// <summary>Time budget of one read slice, ms.</summary>
        public const int LoadSliceMs = 150;

        /// <summary>Loading target for 10 000 elements, ms.</summary>
        public const int LoadTargetMs = 1600;

        /// <summary>Restarts of a load because of model changes.</summary>
        public const int LoadMaxRestarts = 3;

        /// <summary>Expected Raise to Execute latency per slice, ms.</summary>
        public const int RaiseLatencyMs = 20;

        /// <summary>Delay before the busy notice is shown, ms.</summary>
        public const int BusyNoticeMs = 1000;
    }
}
