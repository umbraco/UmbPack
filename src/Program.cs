using CommandLine;
using CommandLine.Text;
using ShellProgressBar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Handlers;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Umbraco.Packager.CI.Properties;

namespace Umbraco.Packager.CI
{
    // Exit code conventions
    // https://docs.microsoft.com/en-gb/windows/win32/debug/system-error-codes--0-499-?redirectedfrom=MSDN

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var parser = new CommandLine.Parser(with => with.HelpWriter = null);
            var parserResult = parser.ParseArguments<Options>(args);

            await parserResult.MapResult(
                (Options opts) => Run(opts),
                errs => DisplayHelp(parserResult, errs));
        }

        static async Task DisplayHelp<T>(ParserResult<T> result, IEnumerable<Error> errs)
        {
            var helpText = HelpText.AutoBuild(result);
            
            // Append header with Ascaii Art
            helpText.Heading = Resources.Ascaii + Environment.NewLine + helpText.Heading;
            Console.WriteLine(helpText);

            // --version or --help
            if (errs.IsVersion() || errs.IsHelp())
            {
                // 0 is everything is all OK exit code
                Environment.Exit(0);
            }

            // ERROR_INVALID_FUNCTION
            Environment.Exit(1);
        }

        static async Task Run(Options options)
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

            // The API token check - WebAPI needs to respond with list of current files
            // Verify same file name does not already exist on our.umb
            // Verify we have a newer version that latest/current file for project

            // TODO: Check PackageInfo.Version from Parse does not exist in WebAPI of current files

            // OK all checks passed - time to upload it
            // With a nice progress bar
            await UploadPackage(filePath, apiKey);

            // Got this far then it got uploaded to our.umb all OK
            Console.WriteLine($"The package '{filePath}' was sucessfully uploaded to our.umbraco.com");

            Environment.Exit(0);
        }

        private static async Task UploadPackage(string filePath, string apiKey)
        {
            // https://github.com/Mpdreamz/shellprogressbar
            var options = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Yellow,
                ForegroundColorDone = ConsoleColor.DarkGreen,
                BackgroundColor = ConsoleColor.DarkGray,
                BackgroundCharacter = '\u2593'
            };


            // Ticks in this progressbar is set to 100 parts
            // As the ProgressBar does not support Long to use BytesUploaded
            using (var pbar = new ProgressBar(100, "Uploading Package to our.umbraco.com", options))
            {
                try
                {
                    // HttpClient will use this event handler to give us
                    // Reporting on how its progress the file upload
                    var processMsgHander = new ProgressMessageHandler(new HttpClientHandler());
                    processMsgHander.HttpSendProgress += (sender, e) =>
                    {
                        // Increase the number of ticks to
                        // be same number as the percentage reported back from event

                        // TODO: Note this gonna be hard to see moving with our.umb running locally & pkgs generally small files
                        // Would be EVIL to put in a Thread.Sleep()
                        pbar.Tick(e.ProgressPercentage);
                    };

                    using (var client = new HttpClient(processMsgHander))
                    {
                        client.BaseAddress = new Uri("http://localhost:24292");
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

}
