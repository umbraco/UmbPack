using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Handlers;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using CommandLine;
using Umbraco.Packager.CI.Properties;

namespace Umbraco.Packager.CI.Verbs
{
    /// <summary>
    ///  Options for the Push verb
    /// </summary>
    [Verb("push", HelpText = "Pushes an umbraco package to our.umbraco.com")]
    public class PushOptions
    {
        [Value(0, MetaName = "package.zip", Required = true,
            HelpText = "Path to the package zip you want to push")]
        public string Package { get; set; }

        [Option("Key", HelpText = "ApiKey")]
        public string ApiKey { get; set; }

        [Option("Publish", Default = true, HelpText = "Makes this package the latest version")]
        public bool Publish { get; set; }

        [Option("DotNetVersion", Default = "4.7.2", HelpText = "Chaange the DotNetVersion of the package")]
        public string DotNetVersion { get; set; }

        [Option("WorksWith", Default = "8.5.0", HelpText = "Compatable versions")]
        public string WorksWith { get; set; }
    }


    internal static class PushCommand
    {
        public static async Task<int> RunAndReturn(PushOptions options)
        {
            // --package=MyFile.zip
            // --package=./MyFile.zip
            // --package=../MyParentFolder.zip
            var filePath = options.Package;

            var apiKey = options.ApiKey;

            var packageHelper = new PackageHelper();

            // Check we can find the file
            packageHelper.EnsurePackageExists(filePath);

            // Check File is a ZIP          
            packageHelper.EnsureIsZip(filePath);

            // Check zip contains valid package.xml
            packageHelper.EnsureContainsPackageXml(filePath);

            // gets a package list from our.umbraco
            // if the api key is invalid we will also find out here.
            var packages = await packageHelper.GetPackageList(apiKey);

            if (packages != null)
            { 
                packageHelper.EnsurePackageDoesntAlreadyExists(packages, filePath);
            }

            // Parse package.xml before upload to print out info
            // and to use for comparisson on what is already uploaded
            var packageInfo = Parse.PackageXml(filePath);

            // OK all checks passed - time to upload it
            await UploadPackage(options);

            // Got this far then it got uploaded to our.umb all OK
            Console.WriteLine(Resources.Push_Complete, filePath);

            return 0;
        }

        private static async Task UploadPackage(PushOptions options)
        {
            try
            {
                Console.WriteLine("Pushing {0}", options.Package);
                // HttpClient will use this event handler to give us
                // Reporting on how its progress the file upload
                var processMsgHander = new ProgressMessageHandler(new HttpClientHandler());
                processMsgHander.HttpSendProgress += (sender, e) =>
                {
                    // Could try to reimplement progressbar - but that library did not work in GH Actions :(
                    var percent = e.ProgressPercentage;
                };

                var packageHelper = new PackageHelper();

                using (var client = packageHelper.GetClientBase(options.ApiKey))
                {
                    MultipartFormDataContent form = new MultipartFormDataContent();
                    var fileInfo = new FileInfo(options.Package);
                    var content = new StreamContent(fileInfo.OpenRead());
                    content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                    {
                        Name = "file",
                        FileName = fileInfo.Name
                    };
                    form.Add(content);
                    form.Add(new StringContent(options.Publish.ToString()), "isCurrent");
                    form.Add(new StringContent(options.DotNetVersion), "dotNetVersion");
                    form.Add(new StringContent("package"), "fileType");
                    form.Add(GetVersionCompatability(options.WorksWith), "umbracoVersions");

                    var httpResponse = await client.PostAsync("/Umbraco/Api/ProjectUpload/UpdatePackage", form);
                    if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        packageHelper.WriteError("Api Key is invalid");
                        Environment.Exit(5); // ERROR_ACCESS_DENIED
                    }
                    else if (httpResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Done");
                        var apiReponse = await httpResponse.Content.ReadAsStringAsync();
                        // Console.WriteLine(apiReponse);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                // Could get network error or our.umb down
                Console.WriteLine("error {0}", ex);
                throw;
            }
        }



        /// <summary>
        ///  returns the version compatability string for uploading the package
        /// </summary>
        /// <param name="worksWithString"></param>
        /// <returns></returns>
        private static StringContent GetVersionCompatability(string worksWithString)
        {
            // TODO: Workout how we can get a latest version from our ? 
            // TODO: Maybe accept wild cards (8.* -> 8.5.0,8.4.0,8.3.0)
            // TODO: Work like nuget e.g '> 8.4.0' 
            var versions = worksWithString
                                .Split(",", StringSplitOptions.RemoveEmptyEntries)
                                .Select(x => $"'{x}'");

            return new StringContent(string.Format("[{{Versions: {0}}}]", string.Join(",", versions)));
        }
    }
}
