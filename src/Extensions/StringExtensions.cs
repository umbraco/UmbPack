namespace UmbPack.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="string" />.
    /// </summary>
    internal static class StringExtensions
    {
        /// <summary>
        /// Ensures an input string starts with the specified value.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="toStartWith">The value to start with.</param>
        /// <returns>
        /// The input string that starts with the specified value.
        /// </returns>
        public static string EnsureStartsWith(this string input, string toStartWith)
        {
            return input.StartsWith(toStartWith) ? input : toStartWith + input;
        }

        /// <summary>
        /// Ensures an input string ends with the specified value.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <param name="toEndWith">The value to end with.</param>
        /// <returns>
        /// The input string that ends with the specified value.
        /// </returns>
        public static string EnsureEndsWith(this string input, string toEndWith)
        {
            return input.EndsWith(toEndWith) ? input : input + toEndWith;
        }
    }
}