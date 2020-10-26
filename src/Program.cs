using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UmbPack.Properties;
using UmbPack.Verbs;

namespace UmbPack
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var builder = new HostBuilder().ConfigureServices((hostContext, services) =>
            {
                // TODO Change basic HTTP client into typed client
                services.AddHttpClient();
                services.AddTransient<PackageHelper>();
            }).UseConsoleLifetime();

            var host = builder.Build();

            using (var serviceScope = host.Services.CreateScope())
            {
                var services = serviceScope.ServiceProvider;
                var packageHelper = services.GetRequiredService<PackageHelper>();

                using var parser = new Parser(with =>
                {
                    with.HelpWriter = null;
                    with.AutoVersion = false;
                    with.CaseSensitive = false;
                });

                var parserResult = parser.ParseArguments<PackOptions, PushOptions, InitOptions>(args);

                return (int)await parserResult.MapResult(
                    (PackOptions opts) => Task.FromResult(PackCommand.RunAndReturn(opts)),
                    async (PushOptions opts) => await PushCommand.RunAndReturn(opts, packageHelper),
                    (InitOptions opts) => Task.FromResult(InitCommand.RunAndReturn(opts)),
                    errs => Task.FromResult(DisplayHelp(parserResult, errs))
                );
            }
        }

        private static ErrorCode DisplayHelp<T>(ParserResult<T> parserResult, IEnumerable<Error> errs)
        {
            var helpText = HelpText.AutoBuild(parserResult, h =>
            {
                h.AutoVersion = false;
                h.AutoHelp = false;

                return h;
            }, e => e, true);

            // Append header with ASCII art
            helpText.Heading = Resources.Ascii + Environment.NewLine + helpText.Heading;
            helpText.AddPostOptionsText(Resources.HelpFooter);
            Console.WriteLine(helpText);

            // --version or --help
            if (errs.IsVersion() || errs.IsHelp())
            {
                return ErrorCode.Success;
            }

            return ErrorCode.InvalidFunction;
        }
    }
}
