using Soenneker.Tests.HostedUnit;

namespace Soenneker.Instantly.Runners.OpenApiClient.Tests;

[ClassDataSource<Host>(Shared = SharedType.PerTestSession)]
public sealed class InstantlyOpenApiRunnerTests : HostedUnitTest
{
    public InstantlyOpenApiRunnerTests(Host host) : base(host)
    {

    }

    [Test]
    public void Default()
    {

    }
}
