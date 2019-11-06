using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;

namespace Umbraco.Packager.CI
{
    public class Options
    {
        [Option('p', "package", Required = true, HelpText = "Umbraco package .zip file to upload")]
        public string Package { get; set; }

        [Option('k', "key", Required = true, HelpText = "Unique API Key to upload package to our.umbraco.com")]
        public string ApiKey { get; set; }

        [Usage(ApplicationAlias = "UmbracoPackage")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                return new List<Example>() {
                    new Example("Uploads MyAwesomePackage.zip to our.umbraco.com", new Options { Package = "MyAwesomePackage.zip", ApiKey = "YourAPIKey" })
                };
            }
        }
    }
}
