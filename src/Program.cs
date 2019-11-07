using CommandLine;
using CommandLine.Text;
using ShellProgressBar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
        private static readonly HttpClient _client = new HttpClient();

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

            // Check we can find the file
            Verify.FilePath(filePath);

            // Check File is a ZIP
            Verify.IsZip(filePath);

            // Check zip contains valid package.xml
            Verify.ContainsPackageXml(filePath);

            // Config HTTPClient
            _client.BaseAddress = new Uri("http://our.umbraco.local");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Verify API Key is valid with our.umbraco.com
            // Could throw network connection errors or invalid key
            // TODO: The API token check from our.umb - WebAPI needs to respond with list of current files
            await Verify.ApiKeyIsValid(_client);

            // Parse package.xml before upload to print out info
            // and to use for comparisson on what is already uploaded
            var packageInfo = Parse.PackageXml(filePath);

            // The API token check - WebAPI needs to respond with list of current files
            // Verify same file name does not already exist on our.umb
            // Verify we have a newer version that latest/current file for project

            // TODO: Check PackageInfo.Version from Parse does not exist in WebAPI of current files

            // OK all checks passed - time to upload it
            // With a nice progress bar
            await UploadPackage(filePath);

            // Got this far then it got uploaded to our.umb all OK
            Console.WriteLine($"The package '{filePath}' was sucessfully uploaded to our.umbraco.com");

            Environment.Exit(0);
        }

        private static async Task UploadPackage(string filePath)
        {
            // https://github.com/Mpdreamz/shellprogressbar
            var options = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Yellow,
                ForegroundColorDone = ConsoleColor.DarkGreen,
                BackgroundColor = ConsoleColor.DarkGray,
                BackgroundCharacter = '\u2593'
            };

            int ticks = 10;
            using(var pbar = new ProgressBar(ticks, "Uploading Package to our.umbraco.com", options))
            {
                for (var i = 0; i < ticks; i++)
                {
                    Thread.Sleep(1000);
                    pbar.Tick();
                }
            }


            // TODO - Google/Research .NET Core WebClient POST File
            // TODO - Figure out how to get a progress/report when uploading & tie into 3rd Party progress bar above

        }
    }

}
