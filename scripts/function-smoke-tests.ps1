param(
    [Parameter(Mandatory = $false)]
    [string]$FunctionBaseUrl = $env:ADOM8_FUNCTION_URL,

    [Parameter(Mandatory = $false)]
    [string]$FunctionKey = $env:ADOM8_FUNCTION_KEY,

    [Parameter(Mandatory = $false)]
    [string]$CopilotWebhookSecret = $env:COPILOT_WEBHOOK_SECRET,

    [Parameter(Mandatory = $false)]
    [int]$TriggerWorkItemId = 0,

    [switch]$IncludeTriggerSimulation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($FunctionBaseUrl)) {
    throw "FunctionBaseUrl is required. Pass -FunctionBaseUrl or set ADOM8_FUNCTION_URL. Example: https://your-func.azurewebsites.net"
}

$FunctionBaseUrl = $FunctionBaseUrl.TrimEnd('/')

function New-TestResult {
    param(
        [string]$Name,
        [bool]$Passed,
        [int]$StatusCode,
        [string]$Message
    )

    [pscustomobject]@{
        Test       = $Name
        Passed     = $Passed
        StatusCode = $StatusCode
        Message    = $Message
    }
}

function Build-Url {
    param([string]$Path)

    $url = "$FunctionBaseUrl$Path"
    if ([string]::IsNullOrWhiteSpace($FunctionKey)) {
        return $url
    }

    if ($url.Contains('?')) {
        return "$url&code=$([uri]::EscapeDataString($FunctionKey))"
    }

    return "$url?code=$([uri]::EscapeDataString($FunctionKey))"
}

function Invoke-Http {
    param(
        [ValidateSet('GET', 'POST')]
        [string]$Method,
        [string]$Path,
        [string]$Body = '',
        [hashtable]$Headers = @{}
    )

    $url = Build-Url -Path $Path
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::$Method, $url)

    foreach ($headerName in $Headers.Keys) {
        $request.Headers.TryAddWithoutValidation($headerName, [string]$Headers[$headerName]) | Out-Null
    }

    if ($Method -eq 'POST') {
        $request.Content = [System.Net.Http.StringContent]::new($Body, [System.Text.Encoding]::UTF8, 'application/json')
    }

    $response = $script:HttpClient.SendAsync($request).GetAwaiter().GetResult()
    $responseText = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()

    $json = $null
    if (-not [string]::IsNullOrWhiteSpace($responseText)) {
        try { $json = $responseText | ConvertFrom-Json -ErrorAction Stop } catch { }
    }

    return [pscustomobject]@{
        StatusCode = [int]$response.StatusCode
        Body       = $responseText
        Json       = $json
        Url        = $url
    }
}

function Get-HmacSha256Signature {
    param(
        [string]$Secret,
        [string]$Body
    )

    $secretBytes = [System.Text.Encoding]::UTF8.GetBytes($Secret)
    $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($Body)

    $hmac = [System.Security.Cryptography.HMACSHA256]::new($secretBytes)
    try {
        $hashBytes = $hmac.ComputeHash($bodyBytes)
        $hex = [System.BitConverter]::ToString($hashBytes).Replace('-', '').ToLowerInvariant()
        return "sha256=$hex"
    }
    finally {
        $hmac.Dispose()
    }
}

$handler = [System.Net.Http.HttpClientHandler]::new()
$script:HttpClient = [System.Net.Http.HttpClient]::new($handler)
$script:HttpClient.Timeout = [TimeSpan]::FromSeconds(30)

$results = New-Object System.Collections.Generic.List[object]

try {
    Write-Host "Running function smoke tests against: $FunctionBaseUrl" -ForegroundColor Cyan

    # 1) Health
    $health = Invoke-Http -Method GET -Path '/api/health'
    $healthPass = $health.StatusCode -eq 200 -and $null -ne $health.Json -and $null -ne $health.Json.status
    $results.Add((New-TestResult -Name 'GET /api/health' -Passed $healthPass -StatusCode $health.StatusCode -Message ($health.Body.Substring(0, [Math]::Min($health.Body.Length, 180)))))

    # 2) Status
    $status = Invoke-Http -Method GET -Path '/api/status'
    $statusPass = $status.StatusCode -eq 200
    $results.Add((New-TestResult -Name 'GET /api/status' -Passed $statusPass -StatusCode $status.StatusCode -Message ($status.Body.Substring(0, [Math]::Min($status.Body.Length, 180)))))

    # 3) ADO webhook invalid JSON
    $invalidWebhook = Invoke-Http -Method POST -Path '/api/webhook' -Body '{bad json}'
    $invalidWebhookPass = $invalidWebhook.StatusCode -eq 400 -and $invalidWebhook.Body -match 'Invalid JSON payload'
    $results.Add((New-TestResult -Name 'POST /api/webhook invalid JSON' -Passed $invalidWebhookPass -StatusCode $invalidWebhook.StatusCode -Message ($invalidWebhook.Body.Substring(0, [Math]::Min($invalidWebhook.Body.Length, 180)))))

    # 4) ADO webhook non-trigger state (safe)
    $nonTriggerPayload = @{
        eventType = 'workitem.updated'
        resource  = @{
            id         = 101
            workItemId = 101
            fields     = @{
                'System.State' = @{
                    oldValue = 'New'
                    newValue = 'Backlog'
                }
            }
        }
    } | ConvertTo-Json -Depth 8

    $nonTrigger = Invoke-Http -Method POST -Path '/api/webhook' -Body $nonTriggerPayload
    $nonTriggerPass = $nonTrigger.StatusCode -eq 200 -and $null -ne $nonTrigger.Json -and $nonTrigger.Json.status -eq 'skipped'
    $results.Add((New-TestResult -Name "POST /api/webhook non-trigger state" -Passed $nonTriggerPass -StatusCode $nonTrigger.StatusCode -Message ($nonTrigger.Body.Substring(0, [Math]::Min($nonTrigger.Body.Length, 180)))))

    # 5) Optional: ADO webhook trigger simulation
    if ($IncludeTriggerSimulation) {
        if ($TriggerWorkItemId -le 0) {
            throw 'IncludeTriggerSimulation requires -TriggerWorkItemId > 0'
        }

        $triggerPayload = @{
            eventType = 'workitem.updated'
            resource  = @{
                id         = $TriggerWorkItemId
                workItemId = $TriggerWorkItemId
                fields     = @{
                    'System.State' = @{
                        oldValue = 'New'
                        newValue = 'AI Agent'
                    }
                }
            }
        } | ConvertTo-Json -Depth 8

        $trigger = Invoke-Http -Method POST -Path '/api/webhook' -Body $triggerPayload
        $acceptedStatuses = @('queued', 'validation_failed', 'skipped')
        $triggerStatus = if ($trigger.Json -and $trigger.Json.status) { [string]$trigger.Json.status } else { '' }
        $triggerPass = $trigger.StatusCode -eq 200 -and $acceptedStatuses -contains $triggerStatus
        $results.Add((New-TestResult -Name "POST /api/webhook trigger simulation (WI-$TriggerWorkItemId)" -Passed $triggerPass -StatusCode $trigger.StatusCode -Message ($trigger.Body.Substring(0, [Math]::Min($trigger.Body.Length, 180)))))
    }

    # 6) GitHub webhook pull_request payload
    $copilotPayloadObject = @{
        action       = 'opened'
        pull_request = @{
            number              = 99999
            title               = '[US-12345] Smoke test PR'
            body                = 'Generated for webhook smoke test'
            draft               = $true
            requested_reviewers = @()
            head                = @{ ref = 'copilot/smoke-test' }
            base                = @{ ref = 'feature/US-12345' }
        }
    }
    $copilotPayload = $copilotPayloadObject | ConvertTo-Json -Depth 10

    $copilotHeaders = @{ 'X-GitHub-Event' = 'pull_request' }
    if (-not [string]::IsNullOrWhiteSpace($CopilotWebhookSecret)) {
        $copilotHeaders['X-Hub-Signature-256'] = Get-HmacSha256Signature -Secret $CopilotWebhookSecret -Body $copilotPayload
    }

    $copilot = Invoke-Http -Method POST -Path '/api/copilot-webhook' -Body $copilotPayload -Headers $copilotHeaders

    $copilotPass = $false
    if ($copilot.StatusCode -eq 200) {
        $copilotPass = $true
    }
    elseif ($copilot.StatusCode -eq 401 -and [string]::IsNullOrWhiteSpace($CopilotWebhookSecret)) {
        # Signature validation is enforced in this environment and secret wasn't supplied.
        $copilotPass = $true
    }

    $results.Add((New-TestResult -Name 'POST /api/copilot-webhook pull_request' -Passed $copilotPass -StatusCode $copilot.StatusCode -Message ($copilot.Body.Substring(0, [Math]::Min($copilot.Body.Length, 180)))))

    $results | Format-Table -AutoSize

    $failed = @($results | Where-Object { -not $_.Passed })
    if ($failed.Count -gt 0) {
        Write-Host "`nSmoke tests failed: $($failed.Count)" -ForegroundColor Red
        exit 1
    }

    Write-Host "`nAll smoke tests passed." -ForegroundColor Green
    exit 0
}
finally {
    if ($null -ne $script:HttpClient) {
        $script:HttpClient.Dispose()
    }
}
