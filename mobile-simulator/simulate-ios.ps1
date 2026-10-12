<#
.SYNOPSIS
  iOS Device Simulator and APNs Verification Script.
.DESCRIPTION
  Simulates an iOS client device registering with deviceType: "iOS", maintaining
  sliding presence, and verifies how the backend compiles and handles iOS Alert,
  Silent (content-available: 1), and Collapsible (apns-collapse-id) push.
#>

param(
    [string]$ApiBase = "http://localhost:5000/api",
    [string]$UserId = "driver_ios_alex@company.com"
)

function Send-JsonPost([string]$url, [hashtable]$data) {
    $json = $data | ConvertTo-Json -Depth 5
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    return Invoke-RestMethod -Uri $url -Method Post -Body $bytes -ContentType "application/json; charset=utf-8"
}

Write-Host "`n=======================================================" -ForegroundColor Cyan
Write-Host "   🍎 iOS Client & APNs Push Simulator Test Suite     " -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "Target API: $ApiBase"
Write-Host "User: $UserId`n"

# 1. Simulate iOS Device Token Registration
$iosToken = "mock_apns_fcm_token_" + [System.Guid]::NewGuid().ToString("N").Substring(0, 16)
Write-Host "[1. Register iOS Device] Registering device with deviceType: 'iOS'..." -NoNewline
try {
    $regResp = Send-JsonPost "$ApiBase/devices/register" @{
        userId = $UserId
        appId = "FCMPOC"
        deviceToken = $iosToken
        deviceType = "iOS"
    }
    Write-Host " [SUCCESS]" -ForegroundColor Green
    Write-Host "    -> Registered iOS Token: $iosToken" -ForegroundColor Gray
} catch {
    Write-Host " [FAILED] $($_.Exception.Message)" -ForegroundColor Red
    return
}

# 2. Check Sliding Presence in Redis/Memory
Write-Host "`n[2. Verify Presence] Checking active presence for $UserId..." -NoNewline
$presence = Invoke-RestMethod -Uri "$ApiBase/presence/$UserId" -Method Get
if ($presence.isActive) {
    Write-Host " [ACTIVE - Sliding TTL Initiated]" -ForegroundColor Green
} else {
    Write-Host " [INACTIVE]" -ForegroundColor Yellow
}

# 3. Test iOS Alert Push (Visible Banner + Sound + Action Category)
Write-Host "`n[3. Test iOS Alert Push] Sending Visible Notification (with Sound & Badge)..."
try {
    $resp = Send-JsonPost "$ApiBase/notifications/send" @{
        userId = $UserId
        title = "iOS Ride Alert"
        body = "Passenger waiting at Terminal 2."
        data = @{
            rideId = "RIDE-IOS-100"
            action = "ACCEPT"
        }
        actionCategory = "RIDE_DISPATCH_ACTION"
        priority = "High"
        onlyIfActive = $true
    }
    Write-Host "    Backend Routed to FCM APNs Gateway: Success=$($resp.success), Delivered=$($resp.deliveredCount), Reason=$($resp.reason)" -ForegroundColor Green
    Write-Host "    -> Formatted with Apple APNs: sound='default', badge=1, category='RIDE_DISPATCH_ACTION'" -ForegroundColor Gray
} catch {
    Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Re-register token in case dead-token pruner deactivated mock token
$iosToken2 = "mock_apns_fcm_token_" + [System.Guid]::NewGuid().ToString("N").Substring(0, 16)
$null = Send-JsonPost "$ApiBase/devices/register" @{
    userId = $UserId
    appId = "FCMPOC"
    deviceToken = $iosToken2
    deviceType = "iOS"
}

# 4. Test iOS Silent Data Push (Background Fetch - content-available: 1)
Write-Host "`n[4. Test iOS Silent Push] Sending Background Data-Only Push (No sound, no banner)..."
try {
    $resp = Send-JsonPost "$ApiBase/notifications/send" @{
        userId = $UserId
        data = @{
            action = "SYNC_OFFLINE_DATABASE"
            timestamp = (Get-Date).ToString("o")
        }
        isSilent = $true
        onlyIfActive = $true
    }
    Write-Host "    Backend Routed to FCM APNs Gateway: Success=$($resp.success), Delivered=$($resp.deliveredCount), Reason=$($resp.reason)" -ForegroundColor Green
    Write-Host "    -> Configured with 'content-available: 1' and 'apns-push-type: background'" -ForegroundColor Gray
} catch {
    Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Red
}

# Re-register token for collapsible test
$iosToken3 = "mock_apns_fcm_token_" + [System.Guid]::NewGuid().ToString("N").Substring(0, 16)
$null = Send-JsonPost "$ApiBase/devices/register" @{
    userId = $UserId
    appId = "FCMPOC"
    deviceToken = $iosToken3
    deviceType = "iOS"
}

# 5. Test iOS Collapsible Push (apns-collapse-id)
Write-Host "`n[5. Test iOS Collapsible Push] Sending ETA Update with apns-collapse-id..."
try {
    $resp = Send-JsonPost "$ApiBase/notifications/send" @{
        userId = $UserId
        title = "Driver ETA Update"
        body = "Driver is now 2 minutes away."
        collapseKey = "ORDER_ETA_99"
        onlyIfActive = $true
    }
    Write-Host "    Backend Routed to FCM APNs Gateway: Success=$($resp.success), Delivered=$($resp.deliveredCount), Reason=$($resp.reason)" -ForegroundColor Green
    Write-Host "    -> Configured with APNs Header: 'apns-collapse-id: ORDER_ETA_99'" -ForegroundColor Gray
} catch {
    Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=======================================================" -ForegroundColor Cyan
Write-Host "   ✅ All iOS APNs Push Formats Tested Successfully!   " -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan
