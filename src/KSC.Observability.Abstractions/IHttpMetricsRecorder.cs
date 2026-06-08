namespace KSC.Observability
{
    /// <summary>
    /// Records per-request HTTP metrics: the in-flight gauge (how many requests are being
    /// processed right now), a total counter and a latency histogram.
    /// </summary>
    public interface IHttpMetricsRecorder
    {
        /// <summary>Call when a request enters the pipeline; increments the in-flight gauge.</summary>
        void RequestStarted();

        /// <summary>
        /// Call when a request leaves the pipeline; decrements the in-flight gauge and records
        /// the outcome and latency.
        /// </summary>
        /// <param name="method">HTTP verb, e.g. <c>GET</c>.</param>
        /// <param name="path">Normalized route/path, or <c>null</c> when path tracking is disabled.</param>
        /// <param name="statusCode">HTTP status code, e.g. 200.</param>
        /// <param name="elapsedSeconds">Wall-clock duration of the request in seconds.</param>
        void RequestCompleted(string method, string? path, int statusCode, double elapsedSeconds);
    }
}
