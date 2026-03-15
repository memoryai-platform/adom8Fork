using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIAgents.Core.Configuration;
using AIAgents.Core.Interfaces;
using AIAgents.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;

namespace AIAgents.Core.Services;

/// <summary>
/// Dataverse Web API client for reading PluginTraceLog entries with app-only authentication.
/// </summary>
public sealed class DataverseClient : IDataverseClient
{
    private const string PluginTraceLogSelect =
        "plugintracelogid,typename,messagename,primaryentity,mode,depth,createdon,exceptiondetails,messageblock";
    private const string BaseFilter = "exceptiondetails ne null";
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DataverseOptions _options;
    private readonly ILogger _logger;
    private readonly Func<CancellationToken, Task<string>> _accessTokenProvider;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    public DataverseClient(
        IConfidentialClientApplication confidentialClientApplication,
        IHttpClientFactory httpClientFactory,
        IOptions<DataverseOptions> options,
        ILogger<DataverseClient> logger)
        : this(
            httpClientFactory,
            options.Value,
            logger,
            cancellationToken => AcquireAccessTokenAsync(confidentialClientApplication, options.Value, cancellationToken))
    {
    }

    internal DataverseClient(
        IHttpClientFactory httpClientFactory,
        DataverseOptions options,
        ILogger logger,
        Func<CancellationToken, Task<string>> accessTokenProvider,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
        _accessTokenProvider = accessTokenProvider;
        _delayAsync = delayAsync ?? Task.Delay;
    }

    public async Task<IReadOnlyList<PluginTraceLogEntry>> GetPluginTraceLogsAsync(
        DateTime? createdAfterUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            return Array.Empty<PluginTraceLogEntry>();
        }

        var client = _httpClientFactory.CreateClient("Dataverse");
        var accessToken = await _accessTokenProvider(cancellationToken);
        var results = new List<PluginTraceLogEntry>();
        string? nextRequestUri = BuildPluginTraceLogQuery(createdAfterUtc);

        while (!string.IsNullOrWhiteSpace(nextRequestUri))
        {
            using var response = await SendWithRetryAsync(client, nextRequestUri, accessToken, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = JsonSerializer.Deserialize<PluginTraceLogResponse>(body, s_jsonOptions)
                ?? new PluginTraceLogResponse();

            if (payload.Value.Count > 0)
            {
                results.AddRange(payload.Value);
            }

            nextRequestUri = payload.NextLink;
        }

        return results;
    }

    internal static string BuildAccessTokenScope(string baseUrl)
    {
        var orgRoot = NormalizeOrgRoot(baseUrl);
        return $"{orgRoot}/.default";
    }

    public static string NormalizeOrgRoot(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Dataverse:BaseUrl is required.");
        }

        var trimmed = baseUrl.Trim().TrimEnd('/');
        var apiMarker = "/api/data/";
        var apiIndex = trimmed.IndexOf(apiMarker, StringComparison.OrdinalIgnoreCase);
        if (apiIndex >= 0)
        {
            trimmed = trimmed[..apiIndex];
        }

        return trimmed.TrimEnd('/');
    }

    private static async Task<string> AcquireAccessTokenAsync(
        IConfidentialClientApplication confidentialClientApplication,
        DataverseOptions options,
        CancellationToken cancellationToken)
    {
        var scope = BuildAccessTokenScope(options.BaseUrl ?? string.Empty);
        var result = await confidentialClientApplication
            .AcquireTokenForClient(new[] { scope })
            .ExecuteAsync(cancellationToken);

        return result.AccessToken;
    }

    private string BuildPluginTraceLogQuery(DateTime? createdAfterUtc)
    {
        var filters = new List<string> { BaseFilter };
        if (createdAfterUtc.HasValue)
        {
            var createdOn = createdAfterUtc.Value.ToUniversalTime()
                .ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
            filters.Add($"createdon gt {createdOn}");
        }

        var queryParts = new List<string>
        {
            $"$select={Uri.EscapeDataString(PluginTraceLogSelect)}",
            $"$filter={Uri.EscapeDataString(string.Join(" and ", filters))}",
            $"$orderby={Uri.EscapeDataString("createdon asc")}",
            $"$top={_options.PluginTraceLogPageSize}"
        };

        return $"plugintracelogs?{string.Join("&", queryParts)}";
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient client,
        string requestUri,
        string accessToken,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.SendAsync(request, cancellationToken);
            if (!ShouldRetry(response.StatusCode) || attempt == maxAttempts)
            {
                return response;
            }

            var delay = GetRetryDelay(response, attempt);
            _logger.LogWarning(
                "Dataverse request to {RequestUri} failed with {StatusCode}. Retrying in {DelayMs}ms (attempt {Attempt}/{MaxAttempts}).",
                requestUri,
                (int)response.StatusCode,
                delay.TotalMilliseconds,
                attempt,
                maxAttempts);

            response.Dispose();
            await _delayAsync(delay, cancellationToken);
        }

        throw new InvalidOperationException("Dataverse retry loop exited unexpectedly.");
    }

    private static bool ShouldRetry(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.TryGetValues("Retry-After", out var values))
        {
            var rawValue = values.FirstOrDefault();
            if (int.TryParse(rawValue, out var retryAfterSeconds) && retryAfterSeconds >= 0)
            {
                return TimeSpan.FromSeconds(retryAfterSeconds);
            }
        }

        return TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
    }

    private sealed class PluginTraceLogResponse
    {
        [JsonPropertyName("value")]
        public List<PluginTraceLogEntry> Value { get; init; } = [];

        [JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; init; }
    }
}
