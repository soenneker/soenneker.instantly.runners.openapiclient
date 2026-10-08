using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Soenneker.Instantly.Runners.OpenApiClient.Utils;
using System.Threading;

namespace Soenneker.Instantly.Runners.OpenApiClient.Tests;

public sealed class UuidFormatNormalizerTests
{
    [Test]
    public async ValueTask RemoveUuidFormats_should_remove_uuid_formats_only(CancellationToken cancellationToken)
    {
        JsonNode root = JsonNode.Parse("""
                                           {
                                             "components": {
                                               "schemas": {
                                                 "Account": {
                                                   "properties": {
                                                     "organization": { "type": "string", "format": "uuid" },
                                                     "warmup_pool_id": { "type": ["null", "string"], "format": "uuid4" },
                                                     "timestamp_created": { "type": "string", "format": "date-time" }
                                                   }
                                                 }
                                               }
                                             }
                                           }
                                           """)!;

        int changed = UuidFormatNormalizer.RemoveUuidFormats(root);

        await Assert.That(changed).IsEqualTo(2);
        await Assert.That(root["components"]!["schemas"]!["Account"]!["properties"]!["organization"]!["format"]).IsNull();
        await Assert.That(root["components"]!["schemas"]!["Account"]!["properties"]!["warmup_pool_id"]!["format"]).IsNull();
        await Assert.That(root["components"]!["schemas"]!["Account"]!["properties"]!["timestamp_created"]!["format"]!.GetValue<string>())
                    .IsEqualTo("date-time");
    }
}
