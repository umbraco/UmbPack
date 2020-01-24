using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using CommandLine;
using CommandLine.Text;

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

            // now uses 'verbs' so each verb is a command
            // 
            // e.g umbpack init or umbpack push
            //
            // these are handled by the Command classes.

            var parser = new CommandLine.Parser(with => with.HelpWriter = null);

            // TODO: could load the verbs by interface or class

            var parserResults = parser.ParseArguments<PackOptions, PushOptions, InitOptions>(args);

            parserResults
                .WithParsed<PackOptions>(opts => PackCommand.RunAndReturn(opts))
                .WithParsed<PushOptions>(async opts => await PushCommand.RunAndReturn(opts))
                .WithParsed<InitOptions>(opts => InitCommand.RunAndReturn(opts))
                .WithNotParsed(async errs => await DisplayHelp(parserResults, errs));
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

    }

}
