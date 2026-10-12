using FirebaseConnector.Models;
using FirebaseConnector.Services;
using Microsoft.AspNetCore.Mvc;

namespace FirebaseConnector.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PresenceController : ControllerBase
{
    private readonly IPresenceService _presenceService;

    public PresenceController(IPresenceService presenceService)
    {
        _presenceService = presenceService;
    }

    /// <summary>
    /// Records a real-time heartbeat from an active mobile device (extends sliding TTL by 90s).
    /// </summary>
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return BadRequest(new { error = "UserId and DeviceId are required." });
        }

        // 90 second sliding window (mobile clients ping every 45 seconds)
        await _presenceService.SetActiveAsync(request.UserId, request.DeviceId, TimeSpan.FromSeconds(90));
        return Ok(new { status = "PresenceExtended", ttlSeconds = 90 });
    }

    /// <summary>
    /// Explicitly marks a device inactive when the app is minimized.
    /// </summary>
    [HttpPost("inactive")]
    public async Task<IActionResult> SetInactive([FromBody] HeartbeatRequest request)
    {
        await _presenceService.SetInactiveAsync(request.UserId, request.DeviceId);
        return Ok(new { status = "PresenceCleared" });
    }

    /// <summary>
    /// Checks if a user is currently active and lists active device IDs.
    /// </summary>
    [HttpGet("{userId}")]
    public async Task<IActionResult> CheckPresence(string userId)
    {
        var isActive = await _presenceService.IsUserActiveAsync(userId);
        var activeDevices = await _presenceService.GetActiveDevicesAsync(userId);

        return Ok(new
        {
            userId,
            isActive,
            activeDevices
        });
    }
}
