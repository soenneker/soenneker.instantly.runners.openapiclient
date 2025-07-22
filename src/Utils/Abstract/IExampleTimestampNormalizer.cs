using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Instantly.Runners.OpenApiClient.Utils.Abstract;

public interface IExampleTimestampNormalizer
{
    /// <summary>
    /// Rewrites every `"example": "YYYY‑MM‑DDTHH:mm:ss.fffZ"` in <paramref name="jsonPath"/>
    /// so the value becomes <paramref name="newTimestampUtc"/>.
    /// </summary>
    ValueTask<int> Normalize(string jsonPath, DateTime newTimestampUtc, CancellationToken cancellationToken = default);
}