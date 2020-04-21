using System;

namespace Umbraco.Packager.CI.Extensions
{
    public static class DateTimeExtensions
    {
        /// <summary>
        /// Gets Unix Timestamp from DateTime object
        /// </summary>
        /// <param name="dateTime">The DateTime object</param>
        public static double ToUnixTimestamp(this DateTime dateTime)
        {
            return (TimeZoneInfo.ConvertTimeToUtc(dateTime) -
                    new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
    }
}