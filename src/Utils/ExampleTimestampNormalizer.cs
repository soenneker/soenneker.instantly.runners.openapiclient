using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.ValueTask;
using Soenneker.Instantly.Runners.OpenApiClient.Utils.Abstract;
using Soenneker.Utils.File.Abstract;

namespace Soenneker.Instantly.Runners.OpenApiClient.Utils;

/// <inheritdoc cref="IExampleTimestampNormalizer"/>
public class ExampleTimestampNormalizer : IExampleTimestampNormalizer
{
    private readonly IFileUtil _fileUtil;

    public ExampleTimestampNormalizer(IFileUtil fileUtil) => _fileUtil = fileUtil;

    /// <summary>
    /// Rewrites every string in <paramref name="jsonPath"/> that parses as a UTC date‑time
    /// to <paramref name="newTimestampUtc"/>.  
    /// Returns the number of replacements made so callers can assert success.
    /// </summary>
    public async ValueTask<int> Normalize(string jsonPath, DateTime newTimestampUtc, CancellationToken cancellationToken = default)
    {
        string json = await _fileUtil.Read(jsonPath, cancellationToken: cancellationToken).NoSync();
        JsonNode? root = JsonNode.Parse(json)!; // never null if input is valid JSON

        var replacement = newTimestampUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        var replaced = 0;
        ProcessNode(root, replacement, ref replaced);

        var options = new JsonSerializerOptions {WriteIndented = true};
        await _fileUtil.Write(jsonPath, root.ToJsonString(options), cancellationToken: cancellationToken).NoSync();

        return replaced;
    }


    private static void ProcessNode(JsonNode? node, string replacement, ref int replaced)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach ((string _, JsonNode? child) in obj)
                    ProcessNode(child, replacement, ref replaced);
                break;

            case JsonArray arr:
                foreach (JsonNode? child in arr)
                    ProcessNode(child, replacement, ref replaced);
                break;

            case JsonValue val when val.TryGetValue<string>(out string? s):
                s = s.Trim(); // remove stray whitespace / newlines

                if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _))
                {
                    val.ReplaceWith(JsonValue.Create(replacement));
                    replaced++;
                }

                break;
        }
    }
}