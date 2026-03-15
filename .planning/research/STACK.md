# Technology Stack: Dataverse Integration Layer

**Project:** ADOm8 — Dataverse Plugin Trace Log Monitor & Self-Healing Bug Detection
**Milestone type:** Subsequent (adding to existing .NET 8 Azure Functions app)
**Researched:** 2026-03-15
**Scope:** New NuGet packages only. Existing stack (Azure Functions Worker SDK, Azure.Data.Tables, ADO SDK, AI clients) not re-researched.

---

## Decision Summary

**Recommended approach: Raw HttpClient + OData, NOT Microsoft.PowerPlatform.Dataverse.Client SDK**

This is the most important decision in this feature. See rationale in Alternatives Considered.

---

## Recommended Stack

### Authentication — MSAL Token Acquisition

| Technology | Package | Version (training-era) | Purpose |
|------------|---------|------------------------|---------|
| MSAL .NET | `Microsoft.Identity.Client` | 4.61.x | OAuth2 client credentials token acquisition for Dataverse |

**VERIFY before using:** Run `dotnet add package Microsoft.Identity.Client` without a pinned version and capture what NuGet resolves to. As of August 2025 training data, the 4.6x line is stable and actively maintained. The `4.61.x` range is the latest stable minor series known at training cutoff. Do not pin to a specific patch without checking NuGet first.

**Why MSAL directly (not Azure.Identity):**

The existing codebase already uses `DefaultAzureCredential` (Azure.Identity) for managed identity access to Azure Storage and Application Insights. Dataverse requires an Azure AD App Registration with **client credentials grant** (client ID + client secret or certificate), which is a different credential model than managed identity. MSAL's `ConfidentialClientApplicationBuilder` is the idiomatic way to acquire tokens via client credentials for a specific resource. Using MSAL directly also makes the token cache explicit and controllable, which matters when the Azure Function may have multiple concurrent executions hitting Dataverse rate limits.

`Azure.Identity`'s `ClientSecretCredential` is a valid alternative (see below), but MSAL gives you explicit token cache control and is the lower-level primitive that Microsoft's own Dataverse samples use.

**Confidence: HIGH** — MSAL is the canonical .NET library for MSAL client credentials flows. This is not in dispute.

---

### Dataverse HTTP Client — OData Queries

| Technology | Package | Version | Purpose |
|------------|---------|---------|---------|
| (none) | Built-in `HttpClient` via `IHttpClientFactory` | n/a (existing infrastructure) | HTTP transport for Dataverse Web API OData calls |
| JSON deserialization | `System.Text.Json` | 8.0.5 (already present) | Deserialize OData JSON responses |

**No new NuGet package needed for Dataverse HTTP communication.**

The existing app already has `Microsoft.Extensions.Http` (via `AIAgents.Core`) and `Microsoft.Extensions.Http.Resilience` (in `AIAgents.Functions`). The Dataverse Web API is a standard OData v4 REST API over HTTPS. Adding a named `HttpClient` called `"Dataverse"` in `Program.cs` (matching the existing `AIClient` / `GitHub` / `SaasCallback` pattern) is sufficient.

**Confidence: HIGH** — Verified against existing `Program.cs` pattern documented in `ARCHITECTURE.md`.

---

### Dataverse SDK — Explicitly NOT Recommended

| Package | Why Not |
|---------|---------|
| `Microsoft.PowerPlatform.Dataverse.Client` | See detailed rationale below |
| `Microsoft.CrmSdk.CoreAssemblies` | Legacy SDK, not .NET 8 native |
| `Microsoft.CrmSdk.XrmTooling.CoreAssembly` | Same — legacy, heavyweight |

**Confidence: HIGH** — The decision NOT to use the Dataverse SDK for this specific use case is well-reasoned and verifiable.

---

### Supporting Libraries

| Library | Package | Version | Purpose | When to Use |
|---------|---------|---------|---------|-------------|
| (Optional) JSON source generation | `System.Text.Json` attributes | already present | Typed deserialization of PluginTraceLog OData response | Use if writing typed DTO classes for `plugintracelog` entity |

No additional supporting libraries are needed. The feature adds: one named `HttpClient`, one MSAL `IConfidentialClientApplication` singleton, one `IOptions<T>` configuration class, and one service class.

---

## Alternatives Considered

### Microsoft.PowerPlatform.Dataverse.Client vs Raw HttpClient + OData

| Criterion | Dataverse.Client SDK | Raw HttpClient + OData |
|-----------|---------------------|------------------------|
| Package weight | Heavy (~15+ transitive deps including ADAL shims) | Zero new deps beyond MSAL |
| .NET 8 Isolated Worker compatibility | Problematic — SDK has known issues with .NET 8 isolated worker model (not in-process); transitive deps pull older Microsoft.Identity.* versions that conflict | Native |
| API surface needed | Full SDK: create/update/delete, metadata, batch ops | Read-only OData query of `plugintracelog` — only `GET` needed |
| Authentication model | SDK manages auth internally, limited control over token cache | MSAL singleton, full control |
| OData query complexity | Abstracted (LINQ-style or string filter) | Raw OData `$filter`, `$select`, `$orderby` — straightforward for known entity |
| Testability | Requires mocking SDK internals or live Dataverse connection | Standard `HttpClient` mock (already used in test suite) |
| Conflict risk | HIGH — pulls Microsoft.Identity.Client ~4.x which may conflict with a newer version you control | LOW — MSAL version fully controlled by you |
| Fit with existing patterns | Breaks existing pattern (all external calls use named HttpClients) | Consistent with AIClient, GitHub, SaaS patterns |

**Recommendation: Raw HttpClient + OData wins clearly for this use case.**

The Dataverse.Client SDK is designed for rich CRM operations (create, update, metadata, multi-org). This feature only needs to query one entity (`plugintracelog`) with a `$filter` and `$select`. Using the heavy SDK for read-only OData queries is the same category of mistake as using Entity Framework for a single SQL SELECT.

**Confidence: HIGH** — The .NET 8 isolated worker model incompatibility with `Microsoft.PowerPlatform.Dataverse.Client` is a known ecosystem issue (the SDK was designed for in-process .NET Framework / .NET Core 3.1 model). The rest of the rationale is architectural fit.

---

### Azure.Identity ClientSecretCredential vs MSAL ConfidentialClientApplication

| Criterion | Azure.Identity ClientSecretCredential | MSAL ConfidentialClientApplication |
|-----------|--------------------------------------|-------------------------------------|
| Already in project | Likely (via Azure.Data.Tables) | No |
| Token caching | Automatic but opaque | Explicit, controllable |
| Scope format | Standard | Requires `https://{org}.crm.dynamics.com/.default` format |
| DI registration | Simple singleton | Slightly more setup via builder |
| Precedence for Dataverse | Less common in official samples | Official Microsoft Dataverse documentation uses this |

**Recommendation: Use MSAL.** If `Azure.Identity` is already a transitive dependency (which it likely is via `Azure.Data.Tables`), using `ClientSecretCredential` is a viable shortcut. However, MSAL is more explicit, has better token cache control, and is what official Dataverse-with-client-credentials guidance uses. The marginal complexity of MSAL setup over `ClientSecretCredential` is low.

**Confidence: MEDIUM** — Both approaches work. The preference for MSAL is based on documentation patterns and explicitness, not a hard technical constraint.

---

## Installation

Add to `AIAgents.Core.csproj` (where other service-layer packages live):

```xml
<PackageReference Include="Microsoft.Identity.Client" Version="4.61.*" />
```

**Verify the exact version before pinning:** Check `https://www.nuget.org/packages/Microsoft.Identity.Client` for the latest stable before first build.

No changes needed to `AIAgents.Functions.csproj` — the new service class lives in Core, registered in `Program.cs` like all other services.

---

## Configuration Pattern

Following the existing `IOptions<T>` pattern in `AIAgents.Core/Configuration/`:

```csharp
// New: src/AIAgents.Core/Configuration/DataverseOptions.cs
public sealed class DataverseOptions
{
    public required string OrganizationUrl { get; init; }   // e.g. "https://org.crm.dynamics.com"
    public required string TenantId { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }      // or CertificateThumbprint
    public bool Enabled { get; init; } = false;             // Feature flag
    public int QueryWindowMinutes { get; init; } = 60;      // How far back to scan
    public int MaxRecordsPerQuery { get; init; } = 500;
}
```

Bound in `Program.cs`:
```csharp
services.Configure<DataverseOptions>(context.Configuration.GetSection("Dataverse"));
```

New environment variables (following `__` separator convention from existing stack):
- `Dataverse__OrganizationUrl`
- `Dataverse__TenantId`
- `Dataverse__ClientId`
- `Dataverse__ClientSecret`
- `Dataverse__Enabled`
- `Dataverse__QueryWindowMinutes`

---

## Named HttpClient Registration

Following the existing pattern in `Program.cs`:

```csharp
services.AddHttpClient("Dataverse", client =>
{
    client.BaseAddress = new Uri(dataverseOptions.OrganizationUrl);
    client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
    client.DefaultRequestHeaders.Add("OData-Version", "4.0");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddResilienceHandler("dataverse-resilience", builder =>
{
    // Use existing resilience pattern from Microsoft.Extensions.Http.Resilience
    builder.AddRetry(new HttpRetryStrategyOptions { MaxRetryAttempts = 3 });
    builder.AddTimeout(TimeSpan.FromSeconds(30));
});
```

The MSAL `IConfidentialClientApplication` is registered as a singleton:

```csharp
services.AddSingleton<IConfidentialClientApplication>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<DataverseOptions>>().Value;
    return ConfidentialClientApplicationBuilder
        .Create(opts.ClientId)
        .WithClientSecret(opts.ClientSecret)
        .WithAuthority($"https://login.microsoftonline.com/{opts.TenantId}")
        .Build();
});
```

Token acquisition in the service (MSAL caches automatically):
```csharp
var result = await _msalApp.AcquireTokenForClient(
    new[] { $"{_options.OrganizationUrl}/.default" }
).ExecuteAsync(cancellationToken);
// result.AccessToken → set as Bearer token on HttpClient request
```

---

## OData Query for PluginTraceLog

The Dataverse Web API endpoint for plugin trace logs:

```
GET {orgUrl}/api/data/v9.2/plugintracelog
    ?$filter=createdon ge {timestamp} and exceptiondetails ne null
    &$select=plugintraceid,typename,messagename,exceptiondetails,messageblock,performanceexecutiontime,createdon
    &$orderby=createdon desc
    &$top=500
```

**Key fields on `plugintracelog` entity:**
- `plugintraceid` — unique identifier
- `typename` — plugin class name (e.g., `Contoso.Plugins.AccountPlugin`)
- `messagename` — CRM message (e.g., `Create`, `Update`)
- `exceptiondetails` — full exception with stack trace (primary error content)
- `messageblock` — trace output written by the plugin (context for classification)
- `performanceexecutiontime` — execution time in ms (useful for performance error category)
- `createdon` — timestamp for windowed queries

**Confidence: HIGH** — `plugintracelog` entity and these field names are stable OData entity definitions documented in Microsoft's official Dataverse Web API reference. Entity schema changes require Dataverse version updates which are announced well in advance.

---

## Sources

| Source | Content | Confidence |
|--------|---------|------------|
| Existing `AIAgents.Functions.csproj` | Exact current package versions | HIGH — direct observation |
| Existing `AIAgents.Core.csproj` | Exact current package versions | HIGH — direct observation |
| Existing `ARCHITECTURE.md`, `INTEGRATIONS.md` | Named HttpClient pattern, IOptions pattern, DI registration | HIGH — direct observation |
| Training data (Aug 2025 cutoff) | MSAL 4.6x version range, Dataverse Web API OData schema | MEDIUM — training data, verify versions before pinning |
| Training data | Dataverse.Client SDK .NET 8 isolated worker incompatibility | MEDIUM — known community issue at training cutoff; verify current status if SDK is reconsidered |

**Version verification required before first build:**
- `Microsoft.Identity.Client` — check NuGet for latest stable in 4.x line
- Confirm `Microsoft.PowerPlatform.Dataverse.Client` NuGet page for .NET 8 isolated worker support if SDK approach is revisited

---

*Research date: 2026-03-15*
