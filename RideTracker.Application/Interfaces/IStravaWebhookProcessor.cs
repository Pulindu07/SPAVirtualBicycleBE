using RideTracker.Application.DTOs;

namespace RideTracker.Application.Interfaces;

public interface IStravaWebhookProcessor
{
    Task ProcessEventAsync(StravaWebhookEventDto evt);
}
