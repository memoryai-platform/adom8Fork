param(
    [Parameter(Mandatory = $true)]
    [string]$FunctionAppUrl,

    [string]$FunctionKey,

    [string]$GitHubWebhookSecret,

    [switch]$IncludeQueueingTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$baseUrl = $FunctionAppUrl.TrimEnd('/')
$payloadDir = Join-Path $PSScriptRoot 'payloads'

$totalTests = 0
$passedTests = 0
$failedTests = 0
$results = New-Object System.Collections.Generic.List[object]

function New-Result {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Detail
    )

    [pscustomobject]@{
        Name = $Name
        Passed = $Passed
        Detail = $Detail
    }
}

function Add-TestResult {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Detail
    )

    $script:totalTests++
    if ($Passed) {
        $script:passedTests++
        Write-Host "[PASS] $Name - $Detail" -ForegroundColor Green
    }
    else {
        $script:failedTests++
        Write-Host "[FAIL] $Name - $Detail" -ForegroundColor Red
    }

    $script:results.Add((New-Result -Name $Name -Passed $Passed -Detail $Detail))
}

function Get-Url {
    param(
        [string]$Route,
        [bool]$NeedsFunctionKey = $false
    )

    $url = "$baseUrl$Route"

    if (-not $NeedsFunctionKey) {
        return $url
    }

    if ([string]::IsNullOrWhiteSpace($FunctionKey)) {
        throw "Function key is required for route '$Route'. Provide -FunctionKey."
    }

    $separator = if ($url.Contains('?')) { '&' } else { '?' }
    return "$url${separator}code=$([Uri]::EscapeDataString($FunctionKey))"
}

function Invoke-Endpoint {
    param(
        [ValidateSet('GET', 'POST')]
        [string]$Method,
        [string]$Url,
        [hashtable]$Headers = @{},
        [string]$Body = $null
    )

    $invokeParams = @{
        Uri = $Url
        Method = $Method
        UseBasicParsing = $true
        ErrorAction = 'Stop'
    }

    if ($Headers.Count -gt 0) {
        $invokeParams.Headers = $Headers
    }

    if ($Method -eq 'POST') {
        $invokeParams.ContentType = 'application/json'
        if ($null -ne $Body) {
            $invokeParams.Body = $Body
        }
    }

    try {
        $response = Invoke-WebRequest @invokeParams
        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Body = [string]$response.Content
        }
    }
    catch {
        $webResponse = $_.Exception.Response
        if ($null -eq $webResponse) {
            throw
        }

        $statusCode = [int]$webResponse.StatusCode
        $reader = New-Object System.IO.StreamReader($webResponse.GetResponseStream())
        try {
            $responseBody = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        return [pscustomobject]@{
            StatusCode = $statusCode
            Body = $responseBody
        }
    }
}

function Parse-JsonSafe {
    param([string]$Raw)

    if ([string]::IsNullOrWhiteSpace($Raw)) {
        return $null
    }

    try {
        return $Raw | ConvertFrom-Json
    }
    catch {
        return $null
    }
}

function Compute-GitHubSignature {
    param(
        [string]$Secret,
        [string]$Body
    )

    $keyBytes = [System.Text.Encoding]::UTF8.GetBytes($Secret)
    $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($Body)

    $hmac = New-Object System.Security.Cryptography.HMACSHA256($keyBytes)
    try {
        $hash = $hmac.ComputeHash($bodyBytes)
        $hex = [System.BitConverter]::ToString($hash).Replace('-', '').ToLowerInvariant()
        return "sha256=$hex"
    }
    finally {
        $hmac.Dispose()
    }
}

Write-Host "Running Azure Functions smoke tests against: $baseUrl" -ForegroundColor Cyan
Write-Host "Includes queueing tests: $($IncludeQueueingTests.IsPresent)" -ForegroundColor Cyan

# 1) Health endpoint contract
try {
    $health = Invoke-Endpoint -Method GET -Url (Get-Url -Route '/api/health')
    $healthBody = Parse-JsonSafe -Raw $health.Body
    $validHealthStatus = $null -ne $healthBody -and @('healthy', 'degraded', 'unhealthy') -contains ([string]$healthBody.status).ToLowerInvariant()

    Add-TestResult -Name 'health-status' -Passed (($health.StatusCode -eq 200) -and $validHealthStatus) -Detail "HTTP=$($health.StatusCode), status=$($healthBody.status)"
}
catch {
    Add-TestResult -Name 'health-status' -Passed $false -Detail $_.Exception.Message
}

# 2) Status endpoint contract
try {
    $statusResp = Invoke-Endpoint -Method GET -Url (Get-Url -Route '/api/status')
    $statusBody = Parse-JsonSafe -Raw $statusResp.Body
    $hasExpectedShape = $null -ne $statusBody -and ($null -ne $statusBody.stats)

    Add-TestResult -Name 'status-shape' -Passed (($statusResp.StatusCode -eq 200) -and $hasExpectedShape) -Detail "HTTP=$($statusResp.StatusCode), hasStats=$hasExpectedShape"
}
catch {
    Add-TestResult -Name 'status-shape' -Passed $false -Detail $_.Exception.Message
}

# 3) ADO webhook: empty body -> 400 + error
try {
    $adoWebhookUrl = Get-Url -Route '/api/webhook' -NeedsFunctionKey $true
    $emptyResp = Invoke-Endpoint -Method POST -Url $adoWebhookUrl -Body ''
    $emptyBody = Parse-JsonSafe -Raw $emptyResp.Body
    $emptyErrorMatch = $null -ne $emptyBody -and ([string]$emptyBody.error -eq 'Empty request body')

    Add-TestResult -Name 'ado-webhook-empty-body' -Passed (($emptyResp.StatusCode -eq 400) -and $emptyErrorMatch) -Detail "HTTP=$($emptyResp.StatusCode), error=$($emptyBody.error)"
}
catch {
    Add-TestResult -Name 'ado-webhook-empty-body' -Passed $false -Detail $_.Exception.Message
}

# 4) ADO webhook: invalid json -> 400 + error
try {
    $adoWebhookUrl = Get-Url -Route '/api/webhook' -NeedsFunctionKey $true
    $invalidResp = Invoke-Endpoint -Method POST -Url $adoWebhookUrl -Body '{bad-json'
    $invalidBody = Parse-JsonSafe -Raw $invalidResp.Body
    $invalidErrorMatch = $null -ne $invalidBody -and ([string]$invalidBody.error -eq 'Invalid JSON payload')

    Add-TestResult -Name 'ado-webhook-invalid-json' -Passed (($invalidResp.StatusCode -eq 400) -and $invalidErrorMatch) -Detail "HTTP=$($invalidResp.StatusCode), error=$($invalidBody.error)"
}
catch {
    Add-TestResult -Name 'ado-webhook-invalid-json' -Passed $false -Detail $_.Exception.Message
}

# 5) ADO webhook: non-triggering state -> skipped
try {
    $adoWebhookUrl = Get-Url -Route '/api/webhook' -NeedsFunctionKey $true
    $adoSafePayload = Get-Content -Raw -Path (Join-Path $payloadDir 'ado_non_trigger.json')
    $safeResp = Invoke-Endpoint -Method POST -Url $adoWebhookUrl -Body $adoSafePayload
    $safeBody = Parse-JsonSafe -Raw $safeResp.Body
    $safeSkipped = $null -ne $safeBody -and ([string]$safeBody.status -eq 'skipped')

    Add-TestResult -Name 'ado-webhook-nontrigger-skipped' -Passed (($safeResp.StatusCode -eq 200) -and $safeSkipped) -Detail "HTTP=$($safeResp.StatusCode), status=$($safeBody.status)"
}
catch {
    Add-TestResult -Name 'ado-webhook-nontrigger-skipped' -Passed $false -Detail $_.Exception.Message
}

# 6) Optional ADO webhook triggering test (may enqueue work)
if ($IncludeQueueingTests.IsPresent) {
    try {
        $adoWebhookUrl = Get-Url -Route '/api/webhook' -NeedsFunctionKey $true
        $adoTriggerPayload = Get-Content -Raw -Path (Join-Path $payloadDir 'ado_ai_agent_trigger.json')
        $triggerResp = Invoke-Endpoint -Method POST -Url $adoWebhookUrl -Body $adoTriggerPayload
        $triggerBody = Parse-JsonSafe -Raw $triggerResp.Body
        $isAccepted = $null -ne $triggerBody -and @('queued', 'validation_failed') -contains ([string]$triggerBody.status)

        Add-TestResult -Name 'ado-webhook-triggering-state' -Passed (($triggerResp.StatusCode -eq 200) -and $isAccepted) -Detail "HTTP=$($triggerResp.StatusCode), status=$($triggerBody.status)"
    }
    catch {
        Add-TestResult -Name 'ado-webhook-triggering-state' -Passed $false -Detail $_.Exception.Message
    }
}

# 7) GitHub webhook guardrail: unsigned/empty request should be rejected
try {
    $copilotWebhookUrl = Get-Url -Route '/api/copilot-webhook' -NeedsFunctionKey $true
    $ghEmptyResp = Invoke-Endpoint -Method POST -Url $copilotWebhookUrl -Body ''
    $rejected = @('400', '401') -contains ([string]$ghEmptyResp.StatusCode)
    Add-TestResult -Name 'github-webhook-guardrail' -Passed $rejected -Detail "HTTP=$($ghEmptyResp.StatusCode)"
}
catch {
    Add-TestResult -Name 'github-webhook-guardrail' -Passed $false -Detail $_.Exception.Message
}

# 8) GitHub webhook signature contract (optional strict mode)
if (-not [string]::IsNullOrWhiteSpace($GitHubWebhookSecret)) {
    try {
        $copilotWebhookUrl = Get-Url -Route '/api/copilot-webhook' -NeedsFunctionKey $true
        $ghBody = Get-Content -Raw -Path (Join-Path $payloadDir 'github_pr_ignored.json')
        $signature = Compute-GitHubSignature -Secret $GitHubWebhookSecret -Body $ghBody
        $headers = @{
            'X-GitHub-Event' = 'pull_request'
            'X-Hub-Signature-256' = $signature
        }
        $ghSignedResp = Invoke-Endpoint -Method POST -Url $copilotWebhookUrl -Headers $headers -Body $ghBody

        Add-TestResult -Name 'github-webhook-signed-request' -Passed ($ghSignedResp.StatusCode -eq 200) -Detail "HTTP=$($ghSignedResp.StatusCode)"
    }
    catch {
        Add-TestResult -Name 'github-webhook-signed-request' -Passed $false -Detail $_.Exception.Message
    }
}
else {
    Write-Host "[INFO] Skipping strict GitHub signature test (provide -GitHubWebhookSecret to enable)." -ForegroundColor Yellow
}

Write-Host ''
Write-Host "Smoke test summary: $passedTests/$totalTests passed, $failedTests failed." -ForegroundColor Cyan

if ($failedTests -gt 0) {
    Write-Host "One or more endpoint contracts failed." -ForegroundColor Red
    exit 1
}

exit 0
