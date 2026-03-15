using AIAgents.Core.Configuration;
using AIAgents.Core.Interfaces;
using AIAgents.Core.Services;
using AIAgents.Functions.Extensions;
using AIAgents.Functions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;

namespace AIAgents.Functions.Tests.Services;

public sealed class DataverseServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDataverseMonitoring_WithoutConfig_RegistersNoOpServices()
    {
        var configuration = BuildConfiguration();
        var services = CreateServices();

        services.AddDataverseMonitoring(configuration);

        using var provider = services.BuildServiceProvider();
        var dataverseClient = provider.GetRequiredService<IDataverseClient>();
        var errorTrackingService = provider.GetRequiredService<IErrorTrackingService>();
        var msalClient = provider.GetService<IConfidentialClientApplication>();

        Assert.IsType<NoOpDataverseClient>(dataverseClient);
        Assert.IsType<NoOpErrorTrackingService>(errorTrackingService);
        Assert.Null(msalClient);
    }

    [Fact]
    public void AddDataverseMonitoring_WithConfig_RegistersRealServices()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{DataverseOptions.SectionName}:BaseUrl"] = "https://contoso.crm.dynamics.com",
            [$"{DataverseOptions.SectionName}:TenantId"] = "11111111-2222-3333-4444-555555555555",
            [$"{DataverseOptions.SectionName}:ClientId"] = "66666666-7777-8888-9999-aaaaaaaaaaaa",
            [$"{DataverseOptions.SectionName}:ClientSecret"] = "secret-value"
        });
        var services = CreateServices();

        services.AddDataverseMonitoring(configuration);

        using var provider = services.BuildServiceProvider();
        var dataverseClient = provider.GetRequiredService<IDataverseClient>();
        var errorTrackingService = provider.GetRequiredService<IErrorTrackingService>();
        var msalClient = provider.GetRequiredService<IConfidentialClientApplication>();
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient("Dataverse");

        Assert.IsType<DataverseClient>(dataverseClient);
        Assert.IsType<TableStorageErrorTrackingService>(errorTrackingService);
        Assert.NotNull(msalClient);
        Assert.Equal(new Uri("https://contoso.crm.dynamics.com/api/data/v9.2/"), httpClient.BaseAddress);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["AzureWebJobsStorage"] = "UseDevelopmentStorage=true"
        };

        if (overrides is not null)
        {
            foreach (var (key, value) in overrides)
            {
                values[key] = value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
