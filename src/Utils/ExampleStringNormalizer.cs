using Soenneker.Extensions.ValueTask;
using Soenneker.Instantly.Runners.OpenApiClient.Utils.Abstract;
using Soenneker.Utils.File.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Instantly.Runners.OpenApiClient.Utils;

///<inheritdoc cref="IExampleStringNormalizer"/>
public sealed class ExampleStringNormalizer : IExampleStringNormalizer
{
    private readonly IFileUtil _fileUtil;
    private readonly List<(Func<string, bool> matcher, string replacement)> _rules = [];

    public ExampleStringNormalizer(IFileUtil fileUtil) => _fileUtil = fileUtil;

    /// <summary>Add a rule: if <paramref name="matcher"/> returns true, replace the value with <paramref name="replacement"/>.</summary>
    public ExampleStringNormalizer AddRule(Func<string, bool> matcher, string replacement)
    {
        _rules.Add((matcher, replacement));
        return this;
    }

    /// <returns>The number of replacements performed.</returns>
    public async ValueTask<int> Normalize(string jsonPath, CancellationToken cancellationToken = default)
    {
        string json = await _fileUtil.Read(jsonPath, cancellationToken: cancellationToken).NoSync();
        JsonNode root = JsonNode.Parse(json)!;

        int changed = 0;

        Traverse(root, null, null, val =>
        {
            if (!val.TryGetValue(out string? s))
                return;

            s = s.Trim();

            foreach ((Func<string, bool> match, string repl) in _rules)
            {
                if (match(s))
                {
                    val.ReplaceWith(JsonValue.Create(repl));
                    changed++;
                    break;
                }
            }
        });

        await _fileUtil.Write(jsonPath, root.ToJsonString(new JsonSerializerOptions {WriteIndented = true}), cancellationToken: cancellationToken).NoSync();
        return changed;
    }

    private static void Traverse(
        JsonNode? node,
        string? key,
        string? parentKey,
        Action<JsonValue> onValue)
    {
        switch (node)
        {
            /* ------------ object ------------ */
            case JsonObject obj:
                // Take a snapshot so mutating children won't invalidate the loop
                foreach (var kv in obj.ToList())          // <- snapshot
                    Traverse(kv.Value, kv.Key, key, onValue);
                break;

            /* ------------ array ------------- */
            case JsonArray arr:
                for (int i = 0; i < arr.Count; i++)       // <- index‑based
                    Traverse(arr[i], key, parentKey, onValue);
                break;

            /* --------- leaf value ----------- */
            case JsonValue v when IsExampleKey(key, parentKey):
                onValue(v);
                break;
        }
    }

    private static bool IsExampleKey(string? key, string? parentKey) =>
        key is not null && (key.Equals("example", StringComparison.OrdinalIgnoreCase) || key.Equals("value", StringComparison.OrdinalIgnoreCase) &&
            parentKey?.Equals("examples", StringComparison.OrdinalIgnoreCase) == true);
}