using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.Task;
using Soenneker.Utils.File.Abstract;

namespace Soenneker.Instantly.Runners.OpenApiClient.Utils;

/// <summary>
/// Treats Instantly identifiers as opaque strings by removing UUID formats from its OpenAPI document.
/// </summary>
public sealed class UuidFormatNormalizer
{
    private readonly IFileUtil _fileUtil;

    public UuidFormatNormalizer(IFileUtil fileUtil)
    {
        _fileUtil = fileUtil;
    }

    /// <summary>
    /// Removes UUID formats from an OpenAPI JSON file.
    /// </summary>
    public async ValueTask<int> Normalize(string jsonPath, CancellationToken cancellationToken = default)
    {
        string json = await _fileUtil.Read(jsonPath, cancellationToken: cancellationToken).NoSync();
        JsonNode root = JsonNode.Parse(json)!;

        int changed = RemoveUuidFormats(root);

        await _fileUtil.Write(jsonPath, root.ToJsonString(new JsonSerializerOptions {WriteIndented = true}), cancellationToken: cancellationToken).NoSync();

        return changed;
    }

    /// <summary>
    /// Removes UUID and UUID4 format annotations from a JSON tree.
    /// </summary>
    public static int RemoveUuidFormats(JsonNode? node)
    {
        int changed = 0;

        switch (node)
        {
            case JsonObject obj:
                if (obj["format"] is JsonValue formatValue && formatValue.TryGetValue(out string? format) &&
                    (string.Equals(format, "uuid", StringComparison.OrdinalIgnoreCase) || string.Equals(format, "uuid4", StringComparison.OrdinalIgnoreCase)))
                {
                    obj.Remove("format");
                    changed++;
                }

                foreach (KeyValuePair<string, JsonNode?> property in obj.ToList())
                    changed += RemoveUuidFormats(property.Value);
                break;

            case JsonArray array:
                foreach (JsonNode? item in array)
                    changed += RemoveUuidFormats(item);
                break;
        }

        return changed;
    }
}
