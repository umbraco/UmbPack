using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UmbPack.Extensions;

namespace UmbPack.Auth
{
    /// <summary>
    /// HMAC authentication utilities.
    /// </summary>
    internal static class HMACAuthentication
    {
        /// <summary>
        /// Gets the signature.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="timestamp">The timestamp.</param>
        /// <param name="nonce">The nonce.</param>
        /// <param name="secret">The secret.</param>
        /// <returns>
        /// The signature.
        /// </returns>
        public static string GetSignature(string requestUri, DateTime timestamp, Guid nonce, string secret)
        {
            return GetSignature(requestUri, timestamp.ToUnixTimestamp().ToString(CultureInfo.InvariantCulture), nonce.ToString(), secret);
        }

        /// <summary>
        /// Gets the signature.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="timestamp">The timestamp.</param>
        /// <param name="nonce">The nonce.</param>
        /// <param name="secret">The secret.</param>
        /// <returns>
        /// The signature.
        /// </returns>
        private static string GetSignature(string requestUri, string timestamp, string nonce, string secret)
        {
            var secretBytes = Encoding.UTF8.GetBytes(secret);

            using (var hmac = new HMACSHA256(secretBytes))
            {
                var signatureString = $"{requestUri}{timestamp}{nonce}";
                var signatureBytes = Encoding.UTF8.GetBytes(signatureString);
                var computedHashBytes = hmac.ComputeHash(signatureBytes);
                var computedString = Convert.ToBase64String(computedHashBytes);
                return computedString;
            }
        }

        /// <summary>
        /// Returns the token authorization header value as a BASE64 encoded string.
        /// </summary>
        /// <param name="signature">The signature.</param>
        /// <param name="nonce">The nonce.</param>
        /// <param name="timestamp">The timestamp.</param>
        /// <returns>
        /// The token authorization header.
        /// </returns>
        public static string GenerateAuthorizationHeader(string signature, Guid nonce, DateTime timestamp)
        {
            var headerString = $"{signature}:{nonce}:{timestamp.ToUnixTimestamp().ToString(CultureInfo.InvariantCulture)}";
            var headerBytes = Encoding.UTF8.GetBytes(headerString);

            return Convert.ToBase64String(headerBytes);
        }
    }
}