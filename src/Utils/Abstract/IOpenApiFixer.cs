using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Instantly.Runners.OpenApiClient.Utils.Abstract;

public interface IOpenApiFixer
{
    ValueTask Fix(string sourceFilePath, string targetFilePath, CancellationToken cancellationToken = default);
}
