using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Instantly.Runners.OpenApiClient.Utils.Abstract;

public interface ICloudflareFileDownloader
{
    /// <summary>
    /// Fetches a text-based file (e.g. JSON/YAML) from a Cloudflare-protected URL using Playwright.
    /// </summary>
    /// <param name="url">The direct URL to the file (e.g., OpenAPI JSON)</param>
    /// <param name="timeoutMs">Timeout in milliseconds (default 60 seconds)</param>
    /// <param name="cancellationToken"></param>
    /// <returns>The raw content of the file as a string</returns>
    [Pure]
    ValueTask<string?> DownloadTextFile(string url, int timeoutMs = 60000, CancellationToken cancellationToken = default);
}