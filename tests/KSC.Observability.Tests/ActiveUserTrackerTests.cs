using System;
using System.Threading;
using KSC.Observability;
using KSC.Observability.Metrics;
using Prometheus;
using Xunit;

namespace KSC.Observability.Tests
{
    public class ActiveUserTrackerTests
    {
        private static PrometheusActiveUserTracker NewTracker(TimeSpan window)
        {
            var registry = global::Prometheus.Metrics.NewCustomRegistry();
            var factory = global::Prometheus.Metrics.WithCustomRegistry(registry);
            var options = new ObservabilityOptions { ActiveUserWindow = window };
            return new PrometheusActiveUserTracker(factory, options);
        }

        [Fact]
        public void DistinctKeys_AreCountedOnce()
        {
            using var tracker = NewTracker(TimeSpan.FromMinutes(5));

            tracker.Touch("user-a");
            tracker.Touch("user-a");
            tracker.Touch("user-b");

            Assert.Equal(2, tracker.CurrentCount);
        }

        [Fact]
        public void Prune_RemovesEntriesOlderThanWindow()
        {
            using var tracker = NewTracker(TimeSpan.FromMilliseconds(20));

            tracker.Touch("user-a");
            tracker.Touch("user-b");
            Assert.Equal(2, tracker.CurrentCount);

            Thread.Sleep(60);
            tracker.Prune();

            Assert.Equal(0, tracker.CurrentCount);
        }

        [Fact]
        public void Touch_AfterPrune_ReAddsUser()
        {
            using var tracker = NewTracker(TimeSpan.FromMilliseconds(20));

            tracker.Touch("user-a");
            Thread.Sleep(60);
            tracker.Prune();
            Assert.Equal(0, tracker.CurrentCount);

            tracker.Touch("user-a");
            Assert.Equal(1, tracker.CurrentCount);
        }
    }
}
