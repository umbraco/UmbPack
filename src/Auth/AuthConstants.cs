namespace Umbraco.Packager.CI.Auth
{
    public class AuthConstants
    {
        public const string ProjectIdClaim = "http://our.umbraco/projects/projectid";
        public const string MemberIdClaim = "http://our.umbraco/projects/memberid";
        public const string BearerTokenClaimType = "http://our.umbraco/projects";
        public const string BearerTokenClaimValue = "yes";
        public const string BearerTokenAuthenticationType = "OurProjects";
        
        public const string ProjectIdHeader = "OurUmbraco-ProjectId";
        public const string MemberIdHeader = "OurUmbraco-MemberId";
        #if DEBUG
            public const string BaseUrl = "http://localhost:24292";
        #else
            public const string BaseUrl = "https://our.umbraco.com";
        #endif
    }
}