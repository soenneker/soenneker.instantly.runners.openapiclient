using Microsoft.Extensions.Logging;
using Soenneker.Extensions.String;
using Soenneker.Git.Util.Abstract;
using Soenneker.Instantly.Runners.OpenApiClient.Utils.Abstract;
using Soenneker.Utils.Dotnet.Abstract;
using Soenneker.Utils.Environment;
using Soenneker.Utils.Process.Abstract;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Cloudflare.Downloader.Abstract;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.File.Abstract;
using Soenneker.Utils.FileSync.Abstract;

namespace Soenneker.Instantly.Runners.OpenApiClient.Utils;

///<inheritdoc cref="IFileOperationsUtil"/>
public sealed class FileOperationsUtil : IFileOperationsUtil
{
    private readonly ILogger<FileOperationsUtil> _logger;
    private readonly IGitUtil _gitUtil;
    private readonly IDotnetUtil _dotnetUtil;
    private readonly IProcessUtil _processUtil;
    private readonly IOpenApiFixer _openApiFixer;
    private readonly IFileUtilSync _fileUtilSync;
    private readonly IFileUtil _fileUtil;
    private readonly ICloudflareDownloader _cloudflareDownloader;

    public FileOperationsUtil(ILogger<FileOperationsUtil> logger, IGitUtil gitUtil, IDotnetUtil dotnetUtil, IProcessUtil processUtil,
        IOpenApiFixer openApiFixer, IFileUtilSync fileUtilSync, IFileUtil fileUtil, ICloudflareDownloader cloudflareDownloader)
    {
        _logger = logger;
        _gitUtil = gitUtil;
        _dotnetUtil = dotnetUtil;
        _processUtil = processUtil;
        _openApiFixer = openApiFixer;
        _fileUtilSync = fileUtilSync;
        _fileUtil = fileUtil;
        _cloudflareDownloader = cloudflareDownloader;
    }

    public async ValueTask Process(CancellationToken cancellationToken = default)
    {
        string gitDirectory = _gitUtil.CloneToTempDirectory($"https://github.com/soenneker/{Constants.Library.ToLowerInvariantFast()}");

        string targetFilePath = Path.Combine(gitDirectory, "api_v2.json");

        _fileUtilSync.DeleteIfExists(targetFilePath);

        string? result = await _cloudflareDownloader.GetPageContent("https://api.instantly.ai/openapi/api_v2.json", cancellationToken: cancellationToken);

        if (result == null)
            throw new Exception("Failed to download OpenAPI spec from Instantly API");

        await _fileUtil.Write(targetFilePath, result, true, cancellationToken).NoSync();

        await _processUtil.Start("dotnet", null, "tool update --global Microsoft.OpenApi.Kiota", waitForExit: true, cancellationToken: cancellationToken);

       // string fixedFilePath = Path.Combine(gitDirectory, "fixed.json");

        //await _openApiFixer.Fix(filePath, fixedFilePath, cancellationToken).NoSync();

        string srcDirectory = Path.Combine(gitDirectory, "src");

        DeleteAllExceptCsproj(srcDirectory);

        await _processUtil.Start("kiota", gitDirectory, $"kiota generate -l CSharp -d \"{targetFilePath}\" -o src -c InstantlyOpenApiClient -n {Constants.Library}",
            waitForExit: true, cancellationToken: cancellationToken).NoSync();

        await BuildAndPush(gitDirectory, cancellationToken).NoSync();
    }

    public void DeleteAllExceptCsproj(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            _logger.LogWarning("Directory does not exist: {DirectoryPath}", directoryPath);
            return;
        }

        try
        {
            // Delete all files except .csproj
            foreach (string file in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Delete(file);
                        _logger.LogInformation("Deleted file: {FilePath}", file);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete file: {FilePath}", file);
                    }
                }
            }

            // Delete all empty subdirectories
            foreach (string dir in Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories)
                                            .OrderByDescending(d => d.Length)) // Sort by depth to delete from deepest first
            {
                try
                {
                    if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir, recursive: false);
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

        await _gitUtil.CommitAndPush(gitDirectory, "soenneker", "Jake Soenneker", "jake@soenneker.com", gitHubToken, "Automated update", cancellationToken);
    }
}
