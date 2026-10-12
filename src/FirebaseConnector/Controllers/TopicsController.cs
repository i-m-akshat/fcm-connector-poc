using FirebaseConnector.Models;
using FirebaseConnector.Services;
using Microsoft.AspNetCore.Mvc;

namespace FirebaseConnector.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TopicsController : ControllerBase
{
    private readonly INotificationDeliveryRouter _deliveryRouter;

    public TopicsController(INotificationDeliveryRouter deliveryRouter)
    {
        _deliveryRouter = deliveryRouter;
    }

    /// <summary>
    /// Subscribes one or more device tokens to a Firebase topic (e.g., 'news', 'drivers_london').
    /// </summary>
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] TopicSubscriptionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Topic) || request.DeviceTokens == null || !request.DeviceTokens.Any())
        {
            return BadRequest(new { error = "Topic and at least one DeviceToken are required." });
        }

        var result = await _deliveryRouter.SubscribeToTopicAsync(request.Topic, request.DeviceTokens, request.AppId);
        return Ok(result);
    }

    /// <summary>
    /// Unsubscribes one or more device tokens from a Firebase topic.
    /// </summary>
    [HttpPost("unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] TopicSubscriptionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Topic) || request.DeviceTokens == null || !request.DeviceTokens.Any())
        {
            return BadRequest(new { error = "Topic and at least one DeviceToken are required." });
        }

        var result = await _deliveryRouter.UnsubscribeFromTopicAsync(request.Topic, request.DeviceTokens, request.AppId);
        return Ok(result);
    }
}
