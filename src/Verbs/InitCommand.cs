using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CommandLine;
using Semver;
using UmbPack.Properties;

namespace UmbPack.Verbs
{
    /// <summary>
    /// Command line options for the Init verb.
    /// </summary>
    [Verb("init", HelpText = "HelpInit", ResourceType = typeof(HelpTextResource))]
    internal class InitOptions
    {
        [Value(0, MetaName = "Folder", HelpText = "HelpInitFolder", ResourceType = typeof(HelpTextResource))]
        public string Folder { get; set; }

        /* (not supported yet)
        [Option("nuspec", HelpText = "HelpTextNuspec", ResourceType = typeof(HelpTextResource))]
        public string NuSpecFile { get; set; }
        */
    }

    /// <summary>
    /// Init command, asks the user some questions makes a package.xml file.
    /// </summary>
    /// <remarks>
    /// Works like npm init, makes some guesses as to what defaults to use and lets the user enter values, at the end it writes out a package.xml file.
    /// </remarks>
    internal static class InitCommand
    {
        /// <summary>
        /// Runs the command and returns the error code.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The error code.
        /// </returns>
        public static ErrorCode RunAndReturn(InitOptions options)
        {
            var path = string.IsNullOrWhiteSpace(options.Folder) ? "." : options.Folder;
            var currentFolder = new DirectoryInfo(path);

            if (GetPackageFile(options.Folder, out var packageFile) is ErrorCode packageFileErrorCode)
            {
                return packageFileErrorCode;
            }

            var setup = new PackageSetup();

            Console.WriteLine(Resources.Init_Header);
            Console.WriteLine();

            // Gather all the user input
            setup.Name = GetUserInput(Resources.Init_PackageName, Path.GetFileName(currentFolder.Name));
            setup.Description = GetUserInput(Resources.Init_Description, Defaults.Init_Description);
            setup.Version = GetVersionString(Resources.Init_Version, Defaults.Init_Version);
            setup.Url = GetUserInput(Resources.Init_Url, Defaults.Init_Url);
            setup.UmbracoVersion = GetVersionString(Resources.Init_UmbracoVersion, Defaults.Init_UmbracoVersion);
            setup.Author = GetUserInput(Resources.Init_Author, Environment.UserName);
            setup.Website = GetUserInput(Resources.Init_Website, Defaults.Init_Website);
            setup.Licence = GetUserInput(Resources.Init_Licence, Defaults.Init_Licence);
            setup.Contributors = GetUserInput(Resources.Init_Contributors, null);

            // Play it back for confirmation
            Console.WriteLine();
            Console.WriteLine(Resources.Init_Confirm, packageFile);

            var node = MakePackageFile(setup);

            Console.WriteLine(node.Element("info").ToString());

            // Confirm
            var confirm = GetUserInput(Resources.Init_Prompt, Defaults.Init_Prompt).ToUpper();
            if (confirm[0] == 'Y')
            {
                // Save XML to disk.
                node.Save(packageFile);
                Console.WriteLine(Resources.Init_Complete);

                return ErrorCode.Success;
            }

            return ErrorCode.InvalidFunction;
        }

        /// <summary>
        /// Make a package XML from the options.
        /// </summary>
        /// <param name="options">The options entered by the user.</param>
        /// <returns>
        /// XElement containing the package XML info.
        /// </returns>
        private static XElement MakePackageFile(PackageSetup options)
        {
            var node = new XElement("umbPackage");

            var info = new XElement("info");

            var package = new XElement("package");
            package.Add(new XElement("name", options.Name));
            package.Add(new XElement("version", options.Version));
            package.Add(new XElement("iconUrl", ""));
            package.Add(new XElement("licence", options.Licence, new XAttribute("url", GetLicenceUrl(options.Licence))));
            package.Add(new XElement("url", options.Url));
            package.Add(new XElement("requirements", new XAttribute("type", "strict"), new XElement("major", options.UmbracoVersion.Major), new XElement("minor", options.UmbracoVersion.Minor), new XElement("patch", options.UmbracoVersion.Patch)));
            info.Add(package);

            info.Add(new XElement("author", new XElement("name", options.Author), new XElement("website", options.Website)));

            var contributors = options.Contributors?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Where(x => !string.IsNullOrWhiteSpace(x)).Select(s => s.Trim()).ToArray();
            if (contributors?.Length > 0)
            {
                info.Add(new XElement("contributors", contributors.Select(c => new XElement("contributor", c))));
            }

            info.Add(new XElement("readme", new XCData(options.Description)));

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
        /// Workout the URL for the licence based on the string value.
        /// </summary>
        /// <param name="licenceName">The licence name (e.g MIT).</param>
        /// <returns>
        /// The URL for the licence file.
        /// </returns>
        private static string GetLicenceUrl(string licenceName)
        {
            // TODO Get licence URLs from somewhere?
            if (licenceName.Equals("MIT", StringComparison.InvariantCultureIgnoreCase))
            {
                return "https://opensource.org/licenses/MIT";
            }

            return string.Empty;
        }

        /// <summary>
        /// Prompts the user for version string and validates it.
        /// </summary>
        /// <param name="prompt">Text to put in prompt.</param>
        /// <param name="defaultValue">Default value if user just presses enter.</param>
        /// <returns>
        /// SemVersion compatible version.
        /// </returns>
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
        /// Prompt the user for some input, return a default value if they just press enter.
        /// </summary>
        /// <param name="prompt">Prompt for user.</param>
        /// <param name="defaultValue">Default value if they just press enter.</param>
        /// <returns>
        /// The user or default value.
        /// </returns>
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
        /// Validates the path to where the package file is going to be created.
        /// </summary>
        /// <param name="packageFile">The path to a package file.</param>
        /// <param name="filePath">The file path.</param>
        /// <returns>
        /// The exit code.
        /// </returns>
        private static ErrorCode? GetPackageFile(string packageFile, out string filePath)
        {
            filePath = Path.Combine(".", "package.xml");
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

            var directoryPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directoryPath))
            {
                Console.WriteLine(Resources.Init_MissingFolder, directoryPath);

                return ErrorCode.FileNotFound;
            }

            return null;
        }

        /// <summary>
        /// Package setup options.
        /// </summary>
        /// <remarks>
        /// Options that are used in building the package.xml file.
        /// </remarks>
        private class PackageSetup
        {
            /// <summary>
            /// Gets or sets the name.
            /// </summary>
            /// <value>
            /// The name.
            /// </value>
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the version.
            /// </summary>
            /// <value>
            /// The version.
            /// </value>
            public SemVersion Version { get; set; }

            /// <summary>
            /// Gets or sets the URL.
            /// </summary>
            /// <value>
            /// The URL.
            /// </value>
            public string Url { get; set; }

            /// <summary>
            /// Gets or sets the author.
            /// </summary>
            /// <value>
            /// The author.
            /// </value>
            public string Author { get; set; }

            /// <summary>
            /// Gets or sets the website.
            /// </summary>
            /// <value>
            /// The website.
            /// </value>
            public string Website { get; set; }

            /// <summary>
            /// Gets or sets the licence.
            /// </summary>
            /// <value>
            /// The licence.
            /// </value>
            public string Licence { get; set; }

            /// <summary>
            /// Gets or sets the contributors.
            /// </summary>
            /// <value>
            /// The contributors.
            /// </value>
            public string Contributors { get; set; }

            /// <summary>
            /// Gets or sets the umbraco version.
            /// </summary>
            /// <value>
            /// The umbraco version.
            /// </value>
            public SemVersion UmbracoVersion { get; set; }

            /// <summary>
            /// Gets or sets the description.
            /// </summary>
            /// <value>
            /// The description.
            /// </value>
            public string Description { get; set; }
        }
    }
}
