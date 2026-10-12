<#
.SYNOPSIS
  Headless Mock Mobile Device Simulator for testing Firebase Connector API.
.DESCRIPTION
  Simulates N mobile devices logging in, registering tokens, sending heartbeats, and logging out.
#>

param(
    [string]$ApiBase = "http://localhost:5000/api",
    [int]$DeviceCount = 3,
    [int]$HeartbeatCycles = 3
)

Write-Host "`n=== [MOBILE SIMULATOR] Firebase Connector Device Test Suite ===" -ForegroundColor Cyan
Write-Host "Target API: $ApiBase"
Write-Host "Simulating $DeviceCount devices...`n"

$devices = @()

# 1. Register Devices
for ($i = 1; $i -le $DeviceCount; $i++) {
    $email = "user_$i@company.com"
    $appId = if ($i % 2 -eq 0) { "DriverApp" } else { "CustomerApp" }
    $token = "mock_fcm_token_" + [System.Guid]::NewGuid().ToString("N").Substring(0, 16)

    $payload = @{
        userId = $email
        appId = $appId
        deviceToken = $token
        deviceType = "MockSimulator"
    } | ConvertTo-Json

    Write-Host "[Register] Device $i ($email - $appId)..." -NoNewline
    try {
        $resp = Invoke-RestMethod -Uri "$ApiBase/devices/register" -Method Post -Body $payload -ContentType "application/json" -TimeoutSec 3 -ErrorAction Stop
        Write-Host " [SUCCESS]" -ForegroundColor Green
    } catch {
        Write-Host " [SKIPPED - API not running or returned error]" -ForegroundColor Yellow
    }

    $devices += [PSCustomObject]@{
        Email = $email
        AppId = $appId
        Token = $token
    }
}

# 2. Simulate Heartbeats
for ($cycle = 1; $cycle -le $HeartbeatCycles; $cycle++) {
    Write-Host "`n[Heartbeat] Running Heartbeat Cycle $cycle of $HeartbeatCycles (Sliding TTL)..." -ForegroundColor Magenta
    foreach ($d in $devices) {
        $heartbeat = @{
            userId = $d.Email
            deviceId = $d.Token
        } | ConvertTo-Json

        Write-Host "  -> Heartbeat for $($d.Email)..." -NoNewline
        try {
            Invoke-RestMethod -Uri "$ApiBase/presence/heartbeat" -Method Post -Body $heartbeat -ContentType "application/json" -TimeoutSec 3 -ErrorAction Stop
            Write-Host " [OK (TTL Extended)]" -ForegroundColor Green
        } catch {
            Write-Host " [FAIL]" -ForegroundColor Red
        }
    }
    if ($cycle -lt $HeartbeatCycles) {
        Write-Host "Waiting 5 seconds before next cycle..."
        Start-Sleep -Seconds 5
    }
}

# 3. Simulate Logout for 1 device
$loggedOut = $devices[0]
Write-Host "`n[Logout] Simulating Logout for $($loggedOut.Email)..." -ForegroundColor Yellow
$logoutPayload = @{
    userId = $loggedOut.Email
    deviceToken = $loggedOut.Token
} | ConvertTo-Json

try {
    Invoke-RestMethod -Uri "$ApiBase/devices/logout" -Method Post -Body $logoutPayload -ContentType "application/json" -TimeoutSec 3 -ErrorAction Stop
    Write-Host "Token deactivated and presence purged for $($loggedOut.Email)! [OK]" -ForegroundColor Green
} catch {
    Write-Host "Failed to call logout API." -ForegroundColor Red
}

Write-Host "`n=== Simulation Complete ===`n" -ForegroundColor Cyan
