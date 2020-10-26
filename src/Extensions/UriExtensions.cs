using System;

namespace UmbPack.Extensions
{
    /// <summary>
    /// Extension methods for <see cref="Uri" />.
    /// </summary>
    internal static class UriExtensions
    {
        /// <summary>
        /// Cleans the path and query.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns>
        /// The cleaned path and query.
        /// </returns>
        public static string CleanPathAndQuery(this Uri uri)
        {
            // Sometimes the request path may have double slashes, so make sure to normalize this
            return uri.PathAndQuery.Replace("//", "/");
        }
    }
}