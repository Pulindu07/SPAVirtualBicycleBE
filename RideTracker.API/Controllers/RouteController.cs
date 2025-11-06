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
    public async Task<IActionResult> GetRoute([FromQuery] int? routeId = null)
    {
        try
        {
            var points = await _routeService.GetRoutePointsAsync(routeId);
            return Ok(points);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving route points");
            return StatusCode(500, new { message = "An error occurred while retrieving route data" });
        }
    }

    [HttpGet("length")]
    public async Task<IActionResult> GetRouteLength([FromQuery] int? routeId = null)
    {
        try
        {
            var length = await _routeService.GetTotalRouteLengthKmAsync(routeId);
            return Ok(new { lengthKm = length });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving route length");
            return StatusCode(500, new { message = "An error occurred while retrieving route length" });
        }
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetAllRoutes()
    {
        try
        {
            var routes = await _routeService.GetAllRoutesAsync();
            return Ok(routes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving routes");
            return StatusCode(500, new { message = "An error occurred while retrieving routes" });
        }
    }

    [HttpGet("{routeId}")]
    public async Task<IActionResult> GetRoute(int routeId)
    {
        try
        {
            var route = await _routeService.GetRouteByIdAsync(routeId);
            if (route == null)
                return NotFound(new { message = "Route not found" });

            return Ok(route);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving route {RouteId}", routeId);
            return StatusCode(500, new { message = "An error occurred while retrieving the route" });
        }
    }
}

