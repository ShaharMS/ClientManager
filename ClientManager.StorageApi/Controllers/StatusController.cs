using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClientManager.StorageApi.Controllers;

/// <summary>
/// Exposes lightweight status information for the internal storage API host.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("internal/v{version:apiVersion}/status")]
[Tags("Status")]
public class StatusController : ControllerBase
{
    /// <summary>
    /// Returns a readiness response for the storage API host.
    /// </summary>
    /// <response code="200">The storage API host is running.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            service = "ClientManager.StorageApi"
        });
    }
}