using Microsoft.AspNetCore.Mvc;
using RideTracker.Infrastructure.Services;

namespace RideTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RouteGenerationController : ControllerBase
{
    private readonly RouteGenerationService _routeGenerationService;
    private readonly ILogger<RouteGenerationController> _logger;

    public RouteGenerationController(
        RouteGenerationService routeGenerationService,
        ILogger<RouteGenerationController> logger)
    {
        _routeGenerationService = routeGenerationService;
        _logger = logger;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateRoute()
    {
        try
        {
            _logger.LogInformation("Starting route generation...");
            
            await _routeGenerationService.GenerateAndSaveRouteAsync();
            
            return Ok(new 
            { 
                message = "Route generated successfully with road data",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate route");
            return StatusCode(500, new 
            { 
                message = "Failed to generate route", 
                error = ex.Message 
            });
        }
    }
}

