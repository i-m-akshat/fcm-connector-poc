using FirebaseAdmin.Messaging;
using FirebaseConnector.Models;

namespace FirebaseConnector.Services;

public interface INotificationDeliveryRouter
{
    Task<DeliveryResponse> DispatchAsync(NotificationSendRequest request);
    Task<TopicActionResponse> SubscribeToTopicAsync(string topic, IEnumerable<string> tokens, string? appId = null);
    Task<TopicActionResponse> UnsubscribeFromTopicAsync(string topic, IEnumerable<string> tokens, string? appId = null);
}

public class NotificationDeliveryRouter : INotificationDeliveryRouter
{
    private readonly IPresenceService _presenceService;
    private readonly IDeviceRegistryService _deviceRegistry;
    private readonly IFirebaseAppManager _firebaseAppManager;
    private readonly ILogger<NotificationDeliveryRouter> _logger;

    public NotificationDeliveryRouter(
        IPresenceService presenceService,
        IDeviceRegistryService deviceRegistry,
        IFirebaseAppManager firebaseAppManager,
        ILogger<NotificationDeliveryRouter> logger)
    {
        _presenceService = presenceService;
        _deviceRegistry = deviceRegistry;
        _firebaseAppManager = firebaseAppManager;
        _logger = logger;
    }

    public async Task<DeliveryResponse> DispatchAsync(NotificationSendRequest request)
    {
        // 1. Topic Broadcast
        if (!string.IsNullOrWhiteSpace(request.Topic))
        {
            return await DispatchToTopicAsync(request);
        }

        // 2. Boolean Condition Broadcast
        if (!string.IsNullOrWhiteSpace(request.Condition))
        {
            return await DispatchToConditionAsync(request);
        }

        // 3. Direct Device Token (Unicast)
        if (!string.IsNullOrWhiteSpace(request.DeviceToken))
        {
            return await DispatchToDirectTokenAsync(request);
        }

        // 4. User ID Target (Multicast with Active Presence Tracking)
        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            return await DispatchToUserAsync(request);
        }

        return new DeliveryResponse
        {
            Success = false,
            Reason = "NoTargetSpecified",
            Errors = new List<string> { "Specify UserId, DeviceToken, Topic, or Condition." }
        };
    }

    private async Task<DeliveryResponse> DispatchToTopicAsync(NotificationSendRequest request)
    {
        var appId = request.AppId ?? "FCMPOC";
        var isMock = _firebaseAppManager.IsMockMode(appId);
        var response = new DeliveryResponse();

        if (isMock)
        {
            _logger.LogInformation("🧪 [MOCK TOPIC DISPATCH] App: '{AppId}', Topic: '{Topic}', Title: '{Title}'",
                appId, request.Topic, request.Title);
            response.Success = true;
            response.DeliveredCount = 1;
            response.Reason = $"MockBroadcastToTopic:{request.Topic}";
            return response;
        }

        var client = _firebaseAppManager.GetMessagingClient(appId);
        if (client == null)
        {
            response.Success = false;
            response.Reason = $"NoClientForApp:{appId}";
            return response;
        }

        var (notif, android, apns, webpush) = BuildPayload(request);
        var msg = new Message
        {
            Topic = request.Topic,
            Notification = notif,
            Android = android,
            Apns = apns,
            Webpush = webpush,
            Data = request.Data ?? new Dictionary<string, string>()
        };

        try
        {
            var msgId = await client.SendAsync(msg);
            _logger.LogInformation("✅ FCM Topic Broadcast '{Topic}' succeeded with MsgId: {MsgId}", request.Topic, msgId);
            response.Success = true;
            response.DeliveredCount = 1;
            response.Reason = $"BroadcastToTopic:{request.Topic}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast to topic '{Topic}'", request.Topic);
            response.Success = false;
            response.FailedCount = 1;
            response.Errors.Add(ex.Message);
            response.Reason = "TopicBroadcastFailed";
        }

        return response;
    }

    private async Task<DeliveryResponse> DispatchToConditionAsync(NotificationSendRequest request)
    {
        var appId = request.AppId ?? "FCMPOC";
        var isMock = _firebaseAppManager.IsMockMode(appId);
        var response = new DeliveryResponse();

        if (isMock)
        {
            _logger.LogInformation("🧪 [MOCK CONDITION DISPATCH] App: '{AppId}', Condition: '{Condition}', Title: '{Title}'",
                appId, request.Condition, request.Title);
            response.Success = true;
            response.DeliveredCount = 1;
            response.Reason = $"MockBroadcastToCondition:{request.Condition}";
            return response;
        }

        var client = _firebaseAppManager.GetMessagingClient(appId);
        if (client == null)
        {
            response.Success = false;
            response.Reason = $"NoClientForApp:{appId}";
            return response;
        }

        var (notif, android, apns, webpush) = BuildPayload(request);
        var msg = new Message
        {
            Condition = request.Condition,
            Notification = notif,
            Android = android,
            Apns = apns,
            Webpush = webpush,
            Data = request.Data ?? new Dictionary<string, string>()
        };

        try
        {
            var msgId = await client.SendAsync(msg);
            _logger.LogInformation("✅ FCM Condition Broadcast '{Condition}' succeeded with MsgId: {MsgId}", request.Condition, msgId);
            response.Success = true;
            response.DeliveredCount = 1;
            response.Reason = $"BroadcastToCondition:{request.Condition}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast to condition '{Condition}'", request.Condition);
            response.Success = false;
            response.FailedCount = 1;
            response.Errors.Add(ex.Message);
            response.Reason = "ConditionBroadcastFailed";
        }

        return response;
    }

    private async Task<DeliveryResponse> DispatchToDirectTokenAsync(NotificationSendRequest request)
    {
        var appId = request.AppId ?? "FCMPOC";
        var isMock = _firebaseAppManager.IsMockMode(appId);
        var response = new DeliveryResponse();

        if (isMock)
        {
            _logger.LogInformation("🧪 [MOCK DIRECT TOKEN DISPATCH] App: '{AppId}', Token: '{Token}', Title: '{Title}'",
                appId, request.DeviceToken, request.Title);
            response.Success = true;
            response.DeliveredCount = 1;
            response.Reason = "MockDirectTokenDelivered";
            return response;
        }

        var client = _firebaseAppManager.GetMessagingClient(appId);
        if (client == null)
        {
            response.Success = false;
            response.Reason = $"NoClientForApp:{appId}";
            return response;
        }

        var (notif, android, apns, webpush) = BuildPayload(request);
        var msg = new Message
        {
            Token = request.DeviceToken,
            Notification = notif,
            Android = android,
            Apns = apns,
            Webpush = webpush,
            Data = request.Data ?? new Dictionary<string, string>()
        };

        try
        {
            var msgId = await client.SendAsync(msg);
            _logger.LogInformation("✅ FCM Direct Token send succeeded with MsgId: {MsgId}", msgId);
            response.Success = true;
            response.DeliveredCount = 1;
            response.Reason = "DirectTokenDelivered";
        }
        catch (FirebaseMessagingException fex)
        {
            _logger.LogWarning("FCM Delivery Error on direct token: {Code} - {Msg}", fex.MessagingErrorCode, fex.Message);
            response.Success = false;
            response.FailedCount = 1;
            response.Reason = "DeliveryFailed";
            response.Errors.Add($"{request.DeviceToken}: {fex.MessagingErrorCode}");
            if (fex.MessagingErrorCode == MessagingErrorCode.Unregistered || fex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
            {
                await _deviceRegistry.DeactivateTokenAsync(request.DeviceToken!);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected exception during direct token send");
            response.Success = false;
            response.FailedCount = 1;
            response.Errors.Add(ex.Message);
            response.Reason = "DeliveryFailed";
        }

        return response;
    }

    private async Task<DeliveryResponse> DispatchToUserAsync(NotificationSendRequest request)
    {
        var response = new DeliveryResponse();
        var userId = request.UserId!;

        // 1. Verify Active Presence Filter (if enabled)
        if (request.OnlyIfActive)
        {
            var isActive = await _presenceService.IsUserActiveAsync(userId);
            if (!isActive)
            {
                _logger.LogInformation("⏭️ Notification skipped for {UserId}: User is not currently active.", userId);
                response.Success = false;
                response.Reason = "UserInactive";
                return response;
            }
        }

        // 2. Fetch Active Devices for the User
        var devices = await _deviceRegistry.GetActiveDevicesByUserIdAsync(userId);
        if (!devices.Any())
        {
            _logger.LogWarning("⚠️ No active registered devices found for user {UserId}.", userId);
            response.Success = false;
            response.Reason = "NoActiveDevicesFound";
            return response;
        }

        // 3. Group by AppId (Multi-App & Multi-Firebase Project Routing)
        var groups = devices.GroupBy(d => d.AppId);

        foreach (var appGroup in groups)
        {
            var appId = appGroup.Key;
            var tokens = appGroup.Select(d => d.DeviceToken).Distinct().ToList();
            var isMock = _firebaseAppManager.IsMockMode(appId);

            if (isMock)
            {
                _logger.LogInformation("🧪 [MOCK DISPATCH] App: '{AppId}', User: '{UserId}', Tokens: {Count}, Title: '{Title}'",
                    appId, userId, tokens.Count, request.Title);
                response.DeliveredCount += tokens.Count;
                continue;
            }

            var messagingClient = _firebaseAppManager.GetMessagingClient(appId);
            if (messagingClient == null)
            {
                _logger.LogWarning("No messaging client available for AppId '{AppId}'.", appId);
                response.FailedCount += tokens.Count;
                response.Errors.Add($"NoClientForApp:{appId}");
                continue;
            }

            var (notif, android, apns, webpush) = BuildPayload(request);
            var multicastMessage = new MulticastMessage
            {
                Tokens = tokens,
                Notification = notif,
                Android = android,
                Apns = apns,
                Webpush = webpush,
                Data = request.Data ?? new Dictionary<string, string>()
            };

            try
            {
                var batchResponse = await messagingClient.SendEachForMulticastAsync(multicastMessage);
                _logger.LogInformation("FCM Dispatch for App '{AppId}': Success = {SuccessCount}, Failures = {FailureCount}",
                    appId, batchResponse.SuccessCount, batchResponse.FailureCount);

                response.DeliveredCount += batchResponse.SuccessCount;
                response.FailedCount += batchResponse.FailureCount;

                // Handle Dead / Unregistered Tokens
                for (int i = 0; i < batchResponse.Responses.Count; i++)
                {
                    var item = batchResponse.Responses[i];
                    if (!item.IsSuccess)
                    {
                        var deadToken = tokens[i];
                        var errorCode = item.Exception?.MessagingErrorCode;

                        _logger.LogWarning("FCM Delivery Error on token {Token}: {Code} - {Msg}",
                            deadToken, errorCode, item.Exception?.Message);

                        response.Errors.Add($"{deadToken}: {errorCode}");

                        // Automatically deactivate invalidated tokens
                        if (errorCode == MessagingErrorCode.Unregistered ||
                            errorCode == MessagingErrorCode.InvalidArgument)
                        {
                            await _deviceRegistry.DeactivateTokenAsync(deadToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception during FCM multicast dispatch for App '{AppId}'", appId);
                response.FailedCount += tokens.Count;
                response.Errors.Add(ex.Message);
            }
        }

        response.Success = response.DeliveredCount > 0;
        if (!response.Success && string.IsNullOrEmpty(response.Reason))
        {
            response.Reason = "DeliveryFailed";
        }

        return response;
    }

    public async Task<TopicActionResponse> SubscribeToTopicAsync(string topic, IEnumerable<string> tokens, string? appId = null)
    {
        var targetApp = appId ?? "FCMPOC";
        var client = _firebaseAppManager.GetMessagingClient(targetApp);
        var tokenList = tokens.Distinct().ToList();

        if (client == null || _firebaseAppManager.IsMockMode(targetApp))
        {
            _logger.LogInformation("🧪 [MOCK TOPIC SUBSCRIBE] Topic '{Topic}', Tokens: {Count}", topic, tokenList.Count);
            return new TopicActionResponse
            {
                Success = true,
                Topic = topic,
                SuccessCount = tokenList.Count,
                FailureCount = 0
            };
        }

        try
        {
            var res = await client.SubscribeToTopicAsync(tokenList, topic);
            return new TopicActionResponse
            {
                Success = res.SuccessCount > 0,
                Topic = topic,
                SuccessCount = res.SuccessCount,
                FailureCount = res.FailureCount,
                Errors = res.Errors.Select(e => $"{tokenList[e.Index]}: {e.Reason}").ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe tokens to topic '{Topic}'", topic);
            return new TopicActionResponse
            {
                Success = false,
                Topic = topic,
                SuccessCount = 0,
                FailureCount = tokenList.Count,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<TopicActionResponse> UnsubscribeFromTopicAsync(string topic, IEnumerable<string> tokens, string? appId = null)
    {
        var targetApp = appId ?? "FCMPOC";
        var client = _firebaseAppManager.GetMessagingClient(targetApp);
        var tokenList = tokens.Distinct().ToList();

        if (client == null || _firebaseAppManager.IsMockMode(targetApp))
        {
            _logger.LogInformation("🧪 [MOCK TOPIC UNSUBSCRIBE] Topic '{Topic}', Tokens: {Count}", topic, tokenList.Count);
            return new TopicActionResponse
            {
                Success = true,
                Topic = topic,
                SuccessCount = tokenList.Count,
                FailureCount = 0
            };
        }

        try
        {
            var res = await client.UnsubscribeFromTopicAsync(tokenList, topic);
            return new TopicActionResponse
            {
                Success = res.SuccessCount > 0,
                Topic = topic,
                SuccessCount = res.SuccessCount,
                FailureCount = res.FailureCount,
                Errors = res.Errors.Select(e => $"{tokenList[e.Index]}: {e.Reason}").ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe tokens from topic '{Topic}'", topic);
            return new TopicActionResponse
            {
                Success = false,
                Topic = topic,
                SuccessCount = 0,
                FailureCount = tokenList.Count,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    private (Notification? notification, AndroidConfig android, ApnsConfig apns, WebpushConfig webpush) BuildPayload(NotificationSendRequest request)
    {
        var isSilent = request.IsSilent || (string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Body));
        Notification? notification = null;
        AndroidConfig android;
        ApnsConfig apns;
        WebpushConfig webpush;

        if (!isSilent)
        {
            // Visible Alert Push (Standard, Rich Media, Actionable, Collapsible, Time-Sensitive)
            notification = new Notification
            {
                Title = request.Title ?? string.Empty,
                Body = request.Body ?? string.Empty,
                ImageUrl = request.ImageUrl
            };

            var androidNotification = new AndroidNotification
            {
                ChannelId = request.Priority == NotificationChannelPriority.Critical ? "critical_channel" : "realtime_channel",
                Sound = "default",
                ImageUrl = request.ImageUrl,
                ClickAction = request.ActionCategory
            };

            android = new AndroidConfig
            {
                Priority = Priority.High,
                CollapseKey = request.CollapseKey,
                Notification = androidNotification
            };

            var aps = new Aps
            {
                Sound = "default",
                Badge = 1,
                Category = request.ActionCategory
            };

            var apnsHeaders = new Dictionary<string, string>();
            if (request.Priority == NotificationChannelPriority.Critical)
            {
                apnsHeaders["apns-priority"] = "10";
            }
            if (!string.IsNullOrEmpty(request.CollapseKey))
            {
                apnsHeaders["apns-collapse-id"] = request.CollapseKey;
            }

            apns = new ApnsConfig
            {
                Aps = aps,
                Headers = apnsHeaders.Any() ? apnsHeaders : null,
                FcmOptions = !string.IsNullOrEmpty(request.ImageUrl) ? new ApnsFcmOptions { ImageUrl = request.ImageUrl } : null
            };

            webpush = new WebpushConfig
            {
                Notification = new WebpushNotification
                {
                    Title = request.Title,
                    Body = request.Body,
                    Image = request.ImageUrl,
                    Icon = "https://www.gstatic.com/mobilesdk/160503_mobilesdk/logo/2x/firebase_96dp.png"
                }
            };
        }
        else
        {
            // Silent Push (Data-Only / Background Sync)
            android = new AndroidConfig
            {
                Priority = Priority.High
            };

            apns = new ApnsConfig
            {
                Aps = new Aps
                {
                    ContentAvailable = true
                },
                Headers = new Dictionary<string, string>
                {
                    { "apns-push-type", "background" },
                    { "apns-priority", "5" }
                }
            };

            webpush = new WebpushConfig();
        }

        return (notification, android, apns, webpush);
    }
}
