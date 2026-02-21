$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$logsDir = Join-Path $root ".tmp\\e2e-logs"
New-Item -ItemType Directory -Path $logsDir -Force | Out-Null

$apiStdOut = Join-Path $logsDir "api.stdout.log"
$apiStdErr = Join-Path $logsDir "api.stderr.log"
$connectorStdOut = Join-Path $logsDir "connector.stdout.log"
$connectorStdErr = Join-Path $logsDir "connector.stderr.log"
$webStdOut = Join-Path $logsDir "web.stdout.log"
$webStdErr = Join-Path $logsDir "web.stderr.log"

$cloudSecret = "e2e-cloud-secret"
$eaSecret = "e2e-ea-secret"
$sourceId = "e2e-source-01"

function Start-ServiceProcess {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][hashtable]$EnvironmentValues,
        [Parameter(Mandatory = $true)][string]$StdOutPath,
        [Parameter(Mandatory = $true)][string]$StdErrPath
    )

    $envLines = @()
    foreach ($kv in $EnvironmentValues.GetEnumerator()) {
        $escapedValue = $kv.Value.Replace("'", "''")
        $envLines += "[System.Environment]::SetEnvironmentVariable('$($kv.Key)', '$escapedValue', 'Process')"
    }

    $projectRelative = $ProjectPath.Replace("\", "/")
    $command = @(
        '$ErrorActionPreference = ''Stop'''
        $envLines
        "Set-Location '$($root.Replace("'", "''"))'"
        "dotnet run --project '$projectRelative'"
    ) -join "; "

    $process = Start-Process -FilePath "pwsh" `
        -ArgumentList @("-NoLogo", "-NoProfile", "-Command", $command) `
        -WorkingDirectory $root `
        -PassThru `
        -RedirectStandardOutput $StdOutPath `
        -RedirectStandardError $StdErrPath

    Write-Host "Started $Name (PID=$($process.Id))"
    return $process
}

function Wait-HttpOk {
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [int]$TimeoutSeconds = 90
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -Method Get -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
                return
            }
        }
        catch {
        }
        Start-Sleep -Milliseconds 500
    }

    throw "Timed out waiting for endpoint: $Url"
}

function Get-HmacHex {
    param(
        [Parameter(Mandatory = $true)][string]$Secret,
        [Parameter(Mandatory = $true)][string]$Payload
    )

    $hmac = [System.Security.Cryptography.HMACSHA256]::new([System.Text.Encoding]::UTF8.GetBytes($Secret))
    try {
        $hash = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Payload))
        return -join ($hash | ForEach-Object { $_.ToString("x2") })
    }
    finally {
        $hmac.Dispose()
    }
}

$apiProcess = $null
$connectorProcess = $null
$webProcess = $null
$result = "FAILED"
$failureReason = ""

try {
    $apiProcess = Start-ServiceProcess `
        -Name "API" `
        -ProjectPath "src/TradePilot.Api" `
        -EnvironmentValues @{
            "ASPNETCORE_ENVIRONMENT" = "Development"
            "Security__Hmac__SharedSecret" = $cloudSecret
            "Security__Hmac__SourceSecrets__demo-source-01" = $cloudSecret
        } `
        -StdOutPath $apiStdOut `
        -StdErrPath $apiStdErr

    $connectorProcess = Start-ServiceProcess `
        -Name "Connector" `
        -ProjectPath "src/TradePilot.Connector" `
        -EnvironmentValues @{
            "ASPNETCORE_ENVIRONMENT" = "Development"
            "Connector__CloudApiBaseUrl" = "http://localhost:5261"
            "Security__InboundHmac__SharedSecret" = $eaSecret
            "Security__InboundHmac__SourceSecrets__demo-source-01" = $eaSecret
            "Security__OutboundHmac__SharedSecret" = $cloudSecret
        } `
        -StdOutPath $connectorStdOut `
        -StdErrPath $connectorStdErr

    $webProcess = Start-ServiceProcess `
        -Name "Web" `
        -ProjectPath "src/TradePilot.Web" `
        -EnvironmentValues @{
            "ASPNETCORE_ENVIRONMENT" = "Development"
            "Api__BaseUrl" = "http://localhost:5261"
        } `
        -StdOutPath $webStdOut `
        -StdErrPath $webStdErr

    Wait-HttpOk -Url "http://localhost:5261/health"
    Wait-HttpOk -Url "http://localhost:5138/health"
    Wait-HttpOk -Url "http://localhost:5288/"

    $snapshot = @{
        sourceId = $sourceId
        timestampUtc = [DateTime]::UtcNow.ToString("o")
        account = @{
            broker = "Demo Broker"
            server = "Demo-Server"
            login = 123456
            currency = "USD"
            balance = 10000.0
            equity = 10020.5
            margin = 100.0
            freeMargin = 9920.5
            marginLevel = 10020.5
        }
        positions = @()
        orders = @()
    }

    $body = $snapshot | ConvertTo-Json -Depth 5 -Compress
    $timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString()
    $nonce = [Guid]::NewGuid().ToString("N")
    $signature = Get-HmacHex -Secret $eaSecret -Payload "$timestamp.$nonce.$body"

    $headers = @{
        "X-Source-Id" = $sourceId
        "X-Timestamp" = $timestamp
        "X-Nonce" = $nonce
        "X-Signature" = $signature
    }

    $httpClient = [System.Net.Http.HttpClient]::new()
    try {
        $requestMessage = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, "http://localhost:5138/ingest/snapshot")
        $requestMessage.Content = [System.Net.Http.StringContent]::new($body, [System.Text.Encoding]::UTF8, "application/json")
        foreach ($header in $headers.GetEnumerator()) {
            [void]$requestMessage.Headers.TryAddWithoutValidation($header.Key, $header.Value)
        }

        $ingestResponse = $httpClient.SendAsync($requestMessage).GetAwaiter().GetResult()
        if ([int]$ingestResponse.StatusCode -ne 202) {
            $errorBody = $ingestResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            throw "Connector ingest did not return 202. Actual: $([int]$ingestResponse.StatusCode). Body: $errorBody"
        }
    }
    finally {
        $httpClient.Dispose()
    }

    Start-Sleep -Seconds 2

    $sources = Invoke-RestMethod -Uri "http://localhost:5261/v1/mt/sources" -Method Get
    if (-not ($sources | Where-Object { $_.sourceId -eq $sourceId })) {
        throw "Source '$sourceId' not found in API sources response."
    }

    $latest = Invoke-RestMethod -Uri "http://localhost:5261/v1/mt/sources/$sourceId/latest" -Method Get
    if ($latest.sourceId -ne $sourceId) {
        throw "Latest snapshot sourceId mismatch. Expected '$sourceId' but got '$($latest.sourceId)'."
    }

    $dashboardResponse = Invoke-WebRequest -Uri "http://localhost:5288/dashboard/$sourceId" -Method Get -UseBasicParsing
    if ($dashboardResponse.StatusCode -ne 200) {
        throw "Web dashboard route returned unexpected status code: $($dashboardResponse.StatusCode)"
    }

    $result = "PASSED"
}
catch {
    $failureReason = $_.Exception.Message
    Write-Host "E2E test failed: $failureReason"
    throw
}
finally {
    foreach ($proc in @($webProcess, $connectorProcess, $apiProcess)) {
        if ($null -ne $proc -and -not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force
        }
    }

    $reportPath = Join-Path $root "docs\\e2e-local-test-results.md"
    $timestampUtc = [DateTime]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
    $lines = @(
        "# Local E2E Test Result"
        ""
        "Run timestamp: $timestampUtc"
        ""
        "Result: $result"
        ""
        "Checks:"
        "- API health reachable"
        "- Connector health reachable"
        "- Web UI reachable"
        "- Signed snapshot accepted by connector"
        "- Source appears in API sources endpoint"
        "- Latest snapshot endpoint returns expected source"
        "- Dashboard route responds successfully"
        ""
        "Logs:"
        "- .tmp/e2e-logs/api.stdout.log"
        "- .tmp/e2e-logs/api.stderr.log"
        "- .tmp/e2e-logs/connector.stdout.log"
        "- .tmp/e2e-logs/connector.stderr.log"
        "- .tmp/e2e-logs/web.stdout.log"
        "- .tmp/e2e-logs/web.stderr.log"
    )

    if ($result -eq "FAILED" -and $failureReason.Length -gt 0) {
        $lines += ""
        $lines += "Failure reason: $failureReason"
    }

    Set-Content -Path $reportPath -Value $lines
}
