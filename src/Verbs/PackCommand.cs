using System;

using CommandLine;

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
            HelpText = "Overrides the version defined in the package.xml file",
            Default = "")]
        public string Version { get; set; }
    }


    internal static class PackCommand
    {
        public static int RunAndReturn(PackOptions options)
        {
            Console.WriteLine(options.FolderOrFile);
            Console.WriteLine(options.OutputDirectory);
            Console.WriteLine(options.Version);
               

            return 0;
        }
    }
}
