using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Umbraco.Packager.CI.Properties;
using Umbraco.Packager.CI.Verbs;

namespace Umbraco.Packager.CI
{
    // Exit code conventions
    // https://docs.microsoft.com/en-gb/windows/win32/debug/system-error-codes--0-499-?redirectedfrom=MSDN

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = new HostBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHttpClient();
                services.AddTransient<PackageHelper>();
            }).UseConsoleLifetime();

            var host = builder.Build();

            using (var serviceScope = host.Services.CreateScope())
            {
                var services = serviceScope.ServiceProvider;
                await InternalMain(args, services.GetRequiredService<PackageHelper>());
                   
            }
        }
        internal static async Task InternalMain(string[] args, PackageHelper packageHelper)
        {

            // now uses 'verbs' so each verb is a command
            // 
            // e.g umbpack init or umbpack push
            //
            // these are handled by the Command classes.

            var parser = new CommandLine.Parser(with => {
                with.HelpWriter = null;
                // with.HelpWriter = Console.Out;
                with.AutoVersion = false;
                with.CaseSensitive = false;
            } );

            // TODO: could load the verbs by interface or class

            var parserResults = parser.ParseArguments<PackOptions, PushOptions, InitOptions>(args);

            parserResults
                .WithParsed<PackOptions>(opts => PackCommand.RunAndReturn(opts).Wait())
                .WithParsed<PushOptions>(opts => PushCommand.RunAndReturn(opts, packageHelper).Wait())
                .WithParsed<InitOptions>(opts => InitCommand.RunAndReturn(opts))
                .WithNotParsed(async errs => await DisplayHelp(parserResults, errs));
        }

        static async Task DisplayHelp<T>(ParserResult<T> result, IEnumerable<Error> errs)
        {
            var helpText = HelpText.AutoBuild(result, h =>
            {
                h.AutoVersion = false;
                return h;
            }, e => e);
            
            // Append header with Ascaii Art
            helpText.Heading = Resources.Ascaii + Environment.NewLine + helpText.Heading;
            helpText.AddPostOptionsText(Resources.HelpFooter);
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

    }

}
