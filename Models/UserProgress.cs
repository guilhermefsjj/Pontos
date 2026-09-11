using System;
using System.Collections.Generic;

namespace Pontos.Models
{
    /// <summary>
    /// Represents the user's overall progress including points, levels, and session data.
    /// </summary>
    public class UserProgress
    {
        /// <summary>
        /// Total number of points earned.
        /// </summary>
        public int TotalPoints { get; set; }

        /// <summary>
        /// Current level based on points.
        /// </summary>
        public int CurrentLevel { get; set; }

        /// <summary>
        /// Timestamp of the last point earned.
        /// </summary>
        public DateTime LastPointEarnedAt { get; set; }

        /// <summary>
        /// Timestamp of the last recorded user activity.
        /// </summary>
        public DateTime LastActivityAt { get; set; }

        /// <summary>
        /// Total accumulated usage time in minutes.
        /// </summary>
        public long TotalUsageMinutes { get; set; }

        /// <summary>
        /// List of all earned points.
        /// </summary>
        public List<Point> EarnedPoints { get; set; }

        /// <summary>
        /// Unique machine ID for network mode.
        /// </summary>
        public string MachineId { get; set; }

        /// <summary>
        /// HMAC signature for integrity validation of this progress record.
        /// </summary>
        public string Signature { get; set; }

        /// <summary>
        /// Version of the data format for backward compatibility.
        /// </summary>
        public int DataVersion { get; set; } = 1;

        public UserProgress()
        {
            EarnedPoints = new List<Point>();
            LastActivityAt = DateTime.UtcNow;
            LastPointEarnedAt = DateTime.UtcNow;
            MachineId = Guid.NewGuid().ToString();
        }
    }
}
