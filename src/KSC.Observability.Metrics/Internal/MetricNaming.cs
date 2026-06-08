namespace KSC.Observability.Metrics.Internal
{
    /// <summary>Builds final metric names from the configurable prefix and a fixed suffix.</summary>
    internal static class MetricNaming
    {
        public static string Build(string prefix, string suffix)
        {
            return prefix + "_" + suffix;
        }
    }
}
