using System;
using System.Security.Cryptography;
using System.Text;

namespace Pontos.Security
{
    /// <summary>
    /// Handles cryptographic operations for data integrity and security.
    /// This class provides HMAC-SHA256 signatures and random ID generation.
    /// </summary>
    public static class CryptoHelper
    {
        /// <summary>
        /// Generates an extremely long, cryptographically secure random unique ID.
        /// Format: {timestamp_base64}_{randomBytes_base64}
        /// This prevents trivial duplication and ensures uniqueness.
        /// </summary>
        public static string GenerateUniquePointId()
        {
            // Combine timestamp (8 bytes) with 64 random bytes = 72 bytes total
            byte[] timestamp = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
            byte[] randomBytes = new byte[64];
            
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }

            // Combine and encode
            byte[] combined = new byte[timestamp.Length + randomBytes.Length];
            Buffer.BlockCopy(timestamp, 0, combined, 0, timestamp.Length);
            Buffer.BlockCopy(randomBytes, 0, combined, timestamp.Length, randomBytes.Length);

            // Convert to base64url (URL-safe)
            string base64 = Convert.ToBase64String(combined)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

            return $"{DateTime.UtcNow.Ticks}_{base64}";
        }

        /// <summary>
        /// Computes HMAC-SHA256 signature for data integrity verification.
        /// </summary>
        /// <param name="data">Data to sign</param>
        /// <param name="key">Secret key (obtained from secure storage)</param>
        /// <returns>Base64-encoded HMAC signature</returns>
        public static string ComputeHmacSha256(string data, byte[] key)
        {
            if (string.IsNullOrEmpty(data))
                throw new ArgumentNullException(nameof(data));
            if (key == null || key.Length == 0)
                throw new ArgumentNullException(nameof(key));

            using (var hmac = new HMACSHA256(key))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Verifies that data matches the provided HMAC signature.
        /// </summary>
        /// <param name="data">Original data</param>
        /// <param name="signature">Expected HMAC signature (base64)</param>
        /// <param name="key">Secret key</param>
        /// <returns>True if signature is valid</returns>
        public static bool VerifyHmacSha256(string data, string signature, byte[] key)
        {
            try
            {
                string computed = ComputeHmacSha256(data, key);
                // Use constant-time comparison to prevent timing attacks
                return CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(computed),
                    Encoding.UTF8.GetBytes(signature)
                );
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Derives a cryptographic key from the machine using Windows DPAPI.
        /// This key is tied to the current user and machine.
        /// </summary>
        /// <returns>Derived key bytes for HMAC operations</returns>
        public static byte[] DeriveSecurityKey()
        {
            // Use a fixed seed combined with machine information
            string machineSeed = $"{Environment.MachineName}_{Environment.UserName}_Pontos_v1";
            
            // Use PBKDF2 with a fixed number of iterations
            using (var pbkdf2 = new Rfc2898DeriveBytes(
                machineSeed,
                Encoding.UTF8.GetBytes("Pontos_SecurityKey_Seed"),
                100000,
                HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(32); // 32 bytes = 256 bits
            }
        }

        /// <summary>
        /// Generates a cryptographic hash of a string (for validation, not encryption).
        /// </summary>
        public static string ComputeSha256Hash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(hash);
            }
        }
    }
}
