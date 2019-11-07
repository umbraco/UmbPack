﻿using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

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

        public static async Task ApiKeyIsValid(string apiKey)
        {
            try
            {
                using(var httpClient = new HttpClient())
                {
                    // Config HTTPClient
                    //httpClient.BaseAddress = new Uri("http://our.umbraco.local");
                    httpClient.BaseAddress = new Uri("http://localhost:24292");

                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // The JWT token contains a project ID/key - hence no querystring ?id=3256
                    var httpResponse = await httpClient.GetAsync("/Umbraco/Api/ProjectUpload/GetProjectFiles");
                    if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine("API Key is invalid");
                        Console.ResetColor();

                        // ERROR_ACCESS_DENIED
                        Environment.Exit(5);
                    }
                    else if (httpResponse.IsSuccessStatusCode)
                    {
                        // Get the JSON string content which gives us a list
                        // of current Umbraco Package .zips for this project
                        var apiReponse = await httpResponse.Content.ReadAsStringAsync();
                        //Console.WriteLine(apiReponse);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                // Could get network error or our.umb down
                throw;
            }
        }
    }
}