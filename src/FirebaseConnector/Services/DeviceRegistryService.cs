using System.Collections.Concurrent;
using FirebaseConnector.Models;

namespace FirebaseConnector.Services;

public interface IDeviceRegistryService
{
    Task RegisterDeviceAsync(DeviceRegistrationRequest request);
    Task DeactivateDeviceAsync(string userId, string deviceToken);
    Task DeactivateTokenAsync(string deviceToken);
    Task<List<UserDevice>> GetActiveDevicesByUserIdAsync(string userId);
    Task<List<UserDevice>> GetAllDevicesAsync();
}

public class DeviceRegistryService : IDeviceRegistryService
{
    private readonly ConcurrentDictionary<string, UserDevice> _devicesByToken = new();
    private readonly ILogger<DeviceRegistryService> _logger;

    public DeviceRegistryService(ILogger<DeviceRegistryService> logger)
    {
        _logger = logger;
    }

    public Task RegisterDeviceAsync(DeviceRegistrationRequest request)
    {
        var cleanUser = request.UserId.Trim();
        if (_devicesByToken.TryGetValue(request.DeviceToken, out var existing))
        {
            existing.UserId = cleanUser;
            existing.AppId = request.AppId;
            existing.DeviceType = request.DeviceType;
            existing.IsActive = true;
            existing.LastLoginAt = DateTime.UtcNow;
            existing.RevokedAt = null;
            _logger.LogInformation("Updated existing device token registration for user {UserId} ({AppId})", cleanUser, request.AppId);
        }
        else
        {
            var device = new UserDevice
            {
                UserId = cleanUser,
                AppId = request.AppId,
                DeviceToken = request.DeviceToken,
                DeviceType = request.DeviceType,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
            _devicesByToken[request.DeviceToken] = device;
            _logger.LogInformation("Registered new device token for user {UserId} ({AppId})", cleanUser, request.AppId);
        }

        return Task.CompletedTask;
    }

    public Task DeactivateDeviceAsync(string userId, string deviceToken)
    {
        if (_devicesByToken.TryGetValue(deviceToken, out var device) && device.UserId == userId)
        {
            device.IsActive = false;
            device.RevokedAt = DateTime.UtcNow;
            _logger.LogInformation("Deactivated device {Token} for user {UserId}", deviceToken, userId);
        }
        return Task.CompletedTask;
    }

    public Task DeactivateTokenAsync(string deviceToken)
    {
        if (_devicesByToken.TryGetValue(deviceToken, out var device))
        {
            device.IsActive = false;
            device.RevokedAt = DateTime.UtcNow;
            _logger.LogWarning("Marked token {Token} as inactive (unregistered by FCM)", deviceToken);
        }
        return Task.CompletedTask;
    }

    public Task<List<UserDevice>> GetActiveDevicesByUserIdAsync(string userId)
    {
        var targetUser = userId.Trim();
        var active = _devicesByToken.Values
            .Where(d => string.Equals(d.UserId?.Trim(), targetUser, StringComparison.OrdinalIgnoreCase) && d.IsActive)
            .ToList();
        return Task.FromResult(active);
    }

    public Task<List<UserDevice>> GetAllDevicesAsync()
    {
        return Task.FromResult(_devicesByToken.Values.ToList());
    }
}
