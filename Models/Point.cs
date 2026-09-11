using System;
using System.Collections.Generic;

namespace Pontos.Models
{
    /// <summary>
    /// Represents a single point earned by the user.
    /// Each point has a unique ID, timestamp, associated image, and HMAC signature for integrity validation.
    /// </summary>
    public class Point
    {
        /// <summary>
        /// Extremely long random unique identifier for this point.
        /// Format: {timestamp}_{cryptographicRandomBytes}
        /// </summary>
        public string UniqueId { get; set; }

        /// <summary>
        /// Timestamp when the point was earned (UTC).
        /// </summary>
        public DateTime EarnedAt { get; set; }

        /// <summary>
        /// Path to the associated random image file.
        /// </summary>
        public string? ImagePath { get; set; }

        /// <summary>
        /// HMAC-SHA256 signature for integrity validation.
        /// Prevents casual tampering with the point record.
        /// </summary>
        public string Signature { get; set; }

        /// <summary>
        /// Metadata about the point (optional JSON data).
        /// </summary>
        public Dictionary<string, string>? Metadata { get; set; }

        public Point()
        {
            EarnedAt = DateTime.UtcNow;
            Metadata = new Dictionary<string, string>();
        }
    }
}
