using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Soenneker.Extensions.ValueTask;
using Soenneker.Instantly.Runners.OpenApiClient.Utils.Abstract;
using Soenneker.Utils.File.Abstract;

namespace Soenneker.Instantly.Runners.OpenApiClient.Utils;

public class ExampleTimestampNormalizer : IExampleTimestampNormalizer
{
    private readonly IFileUtil _fileUtil;
    private readonly ILogger<ExampleTimestampNormalizer> _logger;

    public ExampleTimestampNormalizer(IFileUtil fileUtil, ILogger<ExampleTimestampNormalizer> logger)
    {
        _fileUtil = fileUtil;
        _logger = logger;
    }

    public async ValueTask<int> Normalize(string jsonPath, DateTime newTimestampUtc, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Normalizing example timestamps in {JsonPath} to {NewTimestampUtc}", jsonPath, newTimestampUtc);

        // 1) load
        string json = await _fileUtil.Read(jsonPath, cancellationToken: cancellationToken).NoSync();
        JsonNode root = JsonNode.Parse(json)!;

        // 2) fixed instant once
        string replacement = newTimestampUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        int replaced = 0;
        Process(root, currentKey: null, parentKey: null, replacement, ref replaced);

        // 3) write back
        var opts = new JsonSerializerOptions {WriteIndented = true};
        await _fileUtil.Write(jsonPath, root.ToJsonString(opts), cancellationToken: cancellationToken).NoSync();

        return replaced;
    }

    /* ------------------------------------------------------------------ */

    private static void Process(JsonNode? node, string? currentKey, // the property‑name of <node> under its parent, or null for array/root
        string? parentKey, // the property‑name of the parent object
        string replacement, ref int replaced)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, child) in obj)
                    Process(child, key, currentKey, replacement, ref replaced);
                break;

            case JsonArray arr:
                foreach (var child in arr)
                    Process(child, currentKey: null, parentKey: currentKey, replacement, ref replaced);
                break;

            case JsonValue val when val.TryGetValue<string>(out var s):
                if (!ShouldReplace(currentKey, parentKey, s)) return;

                if (DateTimeOffset.TryParse(s.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _))
                {
                    val.ReplaceWith(JsonValue.Create(replacement));
                    replaced++;
                }

                break;
        }
    }

    /// Only replace when the path looks like …example or …examples.*.value
    private static bool ShouldReplace(string? key, string? parentKey, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (key.Equals("example", StringComparison.OrdinalIgnoreCase))
            return true;

        return key.Equals("value", StringComparison.OrdinalIgnoreCase) && parentKey != null && parentKey.Equals("examples", StringComparison.OrdinalIgnoreCase);
    }
}