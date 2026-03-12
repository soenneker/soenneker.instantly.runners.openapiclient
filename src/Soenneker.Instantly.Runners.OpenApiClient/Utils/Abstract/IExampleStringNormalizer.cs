using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Instantly.Runners.OpenApiClient.Utils.Abstract;

internal interface IExampleStringNormalizer
{
    /// <summary>
    /// Walks an OpenAPI/Terraform/etc. JSON file and rewrites <em>example</em> values
    /// that match user‑supplied predicates.
    /// </summary>
    ValueTask<int> Normalize(string jsonPath, CancellationToken cancellationToken = default);
}