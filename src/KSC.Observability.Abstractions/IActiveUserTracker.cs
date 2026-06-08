using System;

namespace KSC.Observability
{
    /// <summary>
    /// Tracks how many distinct users are concurrently using the application. A user is counted
    /// as active while their key has been seen within the configured sliding window.
    /// </summary>
    public interface IActiveUserTracker : IDisposable
    {
        /// <summary>Records activity for a user key (typically a session id, login or client ip).</summary>
        void Touch(string userKey);

        /// <summary>Current number of users seen within the sliding window.</summary>
        int CurrentCount { get; }
    }
}
