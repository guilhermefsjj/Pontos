using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Pontos.Services
{
    /// <summary>
    /// Monitors user activity on the computer.
    /// Detects mouse/keyboard activity to determine if the computer is in active use.
    /// </summary>
    public class UserActivity
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("User32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        private DateTime _lastActiveTime;
        private Timer? _activityCheckTimer;
        private const int ACTIVITY_CHECK_INTERVAL = 5000; // Check every 5 seconds

        public event EventHandler<ActivityStatus>? ActivityStatusChanged;

        public UserActivity()
        {
            _lastActiveTime = DateTime.UtcNow;
        }

        public void Start()
        {
            _activityCheckTimer = new Timer(CheckActivity, null, 0, ACTIVITY_CHECK_INTERVAL);
        }

        public void Stop()
        {
            _activityCheckTimer?.Dispose();
            _activityCheckTimer = null;
        }

        /// <summary>
        /// Gets the time elapsed since the last user input.
        /// </summary>
        public TimeSpan GetIdleTime()
        {
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);

            try
            {
                if (GetLastInputInfo(ref lastInputInfo))
                {
                    uint idleTime = (uint)Environment.TickCount - lastInputInfo.dwTime;
                    return TimeSpan.FromMilliseconds(idleTime);
                }
            }
            catch { /* Ignore errors */ }

            return TimeSpan.Zero;
        }

        /// <summary>
        /// Determines if the computer is currently in active use.
        /// Returns true if user activity detected within the idle threshold.
        /// </summary>
        public bool IsComputerActive()
        {
            try
            {
                // Consider active if idle time is less than 15 minutes
                return GetIdleTime().TotalMinutes < 15;
            }
            catch
            {
                return true; // Assume active on error
            }
        }

        private void CheckActivity(object? state)
        {
            bool isActive = IsComputerActive();
            ActivityStatusChanged?.Invoke(this, new ActivityStatus { IsActive = isActive });
        }
    }

    public class ActivityStatus
    {
        public bool IsActive { get; set; }
    }
}
