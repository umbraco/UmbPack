namespace UmbPack.Auth
{
    /// <summary>
    /// Authentication constants.
    /// </summary>
    internal static class AuthConstants
    {
        public const string ProjectIdHeader = "OurUmbraco-ProjectId";

        public const string MemberIdHeader = "OurUmbraco-MemberId";

        // TODO Allow specifying base URL from command line?
        #if DEBUG
            public const string BaseUrl = "http://localhost:24292";
        #else
            public const string BaseUrl = "https://our.umbraco.com";
        #endif
    }
}