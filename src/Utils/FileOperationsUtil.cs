using Microsoft.Extensions.Logging;
using Soenneker.Extensions.String;
using Soenneker.Git.Util.Abstract;
using Soenneker.Instantly.Runners.OpenApiClient.Utils.Abstract;
using Soenneker.Utils.Dotnet.Abstract;
using Soenneker.Utils.Environment;
using Soenneker.Utils.Process.Abstract;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Cloudflare.Downloader.Abstract;
using Soenneker.Extensions.Task;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.Directory.Abstract;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.Json;

namespace Soenneker.Instantly.Runners.OpenApiClient.Utils;

///<inheritdoc cref="IFileOperationsUtil"/>
public sealed class FileOperationsUtil : IFileOperationsUtil
{
    private readonly ILogger<FileOperationsUtil> _logger;
    private readonly IGitUtil _gitUtil;
    private readonly IDotnetUtil _dotnetUtil;
    private readonly IProcessUtil _processUtil;
    private readonly IFileUtil _fileUtil;
    private readonly IDirectoryUtil _directoryUtil;
    private readonly ICloudflareDownloader _cloudflareDownloader;
    public const string ExampleGuid = "f9cc070d-8dba-4341-b847-f083c358e460";

    public FileOperationsUtil(ILogger<FileOperationsUtil> logger, IGitUtil gitUtil, IDotnetUtil dotnetUtil, IProcessUtil processUtil, IFileUtil fileUtil,
        IDirectoryUtil directoryUtil, ICloudflareDownloader cloudflareDownloader)
    {
        _logger = logger;
        _gitUtil = gitUtil;
        _dotnetUtil = dotnetUtil;
        _processUtil = processUtil;
        _fileUtil = fileUtil;
        _directoryUtil = directoryUtil;
        _cloudflareDownloader = cloudflareDownloader;
    }

    public async ValueTask Process(CancellationToken cancellationToken = default)
    {
        string gitDirectory = await _gitUtil.CloneToTempDirectory($"https://github.com/soenneker/{Constants.Library.ToLowerInvariantFast()}",
            cancellationToken: cancellationToken);

        string targetFilePath = Path.Combine(gitDirectory, "api_v2.json");

        await _fileUtil.DeleteIfExists(targetFilePath, cancellationToken: cancellationToken);

        string? result = await _cloudflareDownloader.GetPageContent("https://api.instantly.ai/openapi/api_v2.json", cancellationToken: cancellationToken);

        if (result == null)
            throw new Exception("Failed to download OpenAPI spec from Instantly API");

        string formatted = JsonUtil.Format(result, false);

        await _fileUtil.Write(targetFilePath, formatted, true, cancellationToken).NoSync();

        ExampleStringNormalizer normalizer = new ExampleStringNormalizer(_fileUtil)
                                             .AddRule(
                                                 s => DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                                                     DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _), "1970-01-01T00:00:00.000Z")

                                             // 2) GUIDs / UUIDs ------------------------------------------------------
                                             .AddRule(s => Guid.TryParse(s, out _), ExampleGuid)
                                             .AddRule(s => Regex.IsMatch(s, @"^[0-9a-f]{24}$", RegexOptions.IgnoreCase), "000000000000000000000000")

                                             // 3) PTID tokens --------------------------------------------------------
                                             .AddRule(s => Regex.IsMatch(s, "^ptid_[A-Za-z0-9_-]{10,}$"), "ptid_y8ujsCRs9972UH_LfKf3H");

        await normalizer.Normalize(targetFilePath, cancellationToken).NoSync();

        await _processUtil.Start("dotnet", null, "tool update --global Microsoft.OpenApi.Kiota", waitForExit: true, cancellationToken: cancellationToken);

        string srcDirectory = Path.Combine(gitDirectory, "src");

        await DeleteAllExceptCsproj(srcDirectory, cancellationToken);

        await _processUtil.Start("kiota", gitDirectory,
                              $"kiota generate -l CSharp -d \"{targetFilePath}\" -o src -c InstantlyOpenApiClient -n {Constants.Library} --ebc --cc", waitForExit: true,
                              cancellationToken: cancellationToken)
                          .NoSync();

        await BuildAndPush(gitDirectory, cancellationToken).NoSync();
    }

    public async ValueTask DeleteAllExceptCsproj(string directoryPath, CancellationToken cancellationToken = default)
    {
        if (!(await _directoryUtil.Exists(directoryPath, cancellationToken)))
        {
            _logger.LogWarning("Directory does not exist: {DirectoryPath}", directoryPath);
            return;
        }

        try
        {
            // Delete all files except .csproj
            List<string> files = await _directoryUtil.GetFilesByExtension(directoryPath, "", true, cancellationToken);
            foreach (string file in files)
            {
                if (!file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        await _fileUtil.Delete(file, ignoreMissing: true, log: false, cancellationToken);
                        _logger.LogInformation("Deleted file: {FilePath}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete file: {FilePath}", file);
                    }
                }
            }

            // Delete all empty subdirectories
            List<string> dirs = await _directoryUtil.GetAllDirectoriesRecursively(directoryPath, cancellationToken);
            foreach (string dir in dirs.OrderByDescending(d => d.Length)) // Sort by depth to delete from deepest first
            {
                try
                {
                    List<string> dirFiles = await _directoryUtil.GetFilesByExtension(dir, "", false, cancellationToken);
                    List<string> subDirs = await _directoryUtil.GetAllDirectories(dir, cancellationToken);
                    if (dirFiles.Count == 0 && subDirs.Count == 0)
                    {
                        await _directoryUtil.Delete(dir, cancellationToken);
                        _logger.LogInformation("Deleted empty directory: {DirectoryPath}", dir);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete directory: {DirectoryPath}", dir);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while cleaning the directory: {DirectoryPath}", directoryPath);
        }
    }

    private async ValueTask BuildAndPush(string gitDirectory, CancellationToken cancellationToken)
    {
        string projFilePath = Path.Combine(gitDirectory, "src", $"{Constants.Library}.csproj");

        await _dotnetUtil.Restore(projFilePath, cancellationToken: cancellationToken);

        bool successful = await _dotnetUtil.Build(projFilePath, true, "Release", false, cancellationToken: cancellationToken);

        if (!successful)
        {
            _logger.LogError("Build was not successful, exiting...");
            return;
        }

        string gitHubToken = EnvironmentUtil.GetVariableStrict("GH__TOKEN");
        string name = EnvironmentUtil.GetVariableStrict("GIT__NAME");
        string email = EnvironmentUtil.GetVariableStrict("GIT__EMAIL");

        await _gitUtil.CommitAndPush(gitDirectory, "Automated update", gitHubToken, name, email, cancellationToken);
    }
}