using System;
using System.Globalization;

namespace Umbraco.Packager.CI.Extensions
{
    public static class StringExtensions
    {
        /// <summary>Converts an integer to an invariant formatted string</summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string ToInvariantString(this int str)
        {
            return str.ToString((IFormatProvider) CultureInfo.InvariantCulture);
        }
    }
}