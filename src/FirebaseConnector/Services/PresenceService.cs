using System.Collections.Concurrent;
using StackExchange.Redis;

namespace FirebaseConnector.Services;

public interface IPresenceService
{
    Task SetActiveAsync(string userId, string deviceId, TimeSpan ttl);
    Task SetInactiveAsync(string userId, string deviceId);
    Task<bool> IsUserActiveAsync(string userId);
    Task<IEnumerable<string>> GetActiveDevicesAsync(string userId);
}

public class PresenceService : IPresenceService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<PresenceService> _logger;

    // In-memory fallback if Redis is unavailable
    private readonly ConcurrentDictionary<string, (DateTime Expiry, string DeviceId)> _memoryStore = new();

    public PresenceService(IConfiguration config, ILogger<PresenceService> logger)
    {
        _logger = logger;
        var redisConn = config.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConn))
        {
            try
            {
                var options = ConfigurationOptions.Parse(redisConn);
                options.AbortOnConnectFail = false;
                options.ConnectTimeout = 3000;
                _redis = ConnectionMultiplexer.Connect(options);
                _logger.LogInformation("✅ Connected to Redis at {Host} for Presence Tracking.", redisConn);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Could not connect to Redis. Using High-Performance In-Memory Presence Fallback.");
            }
        }
        else
        {
            _logger.LogInformation("ℹ️ No Redis connection string provided. Using In-Memory Presence Store.");
        }
    }

    public async Task SetActiveAsync(string userId, string deviceId, TimeSpan ttl)
    {
        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            var deviceKey = $"presence:user:{userId}:device:{deviceId}";
            var userDevicesKey = $"presence:user:{userId}:devices";

            await db.StringSetAsync(deviceKey, "active", ttl);
            await db.SetAddAsync(userDevicesKey, deviceId);
            await db.KeyExpireAsync(userDevicesKey, TimeSpan.FromMinutes(15));
            return;
        }

        // Memory fallback
        var key = $"{userId}:{deviceId}";
        _memoryStore[key] = (DateTime.UtcNow.Add(ttl), deviceId);
    }

    public async Task SetInactiveAsync(string userId, string deviceId)
    {
        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            var deviceKey = $"presence:user:{userId}:device:{deviceId}";
            var userDevicesKey = $"presence:user:{userId}:devices";

            await db.KeyDeleteAsync(deviceKey);
            await db.SetRemoveAsync(userDevicesKey, deviceId);
            return;
        }

        var key = $"{userId}:{deviceId}";
        _memoryStore.TryRemove(key, out _);
    }

    public async Task<bool> IsUserActiveAsync(string userId)
    {
        var devices = await GetActiveDevicesAsync(userId);
        return devices.Any();
    }

    public async Task<IEnumerable<string>> GetActiveDevicesAsync(string userId)
    {
        if (_redis != null && _redis.IsConnected)
        {
            var db = _redis.GetDatabase();
            var userDevicesKey = $"presence:user:{userId}:devices";
            var deviceIds = await db.SetMembersAsync(userDevicesKey);

            var active = new List<string>();
            foreach (var id in deviceIds)
            {
                var deviceId = id.ToString();
                var key = $"presence:user:{userId}:device:{deviceId}";
                if (await db.KeyExistsAsync(key))
                {
                    active.Add(deviceId);
                }
            }
            return active;
        }

        // Memory fallback
        var now = DateTime.UtcNow;
        var prefix = $"{userId}:";
        return _memoryStore
            .Where(kvp => kvp.Key.StartsWith(prefix) && kvp.Value.Expiry > now)
            .Select(kvp => kvp.Value.DeviceId)
            .ToList();
    }
}
