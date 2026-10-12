namespace FirebaseConnector.Models;

public class UserDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = default!;
    public string AppId { get; set; } = default!; // e.g., "CustomerApp", "DriverApp"
    public string DeviceToken { get; set; } = default!;
    public string DeviceType { get; set; } = default!; // "Android", "iOS", "Web"
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
}

public class DeviceRegistrationRequest
{
    public string UserId { get; set; } = default!;
    public string AppId { get; set; } = "CustomerApp";
    public string DeviceToken { get; set; } = default!;
    public string DeviceType { get; set; } = "Android";
}

public class HeartbeatRequest
{
    public string UserId { get; set; } = default!;
    public string DeviceId { get; set; } = default!;
}

public class LogoutRequest
{
    public string UserId { get; set; } = default!;
    public string DeviceToken { get; set; } = default!;
}

public enum NotificationChannelPriority
{
    Normal,
    High,
    Critical
}

public class NotificationSendRequest
{
    // --- TARGETING (Choose one: UserId, DeviceToken, Topic, or Condition) ---
    /// <summary>Target a User ID (resolves active devices with sliding presence).</summary>
    public string? UserId { get; set; }

    /// <summary>Target a single device registration token directly (Unicast).</summary>
    public string? DeviceToken { get; set; }

    /// <summary>Broadcast to an FCM topic name (e.g. "announcements", "drivers_london").</summary>
    public string? Topic { get; set; }

    /// <summary>Broadcast to a boolean topic condition (e.g. "'drivers' in topics && 'london' in topics").</summary>
    public string? Condition { get; set; }

    /// <summary>Target App family / Firebase project (defaults to "FCMPOC" or user's registered app).</summary>
    public string? AppId { get; set; }

    // --- CONTENT & MEDIA ---
    public string? Title { get; set; }
    public string? Body { get; set; }
    /// <summary>Rich Media / Banner image URL (rendered as BigPicture on Android, rich thumbnail on iOS & Web).</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Custom metadata key-value pairs delivered to client application code.</summary>
    public Dictionary<string, string>? Data { get; set; }

    // --- BEHAVIOR & INTERACTION FLAGS ---
    /// <summary>If true, sends a silent data-only push without visual alert, sound, or banner.</summary>
    public bool IsSilent { get; set; } = false;

    /// <summary>If true (and UserId is set), sends only if user is currently active within sliding 90s window.</summary>
    public bool OnlyIfActive { get; set; } = true;

    /// <summary>Collapse key. Replaces older notifications with the same key (e.g., live ride ETA updates).</summary>
    public string? CollapseKey { get; set; }

    /// <summary>Interactive action category identifier (e.g., "ACCEPT_RIDE", "VIEW_ORDER").</summary>
    public string? ActionCategory { get; set; }

    /// <summary>Priority level: Normal, High, or Critical (Time-sensitive, bypasses DND).</summary>
    public NotificationChannelPriority Priority { get; set; } = NotificationChannelPriority.High;
}

public class TopicSubscriptionRequest
{
    public string Topic { get; set; } = default!;
    public List<string> DeviceTokens { get; set; } = new();
    public string? AppId { get; set; } = "FCMPOC";
}

public class TopicActionResponse
{
    public bool Success { get; set; }
    public string Topic { get; set; } = default!;
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class DeliveryResponse
{
    public bool Success { get; set; }
    public string? Reason { get; set; }
    public int DeliveredCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
