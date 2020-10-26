using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using CommandLine;
using UmbPack.Extensions;
using UmbPack.Properties;

namespace UmbPack.Verbs
{
    /// <summary>
    /// Command line options for the Pack verb.
    /// </summary>
    [Verb("pack", HelpText = "HelpPack", ResourceType = typeof(HelpTextResource))]
    internal class PackOptions
    {
        [Value(0, MetaName = "file/folder", Required = true, HelpText = "HelpPackFile", ResourceType = typeof(HelpTextResource))]
        public string FolderOrFile { get; set; }

        [Option('o', "OutputDirectory", HelpText = "HelpPackOutput", ResourceType = typeof(HelpTextResource), Default = ".")]
        public string OutputDirectory { get; set; }

        [Option('v', "Version", HelpText = "HelpPackVersion", ResourceType = typeof(HelpTextResource))]
        public string Version { get; set; }

        [Option('p', "Properties", HelpText = "HelpPackProperties", ResourceType = typeof(HelpTextResource))]
        public string Properties { get; set; }

        [Option('n', "PackageFileName", HelpText = "HelpPackPackageFileName", ResourceType = typeof(HelpTextResource))]
        public string PackageFileName { get; set; }
    }

    /// <summary>
    /// Pack command, lets you create a package.zip either from a package.xml or a folder.
    /// </summary>
    /// <remarks>
    /// You can mark up the package.xml file with a couple of extra things and when its processed by this command, to make the full version.
    /// Example:
    /// 
    /// <code>
    /// <![CDATA[
    /// <files>
    ///     <file path="../path/to/file.dll" orgPath="bin" />
    ///     <folder path="../path/to/folder/App_Plugins" orgPath="App_Plugins" />
    /// </files>
    /// ]]>
    /// </code>
    /// 
    /// This would copy the file to the bin folder in the package and copy the content of the folder to the App_Plugins folder. The structure of the folder beneath this level is also preserved.
    /// </remarks>
    internal static class PackCommand
    {
        /// <summary>
        /// Runs the command and returns the error code.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <returns>
        /// The error code.
        /// </returns>
        public static ErrorCode RunAndReturn(PackOptions options)
        {
            // Make sure the output directory exists
            Directory.CreateDirectory(options.OutputDirectory);

            // Working dir, is where we build a structure of what the package will do
            var workingDir = CreateWorkingFolder(options.OutputDirectory, "__umbpack__tmp");

            // Build folder is the things we ZIP up
            var buildFolder = CreateWorkingFolder(options.OutputDirectory, "__umbpack__build");

            /*
            Console.WriteLine("Option: {0}", options.FolderOrFile);
            Console.WriteLine("Output Folder: {0}", options.OutputDirectory);
            Console.WriteLine("Working Folder: {0}", workingDir);
            Console.WriteLine("Build Folder: {0}", buildFolder);
            */

            var packageFile = options.FolderOrFile;
            bool isFolder = false;

            if (!Path.GetExtension(options.FolderOrFile).Equals(".xml", StringComparison.InvariantCultureIgnoreCase))
            {
                // A folder - we assume the package.xml is in that folder
                isFolder = true;
                packageFile = Path.Combine(options.FolderOrFile, "package.xml");

                Console.WriteLine(Resources.Pack_BuildingFolder, options.FolderOrFile);
            }
            else
            {
                Console.WriteLine(Resources.Pack_BuildingFile);
            }

            if (!File.Exists(packageFile))
            {
                Console.WriteLine(Resources.Pack_MissingXml, packageFile);

                return ErrorCode.FileNotFound;
            }

            Console.WriteLine(Resources.Pack_LoadingFile, packageFile);

            // Load the package XML
            XElement packageXml = null;
            if (!string.IsNullOrWhiteSpace(options.Properties))
            {
                var packageXmlContents = File.ReadAllText(packageFile);

                var props = options.Properties.Split(";", StringSplitOptions.RemoveEmptyEntries).Select(x => x.Split('=')).ToDictionary(x => x[0], x => x[1]);
                foreach (var prop in props)
                {
                    packageXmlContents = packageXmlContents.Replace($"${prop.Key}$", prop.Value);
                }

                packageXml = XElement.Parse(packageXmlContents);
            }
            else
            {
                packageXml = XElement.Load(packageFile);
            }

            Console.WriteLine(Resources.Pack_UpdatingVersion);

            // Stamp the package version.
            var version = GetOrSetPackageVersion(packageXml, options.Version);

            Console.WriteLine(Resources.Pack_GetPackageName);

            // Work out what we are going to call the package
            string packageFileName;
            if (!string.IsNullOrWhiteSpace(options.PackageFileName))
            {
                packageFileName = options.PackageFileName.EnsureEndsWith(".zip");
            }
            else
            {
                ErrorCode? errorCode;
                (packageFileName, errorCode) = GetPackageFileName(packageXml, version);

                if (errorCode.HasValue)
                {
                    return errorCode.Value;
                }
            }

            // Work out what where we are going to output the package to
            var packageOutputPath = Path.Combine(options.OutputDirectory, packageFileName);

            Console.WriteLine(Resources.Pack_AddPackageFiles);

            // Add any files based on what is already in the package.xml
            AddFilesBasedOnPackageXml(packageXml, workingDir);

            // If the source is a folder, grab all the files from that folder
            if (isFolder) AddFilesFromFolders(options.FolderOrFile, workingDir);

            Console.WriteLine(Resources.Pack_BuildPackage);
            BuildPackageFolder(packageXml, workingDir, buildFolder);
            Directory.Delete(workingDir, true);

            CreateZip(buildFolder, packageOutputPath);

            Console.WriteLine(Resources.Pack_Complete);
            Directory.Delete(buildFolder, true);

            return ErrorCode.Success;
        }

        /// <summary>
        /// Creates the working folder.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="subFolder">The sub folder.</param>
        /// <param name="clean">If set to <c>true</c> cleans the folder.</param>
        /// <returns>
        /// The working folder path.
        /// </returns>
        private static string CreateWorkingFolder(string path, string subFolder = "", bool clean = true)
        {
            var folder = Path.Combine(path, subFolder);

            if (clean && Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            Directory.CreateDirectory(folder);

            return folder;
        }

        /// <summary>
        /// Gets the name of the package file.
        /// </summary>
        /// <param name="packageFile">The package file.</param>
        /// <param name="version">The version.</param>
        /// <returns>
        /// The name of the package file and/or the error code.
        /// </returns>
        private static (string, ErrorCode?) GetPackageFileName(XElement packageFile, string version)
        {
            var nameNode = packageFile.Element("info")?.Element("package")?.Element("name");
            if (nameNode != null)
            {
                var name = nameNode.Value.Replace(".", "_").Replace(" ", "_");

                return ($"{name}_{version}.zip", null);
            }

            return (null, ErrorCode.FileNotFound);
        }

        /// <summary>
        /// Gets or sets the package version.
        /// </summary>
        /// <param name="packageXml">The package XML.</param>
        /// <param name="version">The version.</param>
        /// <returns>
        /// The version.
        /// </returns>
        private static string GetOrSetPackageVersion(XElement packageXml, string version)
        {
            if (!string.IsNullOrWhiteSpace(version))
            {
                var packageNode = packageXml.Element("info")?.Element("package");
                if (packageNode != null)
                {
                    var versionNode = packageNode.Element("version");
                    if (versionNode == null)
                    {
                        versionNode = new XElement("version");
                        packageNode.Add(versionNode);
                    }

                    versionNode.Value = version;
                }

                return version;
            }
            else
            {
                return packageXml?.Element("info")?.Element("package")?.Element("version")?.Value;
            }
        }

        /// <summary>
        /// Adds all the files in one folder (and sub folders) to the package directory.
        /// </summary>
        /// <param name="sourceFolder">The source folder.</param>
        /// <param name="destinationFolder">The destination folder.</param>
        /// <param name="prefix">The prefix.</param>
        private static void AddFilesFromFolders(string sourceFolder, string destinationFolder, string prefix = "")
        {
            Console.WriteLine(Resources.Pack_AddingFolder, sourceFolder);

            foreach (var file in Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories))
            {
                var relative = file.Substring(sourceFolder.Length + 1);

                var destination = Path.Combine(prefix, destinationFolder, relative);

                Console.WriteLine(Resources.Pack_AddingSingle, relative, destinationFolder);

                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination);
            }
        }

        /// <summary>
        /// Adds a single file to the package folder.
        /// </summary>
        /// <param name="sourceFile">The source file.</param>
        /// <param name="destinationFile">The destination file.</param>
        /// <param name="prefix">The prefix.</param>
        private static void AddFile(string sourceFile, string destinationFile, string prefix = "")
        {
            var destination = Path.Combine(prefix, destinationFile);
            Console.WriteLine(Resources.Pack_AddingSingle, sourceFile, destinationFile);

            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(sourceFile, destination);
        }

        /// <summary>
        /// Adds the files based on package XML.
        /// </summary>
        /// <param name="package">The package.</param>
        /// <param name="tempFolder">The temporary folder.</param>
        private static void AddFilesBasedOnPackageXml(XElement package, string tempFolder)
        {
            var fileNodes = package.Elements("files");

            foreach (var node in fileNodes.Elements())
            {
                var (path, orgPath) = GetPathAndOrgPath(node);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    switch (node.Name.LocalName)
                    {
                        case "file":
                            AddFile(path, orgPath, tempFolder);
                            break;
                        case "folder":
                            AddFilesFromFolders(path, orgPath, tempFolder);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the path and original path.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns>
        /// The path and original path.
        /// </returns>
        private static (string path, string orgPath) GetPathAndOrgPath(XElement node)
        {
            var orgPath = node.Attribute("orgPath")?.Value;
            var path = node.Attribute("path")?.Value;

            if (string.IsNullOrWhiteSpace(orgPath)) orgPath = "";

            // remove leading and trialing slashes from anything we have.
            orgPath = orgPath.Replace("/", "\\").Trim('\\');
            path = path.Replace("/", "\\").Trim('\\');

            return (path, orgPath);
        }

        /// <summary>
        /// Builds the package folder.
        /// </summary>
        /// <param name="package">The package.</param>
        /// <param name="sourceFolder">The source folder.</param>
        /// <param name="flatFolder">The flattended folder.</param>
        private static void BuildPackageFolder(XElement package, string sourceFolder, string flatFolder)
        {
            var filesNode = package.Element("files");

            // clean out any child nodes we might already have
            filesNode.RemoveNodes();

            foreach (var file in Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories))
            {
                var guid = Path.GetFileName(file);

                if (guid.Equals("package.xml", StringComparison.InvariantCultureIgnoreCase)) continue;

                var orgPath = Path.GetDirectoryName(file);
                var orgName = guid;

                if (orgPath.Length > sourceFolder.Length)
                {
                    orgPath = orgPath.Substring(sourceFolder.Length)
                        .Replace("\\", "/");
                }

                var dest = Path.Combine(flatFolder, guid);
                if (File.Exists(dest))
                {
                    guid = $"{Guid.NewGuid()}_{guid}";
                    dest = Path.Combine(flatFolder, guid);
                }

                filesNode.Add(new XElement("file",
                    new XElement("guid", guid),
                    new XElement("orgPath", orgPath),
                    new XElement("orgName", orgName)));

                File.Copy(file, dest);
            }

            package.Save(Path.Combine(flatFolder, "package.xml"));
        }

        /// <summary>
        /// Creates the ZIP file.
        /// </summary>
        /// <param name="folder">The folder.</param>
        /// <param name="zipFileName">Name of the ZIP file.</param>
        private static void CreateZip(string folder, string zipFileName)
        {
            if (Directory.Exists(folder))
            {
                if (File.Exists(zipFileName))
                    File.Delete(zipFileName);

                Console.WriteLine(Resources.Pack_SavingPackage, zipFileName);

                ZipFile.CreateFromDirectory(folder, zipFileName);
            }
            else
            {
                Console.WriteLine(Resources.Pack_DirectoryMissing, folder);
            }
        }
    }
}
