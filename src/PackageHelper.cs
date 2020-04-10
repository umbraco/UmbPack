using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Packager.CI.Auth;
using Umbraco.Packager.CI.Extensions;
using Umbraco.Packager.CI.Properties;

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
                WriteError(Resources.Push_MissingFile, packagePath);
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
                WriteError(Resources.Push_FileNotZip, packagePath);
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
                    WriteError(Resources.Push_NoPackageXml, packagePath);
                   
                    Environment.Exit(222); // ERROR_BAD_FILE_TYPE=222
                }
            }
        }

        /// <summary>
        ///  returns an array of existing package files.
        /// </summary>
        public async Task<JArray> GetPackageList(ApiKeyModel keyParts)
        {
            var url = "Umbraco/Api/ProjectUpload/GetProjectFiles";
            try
            {
                using (var httpClient = GetClientBase(url, keyParts.Token, keyParts.MemberId, keyParts.ProjectId))
                {
                    var httpResponse = await httpClient.GetAsync(url);
                    
                    if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        WriteError(Resources.Push_ApiKeyInvalid);
                        Environment.Exit(5); // ERROR_ACCESS_DENIED
                    }
                    else if (httpResponse.IsSuccessStatusCode)
                    {
                        // Get the JSON string content which gives us a list
                        // of current Umbraco Package .zips for this project
                        var apiResponse = await httpResponse.Content.ReadAsStringAsync();
                        return JsonConvert.DeserializeObject<JArray>(apiResponse);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                throw ex;
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
                    WriteError(Resources.Push_PackageExists, packageFileName);
                    Environment.Exit(80); // FILE_EXISTS
                }
            }
        }

        /// <summary>
        ///  change the colour of the console, write an error and reset the colour back.
        /// </summary>
        public void WriteError(string error, params object[] parameters)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(error, parameters);
            Console.ResetColor();
        }

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
        ///  basic http client with Bearer token setup.
        /// </summary>
        public HttpClient GetClientBase(string url, string apiKey, int memberId, int projectId)
        {
            var baseUrl = AuthConstants.BaseUrl;
            var client = new HttpClient {BaseAddress = new Uri(baseUrl)};
            
            var requestPath = new Uri(client.BaseAddress + url).CleanPathAndQuery();
            var timestamp = DateTime.UtcNow;
            var nonce = Guid.NewGuid();

            var signature = HMACAuthentication.GetSignature(requestPath, timestamp, nonce, apiKey);
            var headerToken = HMACAuthentication.GenerateAuthorizationHeader(signature, nonce, timestamp);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", headerToken);
            client.DefaultRequestHeaders.Add(AuthConstants.MemberIdHeader, memberId.ToInvariantString());
            client.DefaultRequestHeaders.Add(AuthConstants.ProjectIdHeader, projectId.ToInvariantString());

            return client;
        }
    }

    public class ApiKeyModel
    {
        public string Token { get; set; }
        public int ProjectId { get; set; }
        public int MemberId { get; set; }
    }
}
