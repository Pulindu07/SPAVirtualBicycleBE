using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using RideTracker.Application.Interfaces;
using RideTracker.Domain.Entities;
using RideTracker.Infrastructure.Data;
using RideTracker.Infrastructure.Repositories;
using RideTracker.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS
var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") 
                  ?? builder.Configuration["Frontend:Url"] 
                  ?? "http://localhost:5173";

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(frontendUrl, "http://localhost:5173")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Configure PostgreSQL - Environment variable takes precedence
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
                       ?? builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("Database connection string not configured");

builder.Services.AddDbContext<RideTrackerDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure Hangfire
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(options => 
        options.UseNpgsqlConnection(connectionString)));
builder.Services.AddHangfireServer();

// Register repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Register services
builder.Services.AddHttpClient<IStravaService, StravaService>();

// Route Generation Service - Choose one:
// Option 1: Google Maps Directions API (recommended for production - better accuracy)
builder.Services.AddHttpClient<IRouteGenerationService, GoogleMapsRouteService>();
// Option 2: OpenRouteService (free alternative, less accurate)
// builder.Services.AddHttpClient<IRouteGenerationService, OpenRouteService>();

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRouteService, RouteService>();
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<IGroupService, GroupService>();
builder.Services.AddScoped<IChallengeService, ChallengeService>();
builder.Services.AddScoped<RouteGenerationService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Configure Hangfire Dashboard
app.UseHangfireDashboard("/hangfire");

// Seed database and configure recurring job
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<RideTrackerDbContext>();
    
    // Apply migrations
    try
    {
        context.Database.Migrate();
        
        // Seed route points if they don't exist
        if (!context.RoutePoints.Any())
        {
            var routePoints = SeedData.GetSriLankaCoastalRoute();
            context.RoutePoints.AddRange(routePoints);
            context.SaveChanges();
            Console.WriteLine("Route points seeded successfully");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error during database initialization: {ex.Message}");
    }
}

// Configure recurring job for syncing activities every 2 hours
RecurringJob.AddOrUpdate<ISyncService>(
    "sync-all-users",
    service => service.SyncAllUsersAsync(),
    "0 */2 * * *"); // Every 2 hours

Console.WriteLine("RideTracker API is running...");

app.Run();
