using System;
using KSC.Observability;
using Xunit;

namespace KSC.Observability.Tests
{
    public class ObservabilityOptionsTests
    {
        [Fact]
        public void Defaults_AreValid()
        {
            var options = new ObservabilityOptions();
            options.Validate(); // should not throw
            Assert.Equal("/metrics", options.MetricsPath);
            Assert.Equal("ksc", options.MetricPrefix);
            Assert.True(options.EnableSystemMetrics);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Validate_RejectsEmptyServiceName(string name)
        {
            var options = new ObservabilityOptions { ServiceName = name };
            Assert.Throws<InvalidOperationException>(() => options.Validate());
        }

        [Fact]
        public void Validate_RejectsMetricsPathWithoutLeadingSlash()
        {
            var options = new ObservabilityOptions { MetricsPath = "metrics" };
            Assert.Throws<InvalidOperationException>(() => options.Validate());
        }

        [Fact]
        public void Validate_RejectsNonPositiveInterval()
        {
            var options = new ObservabilityOptions { SystemMetricsInterval = TimeSpan.Zero };
            Assert.Throws<InvalidOperationException>(() => options.Validate());
        }
    }
}
