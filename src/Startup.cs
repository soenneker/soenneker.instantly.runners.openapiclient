using Microsoft.Extensions.DependencyInjection;
using Soenneker.Managers.Runners.Registrars;
using Soenneker.Instantly.Runners.OpenApiClient.Utils;
using Soenneker.Instantly.Runners.OpenApiClient.Utils.Abstract;
using Soenneker.Cloudflare.Downloader.Registrars;

namespace Soenneker.Instantly.Runners.OpenApiClient;

/// <summary>
/// Console type startup
/// </summary>
public static class Startup
{
    // This method gets called by the runtime. Use this method to add services to the container.
    public static void ConfigureServices(IServiceCollection services)
    {
        services.SetupIoC();
    }

    public static IServiceCollection SetupIoC(this IServiceCollection services)
    {
        services.AddHostedService<ConsoleHostedService>()
                .AddScoped<IFileOperationsUtil, FileOperationsUtil>()
                .AddRunnersManagerAsScoped()
                .AddCloudflareDownloaderAsScoped()
                .AddScoped<IExampleTimestampNormalizer, ExampleTimestampNormalizer>()
                .AddScoped<IExampleGuidNormalizer, ExampleGuidNormalizer>();

        return services;
    }
}