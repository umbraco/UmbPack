using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;

using CommandLine;

using Semver;

using Umbraco.Packager.CI.Properties;

namespace Umbraco.Packager.CI.Verbs
{

    /// <summary>
    ///  Command line options for the Init verb
    /// </summary>
    [Verb("init", HelpText = "Initializes a package.xml file")]
    public class InitOptions
    {
        [Value(0,
            MetaName = "Package File",
            HelpText = "Path to where package file should be created")]
        public string PackageFile { get; set; }

        [Option("nuspec", HelpText = "Use a nuspec file as a starting point")]
        public string NuSpecFile { get; set; }
    }


    /// <summary>
    ///  Init command, askes the user some questions makes a package.xml file
    /// </summary>
    /// <remarks>
    ///  Works like npm init, makes some guesses as to what defaults to use
    ///  and lets the user enter values, at the end it writes out a package.xml file
    /// </remarks>
    internal static class InitCommand
    {
        public async static Task<int> RunAndReturn(InitOptions options)
        {

            if (!string.IsNullOrWhiteSpace(options.NuSpecFile))
            {
                // TODO: spawn from a nuspec.
                Console.WriteLine("Creating from a nuspec file is not yet supported.");
                Environment.Exit(1);
            }

            var currentFolder = AppContext.BaseDirectory;


            var packageFile = GetPackageFile(options.PackageFile);

            var setup = new PackageSetup();

            Console.WriteLine(Resources.Init_Header);

            setup.Name = GetUserInput(Resources.Init_PackageName, Path.GetFileName(currentFolder));

            setup.Description = GetUserInput(Resources.Init_Description, "Another Awesome Umbraco Package");

            setup.Version = GetVersionString(Resources.Init_Version, "1.0.0");

            setup.Url = GetUserInput(Resources.Init_Url, "http://our.umbraco.com");

            setup.UmbracoVersion = GetVersionString(Resources.Init_UmbracoVersion, "8.0.0");

            setup.Author = GetUserInput(Resources.Init_Author, Environment.UserName);

            setup.Website = GetUserInput(Resources.Init_Website, "http://our.umbraco.com");

            setup.Licence = GetUserInput(Resources.Init_Licence, "MIT");

            // play it back for confirmation
            Console.WriteLine(Resources.Init_Confirm, packageFile);

            var node = MakePackageFile(setup);

            Console.WriteLine(node.ToString());

            var confirm = GetUserInput(Resources.Init_Prompt, "yes").ToUpper();
            if (confirm[0] == 'Y')
            {
                node.Save(packageFile);
                Environment.Exit(0);
            }
            else
            {
                Environment.Exit(1);
            }

            return 1;
        }

        /// <summary>
        ///  Make a package xml from the options
        /// </summary>
        /// <param name="options">options enterd by the user</param>
        /// <returns>XElement containing the package.xml info</returns>
        private static XElement MakePackageFile(PackageSetup options)
        {
            var node = new XElement("umbPackage");

            var info = new XElement("info");

            var package = new XElement("package");
            package.Add(new XElement("name", options.Name));
            package.Add(new XElement("version", options.Version));
            package.Add(new XElement("iconUrl", ""));
            package.Add(new XElement("licence", options.Licence,
                new XAttribute("url", GetLicenceUrl(options.Licence))));
            
            package.Add(new XElement("url", options.Url));
            package.Add(new XElement("requirements",
                new XAttribute("type", "strict"),
                new XElement("major", 8),
                new XElement("major", 0),
                new XElement("major", 0)));
            info.Add(package);

            info.Add(new XElement("author",
                        new XElement("name", options.Author),
                        new XElement("webiste", options.Website)));

            info.Add(new XElement("readme",
                new XCData(options.Description)));

            node.Add(info);

            node.Add(new XElement("files"));
            node.Add(new XElement("Actions"));
            node.Add(new XElement("control"));
            node.Add(new XElement("DocumentTypes"));
            node.Add(new XElement("Templates"));
            node.Add(new XElement("Stylesheets"));
            node.Add(new XElement("Macros"));
            node.Add(new XElement("DictionaryItems"));
            node.Add(new XElement("Languages"));
            node.Add(new XElement("DataTypes"));

            return node;

        }

        /// <summary>
        ///  Workout the URL for the licence based on the string value
        /// </summary>
        /// <param name="licenceName">Licence Name (e.g MIT)</param>
        /// <returns>URL for the licence file</returns>
        private static string GetLicenceUrl(string licenceName)
        {
            // TODO - get licence urls from somewhere?
            if (licenceName.Equals("MIT", StringComparison.InvariantCultureIgnoreCase))
            {
                return "https://opensource.org/licenses/MIT";
            }

            return string.Empty;
        }
        
        /// <summary>
        ///  Prompts the user for version string and validates it.
        /// </summary>
        /// <param name="prompt">text to put in prompt</param>
        /// <param name="defaultValue">default value if user just presses enter</param>
        /// <returns>SemVersion compatable version</returns>
        private static SemVersion GetVersionString(string prompt, string defaultValue)
        {
            while (true)
            {
                var versionString = GetUserInput(prompt, defaultValue);
                if (SemVersion.TryParse(versionString, out SemVersion version))
                {
                    return version;
                }
                else
                {
                    Console.WriteLine(Resources.Init_InvalidVersion, versionString);
                }
            }
        }

        /// <summary>
        ///  Prompt the user for some input, return a default value if they just press enter
        /// </summary>
        /// <param name="prompt">Prompt for user</param>
        /// <param name="defaultValue">Default value if they just press enter</param>
        /// <returns>user value or default</returns>
        private static string GetUserInput(string prompt, string defaultValue)
        {
            Console.Write($"{prompt}: ");
            if (!string.IsNullOrWhiteSpace(defaultValue))
            {
                Console.Write($"({defaultValue}) ");
            }

            var value = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            return value;
        }

        /// <summary>
        ///  Validates the path to where the package file is going to be created
        /// </summary>
        /// <param name="packageFile">path to a package file</param>
        private static string GetPackageFile(string packageFile)
        {
            var currentFolder = Path.GetDirectoryName(AppContext.BaseDirectory);
            var filePath = Path.Combine(currentFolder, "package.xml");

            if (!string.IsNullOrWhiteSpace(packageFile))
            {
                if (Path.HasExtension(packageFile))
                {
                    filePath = packageFile;
                }
                else
                {
                    filePath = Path.Combine(packageFile, "package.xml");
                }
            }

            if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            {
                Console.WriteLine(Resources.Init_MissingFolder, Path.GetDirectoryName(filePath));
                Environment.Exit(2);
            }

            return filePath;

        }

        /// <summary>
        ///  Package Setup options
        /// </summary>
        /// <remarks>
        ///  Options that are used in building the package.xml file. 
        /// </remarks>
        private class PackageSetup
        {
            public string Name { get; set; }
            public SemVersion Version { get; set; }
            public string Url { get; set; }
            public string Author { get; set; }
            public string Website { get; set; }
            public string Licence { get; set; }

            public SemVersion UmbracoVersion { get; set; }
            public string Description { get; set; }
        }
    }
}
