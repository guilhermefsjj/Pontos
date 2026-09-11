using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Pontos.Models;

namespace Pontos.Security
{
    /// <summary>
    /// Validates the integrity of Point and UserProgress records.
    /// Prevents casual tampering and ensures data consistency.
    /// </summary>
    public class IntegrityValidator
    {
        private readonly byte[] _securityKey;

        public IntegrityValidator()
        {
            _securityKey = CryptoHelper.DeriveSecurityKey();
        }

        /// <summary>
        /// Validates a single Point's signature.
        /// </summary>
        public bool ValidatePoint(Point point)
        {
            if (point == null)
                return false;

            if (string.IsNullOrEmpty(point.Signature) || string.IsNullOrEmpty(point.UniqueId))
                return false;

            // Reconstruct the signed data (everything except the signature)
            string dataToVerify = JsonConvert.SerializeObject(new
            {
                point.UniqueId,
                point.EarnedAt,
                point.ImagePath
            }, Formatting.None);

            return CryptoHelper.VerifyHmacSha256(dataToVerify, point.Signature, _securityKey);
        }

        /// <summary>
        /// Validates UserProgress and all its associated points.
        /// </summary>
        public bool ValidateUserProgress(UserProgress progress)
        {
            if (progress == null)
                return false;

            // Validate the progress record itself
            if (string.IsNullOrEmpty(progress.Signature))
                return false;

            string dataToVerify = JsonConvert.SerializeObject(new
            {
                progress.TotalPoints,
                progress.CurrentLevel,
                progress.LastPointEarnedAt,
                progress.TotalUsageMinutes,
                progress.MachineId,
                progress.DataVersion
            }, Formatting.None);

            if (!CryptoHelper.VerifyHmacSha256(dataToVerify, progress.Signature, _securityKey))
                return false;

            // Validate all earned points
            foreach (var point in progress.EarnedPoints)
            {
                if (!ValidatePoint(point))
                    return false;
            }

            // Verify point count matches
            if (progress.EarnedPoints.Count != progress.TotalPoints)
                return false;

            // Verify all point IDs are unique
            var uniqueIds = progress.EarnedPoints.Select(p => p.UniqueId).Distinct().Count();
            if (uniqueIds != progress.EarnedPoints.Count)
                return false;

            return true;
        }

        /// <summary>
        /// Signs a Point record.
        /// </summary>
        public void SignPoint(Point point)
        {
            if (point == null)
                throw new ArgumentNullException(nameof(point));

            // Generate unique ID if not already set
            if (string.IsNullOrEmpty(point.UniqueId))
            {
                point.UniqueId = CryptoHelper.GenerateUniquePointId();
            }

            // Data to sign (excluding the signature field)
            string dataToSign = JsonConvert.SerializeObject(new
            {
                point.UniqueId,
                point.EarnedAt,
                point.ImagePath
            }, Formatting.None);

            point.Signature = CryptoHelper.ComputeHmacSha256(dataToSign, _securityKey);
        }

        /// <summary>
        /// Signs a UserProgress record.
        /// </summary>
        public void SignUserProgress(UserProgress progress)
        {
            if (progress == null)
                throw new ArgumentNullException(nameof(progress));

            // First, ensure all points are signed
            foreach (var point in progress.EarnedPoints)
            {
                if (string.IsNullOrEmpty(point.Signature))
                {
                    SignPoint(point);
                }
            }

            // Data to sign (excluding the signature field)
            string dataToSign = JsonConvert.SerializeObject(new
            {
                progress.TotalPoints,
                progress.CurrentLevel,
                progress.LastPointEarnedAt,
                progress.TotalUsageMinutes,
                progress.MachineId,
                progress.DataVersion
            }, Formatting.None);

            progress.Signature = CryptoHelper.ComputeHmacSha256(dataToSign, _securityKey);
        }
    }
}
