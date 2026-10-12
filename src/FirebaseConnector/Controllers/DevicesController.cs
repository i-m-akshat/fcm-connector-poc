using FirebaseConnector.Models;
using FirebaseConnector.Services;
using Microsoft.AspNetCore.Mvc;

namespace FirebaseConnector.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceRegistryService _deviceRegistry;
    private readonly IPresenceService _presenceService;

    public DevicesController(IDeviceRegistryService deviceRegistry, IPresenceService presenceService)
    {
        _deviceRegistry = deviceRegistry;
        _presenceService = presenceService;
    }

    /// <summary>
    /// Registers or updates a device token for a user and activates immediate presence.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterDevice([FromBody] DeviceRegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.DeviceToken))
        {
            return BadRequest(new { error = "UserId and DeviceToken are required." });
        }

        await _deviceRegistry.RegisterDeviceAsync(request);
        // Automatically activate user presence with 90s sliding window
        await _presenceService.SetActiveAsync(request.UserId, request.DeviceToken, TimeSpan.FromSeconds(90));
        return Ok(new { status = "DeviceRegistered", userId = request.UserId, appId = request.AppId, presenceActive = true });
    }

    /// <summary>
    /// Deactivates a device token upon user logout and clears active presence.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        await _deviceRegistry.DeactivateDeviceAsync(request.UserId, request.DeviceToken);
        await _presenceService.SetInactiveAsync(request.UserId, request.DeviceToken);
        return Ok(new { status = "DeviceDeactivated", userId = request.UserId });
    }

    /// <summary>
    /// Lists all registered devices in memory (for debugging and testing).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllDevices()
    {
        var devices = await _deviceRegistry.GetAllDevicesAsync();
        return Ok(devices);
    }
}
