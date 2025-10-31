using RideTracker.Application.DTOs;
using RideTracker.Domain.Entities;

namespace RideTracker.Application.Interfaces;

public interface IStravaService
{
    string GetAuthorizationUrl();
    Task<StravaTokenDto> ExchangeCodeForTokenAsync(string code);
    Task<StravaTokenDto> RefreshTokenAsync(string refreshToken);
    Task<List<StravaActivityDto>> GetActivitiesAfterAsync(string accessToken, DateTime after);
    Task<bool> RefreshTokenIfNeededAsync(User user);
}

