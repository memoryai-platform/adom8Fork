using AIAgents.Core.Configuration;
using AIAgents.Core.Interfaces;
using AIAgents.Core.Services;
using AIAgents.Functions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Identity.Client;

namespace AIAgents.Functions.Extensions;

/// <summary>
/// Registers Dataverse monitoring services in either dormant-mode or fully configured mode.
/// </summary>
public static class DataverseServiceCollectionExtensions
{
    public static IServiceCollection AddDataverseMonitoring(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(configuration);
        services.Configure<DataverseOptions>(configuration.GetSection(DataverseOptions.SectionName));

        var options = configuration
            .GetSection(DataverseOptions.SectionName)
            .Get<DataverseOptions>() ?? new DataverseOptions();

        if (!options.IsConfigured)
        {
            services.AddSingleton<IDataverseClient, NoOpDataverseClient>();
            services.AddSingleton<IErrorTrackingService, NoOpErrorTrackingService>();
            return services;
        }

        services.AddSingleton<IConfidentialClientApplication>(_ =>
            ConfidentialClientApplicationBuilder
                .Create(options.ClientId!)
                .WithTenantId(options.TenantId!)
                .WithClientSecret(options.ClientSecret!)
                .Build());

        services.AddHttpClient("Dataverse", client =>
            {
                client.BaseAddress = BuildApiBaseAddress(options.BaseUrl!);
                client.Timeout = TimeSpan.FromSeconds(90);
            })
            .AddStandardResilienceHandler();

        services.AddSingleton<IDataverseClient, DataverseClient>();
        services.AddSingleton<IErrorTrackingService, TableStorageErrorTrackingService>();

        return services;
    }

    internal static Uri BuildApiBaseAddress(string baseUrl)
    {
        var orgRoot = DataverseClient.NormalizeOrgRoot(baseUrl);
        return new Uri($"{orgRoot}/api/data/v9.2/", UriKind.Absolute);
    }
}
