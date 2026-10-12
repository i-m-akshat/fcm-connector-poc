using FirebaseConnector.Models;
using FirebaseConnector.Services;
using Microsoft.AspNetCore.Mvc;

namespace FirebaseConnector.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationDeliveryRouter _deliveryRouter;

    public NotificationsController(INotificationDeliveryRouter deliveryRouter)
    {
        _deliveryRouter = deliveryRouter;
    }

    /// <summary>
    /// Dispatches a push notification (Alert, Silent, Rich Media, Interactive, or Topic Broadcast).
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> SendNotification([FromBody] NotificationSendRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) &&
            string.IsNullOrWhiteSpace(request.DeviceToken) &&
            string.IsNullOrWhiteSpace(request.Topic) &&
            string.IsNullOrWhiteSpace(request.Condition))
        {
            return BadRequest(new { error = "At least one target must be specified: UserId, DeviceToken, Topic, or Condition." });
        }

        if (!request.IsSilent && string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Body))
        {
            return BadRequest(new { error = "Visible alert push requires at least Title or Body. (For silent data push, set IsSilent = true)." });
        }

        var result = await _deliveryRouter.DispatchAsync(request);
        return Ok(result);
    }
}
