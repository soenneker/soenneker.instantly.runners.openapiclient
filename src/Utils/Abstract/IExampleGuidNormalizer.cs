using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Instantly.Runners.OpenApiClient.Utils.Abstract;

public interface IExampleGuidNormalizer
{
    ValueTask<int> Normalize(string jsonPath, string newGuid, CancellationToken cancellationToken = default);
}