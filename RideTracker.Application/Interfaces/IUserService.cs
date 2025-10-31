using RideTracker.Application.DTOs;
using RideTracker.Domain.Entities;

namespace RideTracker.Application.Interfaces;

public interface IUserService
{
    Task<User?> GetUserByStravaIdAsync(long stravaId);
    Task<User?> GetUserByIdAsync(int userId);
    Task<User> CreateUserAsync(StravaTokenDto tokenDto);
    Task UpdateUserTokensAsync(User user, StravaTokenDto tokenDto);
    Task<UserProgressDto?> GetUserProgressAsync(int userId);
}

