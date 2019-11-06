using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Umbraco.Packager.CI
{
    public static class Verify
    {
        public static void FilePath(string packagePath)
        {
            if (File.Exists(packagePath) == false)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Cannot find file {packagePath}");
                Console.ResetColor();

                // ERROR_FILE_NOT_FOUND=2
                Environment.Exit(2);
            }
        }

        public static void IsZip(string packagePath)
        {
            if (Path.GetExtension(packagePath).ToLowerInvariant() != ".zip")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Umbraco package file '{packagePath}' must be a .zip");
                Console.ResetColor();

                // ERROR_INVALID_NAME=123
                // The filename, directory name, or volume label syntax is incorrect.
                Environment.Exit(123);
            }
        }

        public static void ContainsPackageXml(string packagePath)
        {
            using (var archive = ZipFile.OpenRead(packagePath))
            {
                var packageXmlFileExists = archive.Entries.Any(x => string.Equals(x.Name, "package.xml", StringComparison.InvariantCultureIgnoreCase));
                if (packageXmlFileExists == false)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"Umbraco package file '{packagePath}' does not contain a package.xml file");
                    Console.ResetColor();

                    // ERROR_BAD_FILE_TYPE=222
                    Environment.Exit(222);
                }
            }
        }

        public static void ApiKeyIsValid(string apiKey)
        {
           // WebClient
           // HttpClient
           // HttpWebRequest
           // RestSharp

            // https://our.umbraco.com/api/verify?key=SomeJWT

            // Could get network error or our.umb down

            // API will return 403 if not valid
            // OR 200 OK with JSON of the zip files uploaded for this package

            //throw new NotImplementedException();
        }
    }
}
