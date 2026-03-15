using System.Net;
using System.Text.Json;
using AIAgents.Core.Configuration;
using AIAgents.Core.Models;
using AIAgents.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;

namespace AIAgents.Core.Tests.Services;

public sealed class DataverseClientTests
{
    private static DataverseOptions CreateOptions() => new()
    {
        BaseUrl = "https://contoso.crm.dynamics.com/api/data/v9.2",
        TenantId = "tenant-id",
        ClientId = "client-id",
        ClientSecret = "client-secret",
        PluginTraceLogPageSize = 250
    };

    private static string CreateResponseJson(
        IReadOnlyList<PluginTraceLogEntry> entries,
        string? nextLink = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["value"] = entries
        };

        if (nextLink is not null)
        {
            payload["@odata.nextLink"] = nextLink;
        }

        return JsonSerializer.Serialize(payload);
    }

    private static PluginTraceLogEntry CreateEntry(string id, string typeName, DateTime createdOnUtc) => new()
    {
        PluginTraceLogId = id,
        TypeName = typeName,
        MessageName = "Create",
        PrimaryEntity = "account",
        Mode = 0,
        Depth = 0,
        CreatedOnUtc = createdOnUtc,
        ExceptionDetails = "Boom",
        MessageBlock = "Boom"
    };

    [Fact]
    public void BuildAccessTokenScope_StripsApiPathAndAddsDefaultScope()
    {
        var scope = DataverseClient.BuildAccessTokenScope("https://contoso.crm.dynamics.com/api/data/v9.2");

        Assert.Equal("https://contoso.crm.dynamics.com/.default", scope);
    }

    [Fact]
    public async Task GetPluginTraceLogsAsync_IncludesExceptionDetailsFilterAndAscendingOrder()
    {
        HttpRequestMessage? capturedRequest = null;
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateResponseJson([CreateEntry("1", "Plugin.A", DateTime.UtcNow)]))
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://contoso.crm.dynamics.com/api/data/v9.2/")
        };

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(factory => factory.CreateClient("Dataverse")).Returns(httpClient);

        var client = new DataverseClient(
            factoryMock.Object,
            CreateOptions(),
            NullLogger<DataverseClient>.Instance,
            _ => Task.FromResult("access-token"));

        var results = await client.GetPluginTraceLogsAsync(DateTime.Parse("2026-03-15T01:02:03Z"));

        Assert.Single(results);
        Assert.NotNull(capturedRequest);
        Assert.Equal("Bearer", capturedRequest!.Headers.Authorization?.Scheme);
        Assert.Equal("access-token", capturedRequest.Headers.Authorization?.Parameter);
        var requestUri = Uri.UnescapeDataString(capturedRequest.RequestUri!.ToString());
        Assert.Contains("exceptiondetails ne null", requestUri);
        Assert.Contains("createdon asc", requestUri);
    }

    [Fact]
    public async Task GetPluginTraceLogsAsync_FollowsODataNextLink()
    {
        var callCount = 0;
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((request, _) =>
            {
                callCount++;
                var content = callCount == 1
                    ? CreateResponseJson(
                        [CreateEntry("1", "Plugin.A", DateTime.UtcNow)],
                        "https://contoso.crm.dynamics.com/api/data/v9.2/plugintracelogs?$skiptoken=page-2")
                    : CreateResponseJson([CreateEntry("2", "Plugin.B", DateTime.UtcNow.AddMinutes(1))]);

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content)
                });
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://contoso.crm.dynamics.com/api/data/v9.2/")
        };

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(factory => factory.CreateClient("Dataverse")).Returns(httpClient);

        var client = new DataverseClient(
            factoryMock.Object,
            CreateOptions(),
            NullLogger<DataverseClient>.Instance,
            _ => Task.FromResult("access-token"));

        var results = await client.GetPluginTraceLogsAsync();

        Assert.Equal(2, callCount);
        Assert.Equal(2, results.Count);
        Assert.Equal("1", results[0].PluginTraceLogId);
        Assert.Equal("2", results[1].PluginTraceLogId);
    }

    [Fact]
    public async Task GetPluginTraceLogsAsync_RetriesOnTooManyRequests()
    {
        var callCount = 0;
        var delays = new List<TimeSpan>();
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((_, _) =>
            {
                callCount++;

                if (callCount == 1)
                {
                    var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    throttled.Headers.TryAddWithoutValidation("Retry-After", "1");
                    return Task.FromResult(throttled);
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(CreateResponseJson([CreateEntry("1", "Plugin.A", DateTime.UtcNow)]))
                });
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://contoso.crm.dynamics.com/api/data/v9.2/")
        };

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(factory => factory.CreateClient("Dataverse")).Returns(httpClient);

        var client = new DataverseClient(
            factoryMock.Object,
            CreateOptions(),
            NullLogger<DataverseClient>.Instance,
            _ => Task.FromResult("access-token"),
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        var results = await client.GetPluginTraceLogsAsync();

        Assert.Single(results);
        Assert.Equal(2, callCount);
        Assert.Single(delays);
        Assert.Equal(TimeSpan.FromSeconds(1), delays[0]);
    }
}
