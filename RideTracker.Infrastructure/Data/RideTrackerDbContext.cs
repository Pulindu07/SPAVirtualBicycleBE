using Microsoft.EntityFrameworkCore;
using RideTracker.Domain.Entities;

namespace RideTracker.Infrastructure.Data;

public class RideTrackerDbContext : DbContext
{
    public RideTrackerDbContext(DbContextOptions<RideTrackerDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Activity> Activities { get; set; }
    public DbSet<RoutePoint> RoutePoints { get; set; }
    public DbSet<UserProgress> UserProgress { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User Configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.StravaId).HasColumnName("strava_id").IsRequired();
            entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(255);
            entity.Property(e => e.AccessToken).HasColumnName("access_token").IsRequired();
            entity.Property(e => e.RefreshToken).HasColumnName("refresh_token").IsRequired();
            entity.Property(e => e.TokenExpiry).HasColumnName("token_expiry").IsRequired();
            entity.Property(e => e.TotalDistanceKm).HasColumnName("total_distance_km").HasDefaultValue(0);
            entity.Property(e => e.TotalMovingTimeSec).HasColumnName("total_moving_time_sec").HasDefaultValue(0);
            entity.Property(e => e.LastSync).HasColumnName("last_sync").HasDefaultValueSql("NOW()");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            
            entity.HasIndex(e => e.StravaId).IsUnique();
            
            entity.HasMany(e => e.Activities)
                .WithOne(e => e.User)
                .HasForeignKey(e => e.UserId);
            
            entity.HasOne(e => e.Progress)
                .WithOne(e => e.User)
                .HasForeignKey<UserProgress>(e => e.UserId);
        });

        // Activity Configuration
        modelBuilder.Entity<Activity>(entity =>
        {
            entity.ToTable("activities");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(500);
            entity.Property(e => e.DistanceKm).HasColumnName("distance_km");
            entity.Property(e => e.MovingTimeSec).HasColumnName("moving_time_sec");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.AverageSpeed).HasColumnName("average_speed");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
        });

        // RoutePoint Configuration
        modelBuilder.Entity<RoutePoint>(entity =>
        {
            entity.ToTable("route_points");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.OrderIndex).HasColumnName("order_index").IsRequired();
            entity.Property(e => e.Latitude).HasColumnName("latitude").IsRequired();
            entity.Property(e => e.Longitude).HasColumnName("longitude").IsRequired();
            
            entity.HasIndex(e => e.OrderIndex);
        });

        // UserProgress Configuration
        modelBuilder.Entity<UserProgress>(entity =>
        {
            entity.ToTable("user_progress");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.TotalDistanceKm).HasColumnName("total_distance_km");
            entity.Property(e => e.ProgressPercent).HasColumnName("progress_percent");
            entity.Property(e => e.CurrentLat).HasColumnName("current_lat");
            entity.Property(e => e.CurrentLng).HasColumnName("current_lng");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
        });
    }
}

