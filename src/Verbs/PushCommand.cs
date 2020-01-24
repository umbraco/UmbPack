using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Handlers;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using CommandLine;

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

            // Check we can find the file
            Verify.FilePath(filePath);

            // Check File is a ZIP
            Verify.IsZip(filePath);

            // Check zip contains valid package.xml
            Verify.ContainsPackageXml(filePath);

            // Verify API Key is valid with our.umbraco.com
            await Verify.ApiKeyIsValid(apiKey);

            // Parse package.xml before upload to print out info
            // and to use for comparisson on what is already uploaded
            var packageInfo = Parse.PackageXml(filePath);

            // Prompt all the things
            // .NET Version
            // filetype: Package or HotFix (P or H)
            // 


            // The API token check - WebAPI needs to respond with list of current files
            // Verify same file name does not already exist on our.umb
            // Verify we have a newer version that latest/current file for project

            // TODO: Check PackageInfo.Version from Parse does not exist in WebAPI of current files

            // OK all checks passed - time to upload it
            // With a nice progress bar
            await UploadPackage(filePath, apiKey);

            // Got this far then it got uploaded to our.umb all OK
            Console.WriteLine($"The package '{filePath}' was sucessfully uploaded to our.umbraco.com");

            return 0;
        }

        private static async Task UploadPackage(string filePath, string apiKey)
        {
            try
            {
                // HttpClient will use this event handler to give us
                // Reporting on how its progress the file upload
                var processMsgHander = new ProgressMessageHandler(new HttpClientHandler());
                processMsgHander.HttpSendProgress += (sender, e) =>
                {
                    // Could try to reimplement progressbar - but that library did not work in GH Actions :(
                    var percent = e.ProgressPercentage;
                };

                using (var client = new HttpClient(processMsgHander))
                {
                    //client.BaseAddress = new Uri("http://localhost:24292");
                    //client.BaseAddress = new Uri("http://our.umbraco.local");
                    client.BaseAddress = new Uri("http://ourumb.eu.ngrok.io");
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));


                    MultipartFormDataContent form = new MultipartFormDataContent();
                    var fileInfo = new FileInfo(filePath);
                    var content = new StreamContent(fileInfo.OpenRead());
                    content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                    {
                        Name = "file",
                        FileName = fileInfo.Name
                    };
                    form.Add(content);
                    form.Add(new StringContent("true"), "isCurrent");
                    form.Add(new StringContent("4.7.2"), "dotNetVersion");
                    form.Add(new StringContent("package"), "fileType");
                    form.Add(new StringContent("[{Version: '8.3.0'}]"), "umbracoVersions");

                    var httpResponse = await client.PostAsync("/Umbraco/Api/ProjectUpload/UpdatePackage", form);
                    if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine("API Key is invalid");
                        Console.ResetColor();

                        // ERROR_ACCESS_DENIED
                        Environment.Exit(5);
                    }
                    else if (httpResponse.IsSuccessStatusCode)
                    {
                        // Get the JSON string content which gives us a list
                        // of current Umbraco Package .zips for this project
                        var apiReponse = await httpResponse.Content.ReadAsStringAsync();
                        Console.WriteLine(apiReponse);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                // Could get network error or our.umb down
                throw;
            }
        }

    }
}
