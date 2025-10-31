using Microsoft.AspNetCore.Mvc;
using RideTracker.Application.Interfaces;

namespace RideTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RouteController : ControllerBase
{
    private readonly IRouteService _routeService;
    private readonly ILogger<RouteController> _logger;

    public RouteController(IRouteService routeService, ILogger<RouteController> logger)
    {
        _routeService = routeService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetRoute()
    {
        try
        {
            var points = await _routeService.GetRoutePointsAsync();
            return Ok(points);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving route points");
            return StatusCode(500, new { message = "An error occurred while retrieving route data" });
        }
    }

    [HttpGet("length")]
    public async Task<IActionResult> GetRouteLength()
    {
        try
        {
            var length = await _routeService.GetTotalRouteLengthKmAsync();
            return Ok(new { lengthKm = length });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving route length");
            return StatusCode(500, new { message = "An error occurred while retrieving route length" });
        }
    }
}

