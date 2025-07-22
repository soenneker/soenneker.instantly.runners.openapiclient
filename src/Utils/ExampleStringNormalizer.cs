using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.File.Abstract;
using Soenneker.Instantly.Runners.OpenApiClient.Utils.Abstract;

namespace Soenneker.Instantly.Runners.OpenApiClient.Utils;

/// <inheritdoc cref="IExampleStringNormalizer"/>
public sealed class ExampleStringNormalizer : IExampleStringNormalizer
{
    private readonly IFileUtil _fileUtil;
    private readonly List<(Func<string, bool> match, string replacement)> _rules = new();

    public ExampleStringNormalizer(IFileUtil fileUtil) => _fileUtil = fileUtil;

    public ExampleStringNormalizer AddRule(Func<string, bool> matcher, string replacement)
    {
        _rules.Add((matcher, replacement));
        return this;
    }

    public async ValueTask<int> Normalize(string jsonPath, CancellationToken ct = default)
    {
        string json = await _fileUtil.Read(jsonPath, cancellationToken: ct).NoSync();
        JsonNode root = JsonNode.Parse(json)!;

        int changed = Traverse(root, key: null, parentKey: null, inExample: false);

        await _fileUtil.Write(jsonPath, root.ToJsonString(new JsonSerializerOptions {WriteIndented = true}), cancellationToken: ct).NoSync();

        return changed;
    }

    private int Traverse(JsonNode? node, string? key, string? parentKey, bool inExample)
    {
        // promote context when we hit an example marker
        bool hereIsExample = key is not null && (
            /* "example": "…" */
            key.Equals("example", StringComparison.OrdinalIgnoreCase) ||
            /*  "examples": [ "…" ]   OR   "examples": { "foo": { "value": "…" } }  */
            key.Equals("examples", StringComparison.OrdinalIgnoreCase) ||
            /* inner { "value": "…" } of a named examples block */
            key.Equals("value", StringComparison.OrdinalIgnoreCase) && parentKey?.Equals("examples", StringComparison.OrdinalIgnoreCase) == true);

        bool ctx = inExample || hereIsExample;
        int hits = 0;

        switch (node)
        {
            /* ----- object ----- */
            case JsonObject obj:
                foreach (var kv in obj.ToList()) // snapshot
                    hits += Traverse(kv.Value, kv.Key, key, ctx);
                break;

            /* ----- array ------ */
            case JsonArray arr:
                for (int i = 0; i < arr.Count; i++) // index‑based
                    hits += Traverse(arr[i], key, parentKey, ctx);
                break;

            /* ----- leaf value in example context ----- */
            case JsonValue val when ctx && val.TryGetValue<string>(out var s):
                s = s!.Trim();
                foreach ((var match, var repl) in _rules)
                {
                    if (match(s))
                    {
                        val.ReplaceWith(JsonValue.Create(repl));
                        hits++;
                        break;
                    }
                }

                break;
        }

        return hits;
    }
}