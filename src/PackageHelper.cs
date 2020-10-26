using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UmbPack.Auth;
using UmbPack.Extensions;
using UmbPack.Properties;

namespace UmbPack
{
    internal class PackageHelper
    {
        /// <summary>
        /// The HTTP client factory.
        /// </summary>
        private readonly IHttpClientFactory httpClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageHelper" /> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        public PackageHelper(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Verify that the package file exists at the specified location.
        /// </summary>
        /// <param name="packagePath">The package path.</param>
        /// <returns>
        /// The error code.
        /// </returns>
        public ErrorCode? EnsurePackageExists(string packagePath)
        {
            if (File.Exists(packagePath) == false)
            {
                WriteError(Resources.Push_MissingFile, packagePath);

                return ErrorCode.FileNotFound;
            }

            return null;
        }

        /// <summary>
        /// Verify that the package file is a ZIP file.
        /// </summary>
        /// <param name="packagePath">The package path.</param>
        /// <returns>
        /// The error code.
        /// </returns>
        public ErrorCode? EnsureIsZip(string packagePath)
        {
            if (Path.GetExtension(packagePath).ToLowerInvariant() != ".zip")
            {
                WriteError(Resources.Push_FileNotZip, packagePath);

                return ErrorCode.InvalidName;
            }

            return null;
        }

        /// <summary>
        /// Verify that the ZIP file contains a package.xml file.
        /// </summary>
        /// <param name="packagePath">The package path.</param>
        /// <returns>
        /// The error code.
        /// </returns>
        public ErrorCode? EnsureContainsPackageXml(string packagePath)
        {
            using (var archive = ZipFile.OpenRead(packagePath))
            {
                var packageXmlFileExists = archive.Entries.Any(x => string.Equals(x.Name, "package.xml", StringComparison.InvariantCultureIgnoreCase));
                if (packageXmlFileExists == false)
                {
                    WriteError(Resources.Push_NoPackageXml, packagePath);

                    return ErrorCode.BadFileType;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns an array of existing package files.
        /// </summary>
        /// <param name="keyParts">The key parts.</param>
        /// <returns>
        /// The package files and/or error code.
        /// </returns>
        public async Task<(JArray, ErrorCode?)> GetPackageList(ApiKeyModel keyParts)
        {
            var url = "Umbraco/Api/ProjectUpload/GetProjectFiles";
            using var httpClient = GetClientBase(url, keyParts.Token, keyParts.MemberId, keyParts.ProjectId);
            var httpResponse = await httpClient.GetAsync(url);

            if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                WriteError(Resources.Push_ApiKeyInvalid);

                return (null, ErrorCode.AccessDenied);
            }
            else if (httpResponse.IsSuccessStatusCode)
            {
                // Get the JSON string content which gives us a list
                // of current Umbraco Package .zips for this project
                var apiResponse = await httpResponse.Content.ReadAsStringAsync();

                return (JsonConvert.DeserializeObject<JArray>(apiResponse), null);
            }

            return (null, null);
        }

        /// <summary>
        /// Ensures the package doesn't already exists.
        /// </summary>
        /// <param name="packages">The packages.</param>
        /// <param name="packageFile">The package file.</param>
        /// <returns>
        /// The error code.
        /// </returns>
        public ErrorCode? EnsurePackageDoesntAlreadyExists(JArray packages, string packageFile)
        {
            if (packages != null)
            {
                var packageFileName = Path.GetFileName(packageFile);
                foreach (var package in packages)
                {
                    var packageName = package.Value<string>("Name");
                    if (packageName.Equals(packageFileName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        WriteError(Resources.Push_PackageExists, packageFileName);

                        return ErrorCode.FileExists;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Archives the packages.
        /// </summary>
        /// <param name="keyParts">The key parts.</param>
        /// <param name="ids">The ids.</param>
        /// <returns>
        /// The error code.
        /// </returns>
        public async Task<ErrorCode?> ArchivePackages(ApiKeyModel keyParts, IEnumerable<int> ids)
        {
            var url = "Umbraco/Api/ProjectUpload/ArchiveProjectFiles";
            using var httpClient = GetClientBase(url, keyParts.Token, keyParts.MemberId, keyParts.ProjectId);
            var httpResponse = await httpClient.PostAsJsonAsync(url, ids);

            if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                WriteError(Resources.Push_ApiKeyInvalid);

                return ErrorCode.AccessDenied;
            }

            return null;
        }

        /// <summary>
        /// Gets the current package file identifier.
        /// </summary>
        /// <param name="keyParts">The key parts.</param>
        /// <returns>
        /// The current package file identifier and/or error code.
        /// </returns>
        public async Task<(string, ErrorCode?)> GetCurrentPackageFileId(ApiKeyModel keyParts)
        {
            var url = "Umbraco/Api/ProjectUpload/GetCurrentPackageFileId";
            using var httpClient = GetClientBase(url, keyParts.Token, keyParts.MemberId, keyParts.ProjectId);
            var httpResponse = await httpClient.GetAsync(url);

            if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                WriteError(Resources.Push_ApiKeyInvalid);

                return (null, ErrorCode.AccessDenied);
            }

            var apiResponse = await httpResponse.Content.ReadAsStringAsync();

            return (apiResponse, null);
        }

        /// <summary>
        /// Change the colour of the console, write an error and reset the colour back.
        /// </summary>
        /// <param name="error">The error.</param>
        /// <param name="parameters">The parameters.</param>
        public void WriteError(string error, params object[] parameters)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(error, parameters);
            Console.ResetColor();
        }

        /// <summary>
        /// The API key has the format "packageId-memberId-apiToken", this helper method splits it in the three parts and returns a model with them all.
        /// </summary>
        /// <param name="apiKey">The API key.</param>
        /// <returns>
        /// The API key model.
        /// </returns>
        public ApiKeyModel SplitKey(string apiKey)
        {
            var keyParts = apiKey.Split('-');
            var keyModel = new ApiKeyModel();

            if (int.TryParse(keyParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int projectId))
            {
                keyModel.ProjectId = projectId;
            }

            if (int.TryParse(keyParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int memberId))
            {
                keyModel.MemberId = memberId;
            }

            keyModel.Token = keyParts[2];

            return keyModel;
        }

        /// <summary>
        /// Basic HTTP client with Bearer token setup.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="apiKey">The API key.</param>
        /// <param name="memberId">The member identifier.</param>
        /// <param name="projectId">The project identifier.</param>
        /// <returns>
        /// The HTTP client.
        /// </returns>
        public HttpClient GetClientBase(string url, string apiKey, int memberId, int projectId)
        {
            var baseUrl = AuthConstants.BaseUrl;
            var client = httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(baseUrl);

            var requestPath = new Uri(client.BaseAddress + url).CleanPathAndQuery();
            var timestamp = DateTime.UtcNow;
            var nonce = Guid.NewGuid();

            var signature = HMACAuthentication.GetSignature(requestPath, timestamp, nonce, apiKey);
            var headerToken = HMACAuthentication.GenerateAuthorizationHeader(signature, nonce, timestamp);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", headerToken);
            client.DefaultRequestHeaders.Add(AuthConstants.MemberIdHeader, memberId.ToString());
            client.DefaultRequestHeaders.Add(AuthConstants.ProjectIdHeader, projectId.ToString());

            return client;
        }

        /// <summary>
        /// Parses the package XML.
        /// </summary>
        /// <param name="packagePath">The package path.</param>
        /// <returns>
        /// The parsed package info and/or error code.
        /// </returns>
        public (PackageInfo, ErrorCode?) PackageXml(string packagePath)
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

                    if (packageInfo == null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine(Resources.Push_InvalidXml);
                        Console.Error.WriteLine(Resources.Push_MissingPackageNode);
                        Console.ResetColor();

                        return (null, ErrorCode.InvalidFunction);
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

            return (packageDetails, null);
        }
    }

    /// <summary>
    /// The API key model.
    /// </summary>
    internal class ApiKeyModel
    {
        /// <summary>
        /// Gets or sets the token.
        /// </summary>
        /// <value>
        /// The token.
        /// </value>
        public string Token { get; set; }

        /// <summary>
        /// Gets or sets the project identifier.
        /// </summary>
        /// <value>
        /// The project identifier.
        /// </value>
        public int ProjectId { get; set; }

        /// <summary>
        /// Gets or sets the member identifier.
        /// </summary>
        /// <value>
        /// The member identifier.
        /// </value>
        public int MemberId { get; set; }
    }

    /// <summary>
    /// The package info.
    /// </summary>
    internal class PackageInfo
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the version string.
        /// </summary>
        /// <value>
        /// The version string.
        /// </value>
        public string VersionString { get; set; }
    }
}
