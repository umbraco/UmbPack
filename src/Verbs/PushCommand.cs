using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Handlers;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommandLine;
using Newtonsoft.Json;
using UmbPack.Properties;

namespace UmbPack.Verbs
{
    /// <summary>
    /// Command line options for the Push verb.
    /// </summary>
    [Verb("push", HelpText = "HelpPush", ResourceType = typeof(HelpTextResource))]
    internal class PushOptions
    {
        [Value(0, MetaName = "package.zip", Required = true, HelpText = "HelpPushPackage", ResourceType = typeof(HelpTextResource))]
        public string Package { get; set; }

        [Option('k', "Key", HelpText = "HelpPushKey", ResourceType = typeof(HelpTextResource))]
        public string ApiKey { get; set; }

        [Option('c', "Current", Default = "true", HelpText = "HelpPushCurrent", ResourceType = typeof(HelpTextResource))]
        public string Current { get; set; }

        [Option("DotNetVersion", Default = "4.7.2", HelpText = "HelpPushDotNet", ResourceType = typeof(HelpTextResource))]
        public string DotNetVersion { get; set; }

        [Option('w', "WorksWith", Default = "v850", HelpText = "HelpPushWorks", ResourceType = typeof(HelpTextResource))]
        public string WorksWith { get; set; }

        [Option('a', "Archive", Separator = ' ', HelpText = "HelpPushArchive", ResourceType = typeof(HelpTextResource))]
        public IEnumerable<string> Archive { get; set; }
    }

    /// <summary>
    /// Push command, lets you upload a package to Our.
    /// </summary>
    internal static class PushCommand
    {
        /// <summary>
        /// Runs the command and returns the error code.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="packageHelper">The package helper.</param>
        /// <returns>
        /// The error code.
        /// </returns>
        public static async Task<ErrorCode> RunAndReturn(PushOptions options, PackageHelper packageHelper)
        {
            // --package=MyFile.zip
            // --package=./MyFile.zip
            // --package=../MyParentFolder.zip

            var filePath = options.Package;
            var apiKey = options.ApiKey;

            var keyParts = packageHelper.SplitKey(apiKey);

            // Check we can find the file
            if (packageHelper.EnsurePackageExists(filePath) is ErrorCode ensurePackageExistsErrorCode)
            {
                return ensurePackageExistsErrorCode;
            }

            // Check file is a ZIP
            if (packageHelper.EnsureIsZip(filePath) is ErrorCode ensureIsZipErrorCode)
            {
                return ensureIsZipErrorCode;
            }

            // Check zip contains valid package.xml
            if (packageHelper.EnsureContainsPackageXml(filePath) is ErrorCode ensureContainsPackageXmlErrorCode)
            {
                return ensureContainsPackageXmlErrorCode;
            }

            // gets a package list from our.umbraco
            // if the api key is invalid we will also find out here.
            var (packages, getPackageListErrorCode) = await packageHelper.GetPackageList(keyParts);
            if (getPackageListErrorCode.HasValue)
            {
                return getPackageListErrorCode.Value;
            }

            var (currentPackageId, getCurrentPackageFileIdErrorCode) = await packageHelper.GetCurrentPackageFileId(keyParts);
            if (getCurrentPackageFileIdErrorCode.HasValue)
            {
                return getCurrentPackageFileIdErrorCode.Value;
            }

            if (packageHelper.EnsurePackageDoesntAlreadyExists(packages, filePath) is ErrorCode ensurePackageDoesntAlreadyExistsErrorCode)
            {
                return ensurePackageDoesntAlreadyExistsErrorCode;
            }

            // Archive packages
            var archivePatterns = new List<string>();
            var packagesToArchive = new List<int>();

            if (options.Archive != null)
            {
                archivePatterns.AddRange(options.Archive);
            }

            if (archivePatterns.Count > 0)
            {
                foreach (var archivePattern in archivePatterns)
                {
                    if (archivePattern == "current")
                    {
                        // If the archive option is "current", then archive the current package
                        if (currentPackageId != "0")
                            packagesToArchive.Add(int.Parse(currentPackageId));
                    }
                    else
                    {
                        // Convert the archive option to a regex
                        var archiveRegex = new Regex("^" + archivePattern.Replace(".", "\\.").Replace("*", "(.*)") + "$", RegexOptions.IgnoreCase);

                        // Find packages that match the regex and extract their IDs
                        var archiveIds = packages.Where(x => archiveRegex.IsMatch(x.Value<string>("Name"))).Select(x => x.Value<int>("Id")).ToArray();

                        packagesToArchive.AddRange(archiveIds);
                    }
                }
            }

            if (packagesToArchive.Count > 0)
            {
                if (await packageHelper.ArchivePackages(keyParts, packagesToArchive.Distinct()) is ErrorCode archivePackagesErrorCode)
                {
                    return archivePackagesErrorCode;
                }

                Console.WriteLine($"Archived {packagesToArchive.Count} packages matching the archive pattern.");
            }

            // Parse package.xml before upload to print out info
            // and to use for comparison on what is already uploaded
            var (packageInfo, packageXmlErrorCode) = packageHelper.PackageXml(filePath);
            if (packageXmlErrorCode.HasValue)
            {
                return packageXmlErrorCode.Value;
            }

            // OK all checks passed - time to upload it
            if (await UploadPackage(options, packageHelper, packageInfo) is ErrorCode uploadPackageErrorCode)
            {
                return uploadPackageErrorCode;
            }

            return ErrorCode.Success;
        }

        /// <summary>
        /// Uploads the package.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="packageHelper">The package helper.</param>
        /// <param name="packageInfo">The package information.</param>
        /// <returns>
        /// The error code.
        /// </returns>
        private static async Task<ErrorCode?> UploadPackage(PushOptions options, PackageHelper packageHelper, PackageInfo packageInfo)
        {
            try
            {
                // HttpClient will use this event handler to give us
                // Reporting on how its progress the file upload
                var processMsgHandler = new ProgressMessageHandler(new HttpClientHandler());
                processMsgHandler.HttpSendProgress += (sender, e) =>
                {
                    // Could try to reimplement progressbar - but that library did not work in GH Actions :(
                    var percent = e.ProgressPercentage;
                };

                var keyParts = packageHelper.SplitKey(options.ApiKey);
                var packageFileName = Path.GetFileName(options.Package);

                Console.WriteLine(Resources.Push_Uploading, packageFileName);

                var url = "/Umbraco/Api/ProjectUpload/UpdatePackage";

                using (var client = packageHelper.GetClientBase(url, keyParts.Token, keyParts.MemberId, keyParts.ProjectId))
                {
                    MultipartFormDataContent form = new MultipartFormDataContent();
                    var fileInfo = new FileInfo(options.Package);
                    var content = new StreamContent(fileInfo.OpenRead());
                    content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                    {
                        Name = "file",
                        FileName = fileInfo.Name
                    };
                    form.Add(content);
                    form.Add(new StringContent(ParseCurrentFlag(options.Current)), "isCurrent");
                    form.Add(new StringContent(options.DotNetVersion), "dotNetVersion");
                    form.Add(new StringContent("package"), "fileType");
                    form.Add(GetVersionCompatibility(options.WorksWith), "umbracoVersions");
                    form.Add(new StringContent(packageInfo.VersionString), "packageVersion");

                    var httpResponse = await client.PostAsync(url, form);
                    if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        packageHelper.WriteError(Resources.Push_ApiKeyInvalid);

                        return ErrorCode.AccessDenied;
                    }
                    else if (httpResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine(Resources.Push_Complete, packageFileName);
                        
                        // Response is not reported (at the moment)
                        // var apiReponse = await httpResponse.Content.ReadAsStringAsync();
                        // Console.WriteLine(apiReponse);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                // Could get network error or our.umb down
                Console.WriteLine(Resources.Error, ex);

                throw;
            }

            return null;
        }

        /// <summary>
        /// Returns the version compatibility string for uploading the package.
        /// </summary>
        /// <param name="worksWithString">The 'works with' string.</param>
        /// <returns>
        /// The version compatibility string.
        /// </returns>
        private static StringContent GetVersionCompatibility(string worksWithString)
        {
            // TODO Workout how we can get a latest version from our? Maybe accept wild cards (8.* -> 8.5.0,8.4.0,8.3.0) or work like nuget e.g '> 8.4.0'?
            var versions = worksWithString.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(x => new UmbracoVersion() { Version = x });

            return new StringContent(JsonConvert.SerializeObject(versions));
        }

        /// <summary>
        /// Parses the current flag.
        /// </summary>
        /// <param name="current">The current flag.</param>
        /// <returns>
        /// The parsed current flag.
        /// </returns>
        private static string ParseCurrentFlag(string current)
        {
            if (bool.TryParse(current, out bool result))
            {
                return result.ToString();
            }

            return false.ToString();
        }

        /// <summary>
        /// Represents an Umbraco version.
        /// </summary>
        /// <remarks>
        /// Taken from the source of our.umbraco.com.
        /// </remarks>
        private class UmbracoVersion
        {
            /// <summary>
            /// Gets or sets the version.
            /// </summary>
            /// <value>
            /// The version.
            /// </value>
            public string Version { get; set; }

            // We don't need to supply name. but it is in the orginal model.
            // public string Name { get; set; }
        }
    }
}
