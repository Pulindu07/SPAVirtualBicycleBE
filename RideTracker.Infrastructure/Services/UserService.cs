using Microsoft.EntityFrameworkCore;
using RideTracker.Application.DTOs;
using RideTracker.Application.Interfaces;
using RideTracker.Domain.Entities;
using RideTracker.Infrastructure.Data;

namespace RideTracker.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<UserProgress> _progressRepository;
    private readonly RideTrackerDbContext _context;

    public UserService(
        IRepository<User> userRepository,
        IRepository<UserProgress> progressRepository,
        RideTrackerDbContext context)
    {
        _userRepository = userRepository;
        _progressRepository = progressRepository;
        _context = context;
    }

    public async Task<User?> GetUserByStravaIdAsync(long stravaId)
    {
        return await _userRepository.FirstOrDefaultAsync(u => u.StravaId == stravaId);
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        return await _context.Users
            .Include(u => u.Progress)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<User> CreateUserAsync(StravaTokenDto tokenDto)
    {
        var user = new User
        {
            StravaId = tokenDto.Athlete?.Id ?? 0,
            Username = string.IsNullOrEmpty(tokenDto.Athlete?.Username) 
                ? $"{tokenDto.Athlete?.Firstname} {tokenDto.Athlete?.Lastname}".Trim()
                : tokenDto.Athlete.Username,
            AccessToken = tokenDto.AccessToken,
            RefreshToken = tokenDto.RefreshToken,
            TokenExpiry = tokenDto.ExpiresAt,
            TotalDistanceKm = 0,
            TotalMovingTimeSec = 0,
            LastSync = DateTime.UtcNow.AddYears(-1), // Set to past to trigger first sync
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();

        // Create initial progress
        var progress = new UserProgress
        {
            UserId = user.Id,
            TotalDistanceKm = 0,
            ProgressPercent = 0,
            CurrentLat = 5.9549, // Starting point: Matara
            CurrentLng = 80.5550,
            UpdatedAt = DateTime.UtcNow
        };

        await _progressRepository.AddAsync(progress);
        await _progressRepository.SaveChangesAsync();

        return user;
    }

    public async Task UpdateUserTokensAsync(User user, StravaTokenDto tokenDto)
    {
        user.AccessToken = tokenDto.AccessToken;
        user.RefreshToken = tokenDto.RefreshToken;
        user.TokenExpiry = tokenDto.ExpiresAt;

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();
    }

    public async Task<UserProgressDto?> GetUserProgressAsync(int userId)
    {
        var user = await GetUserByIdAsync(userId);
        if (user == null || user.Progress == null)
            return null;

        return new UserProgressDto
        {
            TotalDistanceKm = user.TotalDistanceKm,
            TotalMovingTimeSec = user.TotalMovingTimeSec,
            ProgressPercent = user.Progress.ProgressPercent,
            CurrentLat = user.Progress.CurrentLat,
            CurrentLng = user.Progress.CurrentLng,
            LastSync = user.LastSync,
            Username = user.Username
        };
    }
}

