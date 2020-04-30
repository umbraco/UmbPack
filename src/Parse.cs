using System;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using Umbraco.Packager.CI.Properties;

namespace Umbraco.Packager.CI
{
    public static class Parse
    {
        public static PackageInfo PackageXml(string packagePath)
         {
            var packageDetails = new PackageInfo();

            using (var archive = ZipFile.OpenRead(packagePath))
            {
                var packageXmlFileExists = archive.Entries.Any(x => string.Equals(x.Name, "package.xml", StringComparison.InvariantCultureIgnoreCase));
                if (packageXmlFileExists)
                {
                    var xmlStream = archive.GetEntry("package.xml").Open();

                    var doc = new XmlDocument();
                    doc.Load(xmlStream);

                    // Do some validation - check if //umbPackage/info/package exists
                    // Throw error if not valid package.xml schema
                    var packageInfo = doc.SelectSingleNode("//umbPackage/info/package");

                    if(packageInfo == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine(Resources.Push_InvalidXml);
                        Console.Error.WriteLine(Resources.Push_MissingPackageNode);
                        Console.ResetColor();

                        // ERROR_INVALID_FUNCTION
                        Environment.Exit(1);
                    }

                    var packageName = packageInfo.SelectSingleNode("//name").InnerText;
                    var packageVersion = packageInfo.SelectSingleNode("//version").InnerText;

                    packageDetails.Name = packageName;
                    packageDetails.VersionString = packageVersion;

                    
                }
            }

            Console.WriteLine(Resources.Push_Extracting);
            Console.WriteLine($"Name: {packageDetails.Name}");
            Console.WriteLine($"Version: {packageDetails.VersionString}\n");
            
            return packageDetails;
        }
    }

    public class PackageInfo
    {
        public string Name { get; set; }

        public string VersionString { get; set; }
    }
}
