using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Umbraco.Packager.CI
{
    public class PackageHelper
    {
        /// <summary>
        ///  verify that the package file exists at the specified location
        /// </summary>
        public void EnsurePackageExists(string packagePath)
        {
            if (File.Exists(packagePath) == false)
            {
                WriteError($"Cannot find file {packagePath}");
                Environment.Exit(2); // ERROR_FILE_NOT_FOUND=2
            }
        }

        /// <summary>
        ///  confirm the package file a zip.
        /// </summary>
        public void EnsureIsZip(string packagePath)
        {
            if (Path.GetExtension(packagePath).ToLowerInvariant() != ".zip")
            {
                WriteError($"Umbraco package file '{packagePath}' must be a .zip");
                Environment.Exit(123);  // ERROR_INVALID_NAME=123
            }
        }

        /// <summary>
        ///  confirm that the zip file contains a package.xml file.
        /// </summary>
        public void EnsureContainsPackageXml(string packagePath)
        {
            using (var archive = ZipFile.OpenRead(packagePath))
            {
                var packageXmlFileExists = archive.Entries.Any(x => string.Equals(x.Name, "package.xml", StringComparison.InvariantCultureIgnoreCase));
                if (packageXmlFileExists == false)
                {
                    WriteError($"Umbraco package file '{packagePath}' does not contain a package.xml file");
                    
                    Environment.Exit(222); // ERROR_BAD_FILE_TYPE=222
                }
            }
        }

        /// <summary>
        ///  returns an array of existing package files.
        /// </summary>
        public async Task<JArray> GetPackageList(string apiKey)
        {
            try
            {
                using (var httpClient = GetClientBase(apiKey))
                {
                    // The JWT token contains a project ID/key - hence no querystring ?id=3256
                    var httpResponse = await httpClient.GetAsync("/Umbraco/Api/ProjectUpload/GetProjectFiles");
                    
                    if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        WriteError("API Key is invalid");
                        Environment.Exit(5); // ERROR_ACCESS_DENIED
                    }
                    else if (httpResponse.IsSuccessStatusCode)
                    {
                        // Get the JSON string content which gives us a list
                        // of current Umbraco Package .zips for this project
                        var apiReponse = await httpResponse.Content.ReadAsStringAsync();
                        return JsonConvert.DeserializeObject<JArray>(apiReponse);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                throw;
            }

            return null;
        }

        public void EnsurePackageDoesntAlreadyExists(JArray packages, string packageFile)
        {
            if (packages == null) return;

            var packageFileName = Path.GetFileName(packageFile);

            foreach(var package in packages)
            {
                var packageName = package.Value<string>("Name"); 
                if (packageName.Equals(packageFileName, StringComparison.InvariantCultureIgnoreCase))
                {
                    WriteError($"A package file named '{packageFileName}' already exists for this package");
                    Environment.Exit(80); // FILE_EXISTS
                }
            }
        }

        /// <summary>
        ///  change the colour of the console, write an error and reset the colour back.
        /// </summary>
        public void WriteError(string error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(error);
            Console.ResetColor();
        }

        /// <summary>
        ///  basic http client with Bearer token setup.
        /// </summary>
        public HttpClient GetClientBase(string apiKey)
        {
            var client = new HttpClient();

            client.BaseAddress = new Uri("http://localhost:24292");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
    }
}
