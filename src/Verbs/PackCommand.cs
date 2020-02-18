using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Xml.Linq;
using CommandLine;
using Semver;
using Umbraco.Packager.CI.Properties;

namespace Umbraco.Packager.CI.Verbs
{
    [Verb("pack", HelpText = "Create an umbraco package from a folder or package.xml file")]
    public class PackOptions
    {
        [Value(0, MetaName = "file/folder", Required = true,
            HelpText = "package.xml file or folder you want to crate package for")]
        public string FolderOrFile { get; set; }

        [Option("OutputDirectory",
            HelpText = "Specifies the directory for the created umbraco package, If not specified, uses the current directory",
            Default = ".")]
        public string OutputDirectory { get; set; }

        [Option("Version",
            HelpText = "Overrides the version defined in the package.xml file")]
        public string Version { get; set; }
    }


    /// <summary>
    ///  Pack command, lets you create a package.zip either from a package.xml or a folder
    /// </summary>
    /// <remarks>
    ///    you can mark up the package.xml file with a couple of extra things
    ///    and when its processed by this command, to make the full verison
    ///    
    /// Example: 
    /// <![CDATA[
    ///     <files>
    ///         <file path="../path/to/file.dll" orgPath="bin" />
    ///         <folder path="../path/to/folder/App_Plugins" orgPath="App_Plugins" />
    ///     </files>
    /// ]]>
    /// 
    ///  this would copy the file to the bin folder in the package
    ///  and copy the content of the folder to the App_Plugins folder
    ///  the structure of the folder beneath this level is also preserved.
    /// </remarks>
    internal static class PackCommand
    {
        public async static Task<int> RunAndReturn(PackOptions options)
        {
            // make sure the output directory exists
            Directory.CreateDirectory(options.OutputDirectory);

            // working dir, is where we build a structure of what the package will do
            var workingDir = CreateWorkingFolder(options.OutputDirectory, "__umbpack__tmp");

            // buildfolder is the things we zip up.
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
                // a folder - we assume the package.xml is in that folder
                isFolder = true;
                packageFile = Path.Combine(options.FolderOrFile, "package.xml");
                Console.WriteLine("Building package from folder: {0}", options.FolderOrFile);
            }
            else
            {
                Console.WriteLine("Building package from package.xml file contents");
            }

            if (!File.Exists(packageFile))
            {
                Console.WriteLine(Resources.Pack_MissingXml, packageFile);
                Environment.Exit(2);
            }

            Console.WriteLine("Loading Package File: {0}", packageFile);
            Console.WriteLine("----------------------------------------");

            // load the package xml
            var packageXml = XElement.Load(packageFile);

            // stamp the package version.
            var version = GetOrSetPackageVersion(packageXml, options.Version);

            // work out what we are going to call the package
            var packageFileName = GetPackageFileName(options.OutputDirectory, packageXml, version);

            // add any files based on what is already in the package.xml
            AddFilesBasedOnPackageXml(packageXml, workingDir);

            // if the source is a folder, grab all the files from that folder
            if (isFolder) AddFilesFromFolders(options.FolderOrFile, workingDir);

            BuildPackageFolder(packageXml, workingDir, buildFolder);
            Directory.Delete(workingDir, true);

            Console.WriteLine("----------------------------------------");

            CreateZip(buildFolder, packageFileName);
            Directory.Delete(buildFolder, true);

            return 0;
        }

        private static string CreateWorkingFolder(string path, string subFolder = "", bool clean = true) 
        {
            var folder = Path.Combine(path, subFolder);
            
            if (clean && Directory.Exists(folder))
                Directory.Delete(folder, true);

            Directory.CreateDirectory(folder);
            return folder;
        }

        private static string GetPackageFileName(string folder, XElement packageFile, string version)
        {

            var nameNode = packageFile.Element("info")?.Element("package")?.Element("name");
            if (nameNode != null)
            {
                var name = nameNode.Value
                    .Replace(".", "_")
                    .Replace(" ", "_");

                return Path.Combine(folder, $"{name}_{version}.zip");
            }

            Environment.Exit(2);
            return "";
        }

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


        private static void AddFilesFromFolders(string sourceFolder, string dest, string prefix = "")
        {
            Console.WriteLine("Adding Folder: {0}", sourceFolder);

            foreach(var file in Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories))
            {
                var relative = file.Substring(sourceFolder.Length+1);

                var destination = Path.Combine(prefix, dest, relative);

                Console.WriteLine("  File: {0}", Path.Combine(sourceFolder, relative));

                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination);
            }
        }

        private static void AddFile(string sourceFile, string dest, string prefix = "")
        {
            Console.WriteLine("Adding File: {0}", dest);
            var destination = Path.Combine(prefix, dest);
            File.Copy(sourceFile, destination);
        }

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

        private static (string path, string orgPath) GetPathAndOrgPath(XElement node)
        {
            var orgPath = node.Attribute("orgPath")?.Value;
            var path = node.Attribute("path")?.Value;

            if (string.IsNullOrWhiteSpace(orgPath)) orgPath = "";

            orgPath = orgPath.TrimStart('\\').Replace("/", "\\");
            path = path.Replace("/", "\\");

            return (path, orgPath);
        }

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
        private static void CreateZip(string folder, string zipFileName)
        {
            if (Directory.Exists(folder))
            {
                if (File.Exists(zipFileName))
                    File.Delete(zipFileName);

                Console.WriteLine("Saving Package to {0}", zipFileName);

                ZipFile.CreateFromDirectory(folder, zipFileName);
            }
            else
            {
                Console.WriteLine("Build Directory {0} doesn't exist", folder);
            }
        }

    }
}
