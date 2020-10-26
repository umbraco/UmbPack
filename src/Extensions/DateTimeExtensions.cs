using System;

namespace UmbPack.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="DateTime" />.
    /// </summary>
    internal static class DateTimeExtensions
    {
        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Converts the <see cref="DateTime" /> to a Unix timestamp.
        /// </summary>
        /// <param name="dateTime">The date time.</param>
        /// <returns>
        /// The Unix timestamp.
        /// </returns>
        public static double ToUnixTimestamp(this DateTime dateTime)
        {
            // TODO Should the return type be converted to long?
            return (TimeZoneInfo.ConvertTimeToUtc(dateTime) - epoch).TotalSeconds;
        }
    }
}