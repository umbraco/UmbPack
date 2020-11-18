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

        /// <summary>
        /// Ensures an input string starts with the supplied value
        /// </summary>
        /// <param name="input"></param>
        /// <param name="toStartWith"></param>
        /// <returns></returns>
        public static string EnsureStartsWith(this string input, string toStartWith)
        {
            return input.StartsWith(toStartWith) ? input : toStartWith + input;
        }

        /// <summary>
        /// Ensures an input string ends with the supplied value
        /// </summary>
        /// <param name="input"></param>
        /// <param name="toEndWith"></param>
        /// <returns></returns>
        public static string EnsureEndsWith(this string input, string toEndWith)
        {
            return input.EndsWith(toEndWith) ? input : input + toEndWith;
        }

    }
}