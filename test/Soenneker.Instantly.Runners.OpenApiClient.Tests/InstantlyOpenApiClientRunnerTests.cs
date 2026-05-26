using Soenneker.Tests.HostedUnit;

namespace Soenneker.Instantly.Runners.OpenApiClient.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class InstantlyOpenApiClientRunnerTests : HostedUnitTest
{
    public InstantlyOpenApiClientRunnerTests(Host host) : base(host)
    {
    }

    [Test]
    [Skip("Manual")]
    public void Default()
    {

    }
}
