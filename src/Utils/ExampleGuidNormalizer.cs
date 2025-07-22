using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.File.Abstract;
using Soenneker.Instantly.Runners.OpenApiClient.Utils.Abstract;

namespace Soenneker.Instantly.Runners.OpenApiClient.Utils;

/// <summary>Utility that normalises GUID example strings in an OpenAPI spec.</summary>
public sealed class ExampleGuidNormalizer : IExampleGuidNormalizer
{
    private readonly IFileUtil _fileUtil;

    public ExampleGuidNormalizer(IFileUtil fileUtil) => _fileUtil = fileUtil;

    /// <param name="newGuid">
    /// The GUID that should replace every example value recognised as a GUID.
    /// </param>
    /// <returns>The number of example strings rewritten.</returns>
    public async ValueTask<int> Normalize(string jsonPath, string newGuid, CancellationToken cancellationToken = default)
    {
        /* 1 — load */
        string json = await _fileUtil.Read(jsonPath, cancellationToken: cancellationToken).NoSync();
        JsonNode root = JsonNode.Parse(json)!;

        int replaced = 0;
        ProcessNode(root, currentKey: null, parentKey: null, newGuid, ref replaced);

        /* 2 — persist (pretty‑printed) */
        var opts = new JsonSerializerOptions {WriteIndented = true};
        await _fileUtil.Write(jsonPath, root.ToJsonString(opts), cancellationToken: cancellationToken).NoSync();

        return replaced;
    }

    /* ------------------------------------------------------------------ */

    private static void ProcessNode(JsonNode? node, string? currentKey, // property name of <node> under its parent (null for arrays / root)
        string? parentKey, // property name of the parent object
        string replacement, ref int replaced)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, child) in obj)
                    ProcessNode(child, key, currentKey, replacement, ref replaced);
                break;

            case JsonArray arr:
                foreach (var child in arr)
                    ProcessNode(child, currentKey: null, parentKey: currentKey, replacement, ref replaced);
                break;

            case JsonValue val when val.TryGetValue<string>(out var s):
                if (!ShouldConsider(currentKey, parentKey)) return;

                if (Guid.TryParse(s.Trim(), out _))
                {
                    val.ReplaceWith(JsonValue.Create(replacement));
                    replaced++;
                }

                break;
        }
    }

    /// <summary>
    /// True when <paramref name="currentKey"/> represents an “example” string we care about:
    ///   * …"<c>example</c>": "&lt;string&gt;"
    ///   * …"<c>examples</c>": { "<i>name</i>": { "<c>value</c>": "&lt;string&gt;" } }
    /// </summary>
    private static bool ShouldConsider(string? currentKey, string? parentKey)
    {
        if (string.IsNullOrWhiteSpace(currentKey))
            return false;

        if (currentKey.Equals("example", StringComparison.OrdinalIgnoreCase))
            return true;

        return currentKey.Equals("value", StringComparison.OrdinalIgnoreCase) && parentKey != null &&
               parentKey.Equals("examples", StringComparison.OrdinalIgnoreCase);
    }
}