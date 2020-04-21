namespace Umbraco.Packager.CI.Auth
{
    public static class AuthConstants
    {
        public const string ProjectIdHeader = "OurUmbraco-ProjectId";
        public const string MemberIdHeader = "OurUmbraco-MemberId";
        #if DEBUG
            public const string BaseUrl = "http://localhost:24292";
        #else
            public const string BaseUrl = "https://our.umbraco.com";
        #endif
    }
}