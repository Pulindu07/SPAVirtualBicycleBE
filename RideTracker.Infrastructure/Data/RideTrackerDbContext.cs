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
    public DbSet<Group> Groups { get; set; }
    public DbSet<GroupMember> GroupMembers { get; set; }
    public DbSet<Challenge> Challenges { get; set; }
    public DbSet<ChallengeGroup> ChallengeGroups { get; set; }
    public DbSet<ChallengeParticipant> ChallengeParticipants { get; set; }
    public DbSet<ChallengeProgress> ChallengeProgress { get; set; }

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
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            
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

        // Group Configuration
        modelBuilder.Entity<Group>(entity =>
        {
            entity.ToTable("groups");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.IconUrl).HasColumnName("icon_url").HasMaxLength(500);
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            
            entity.HasOne(e => e.CreatedBy)
                .WithMany(e => e.CreatedGroups)
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // GroupMember Configuration
        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.ToTable("group_members");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.GroupId).HasColumnName("group_id").IsRequired();
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.Role).HasColumnName("role").HasMaxLength(50).HasDefaultValue("member");
            entity.Property(e => e.JoinedAt).HasColumnName("joined_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            
            entity.HasOne(e => e.Group)
                .WithMany(e => e.Members)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.User)
                .WithMany(e => e.GroupMemberships)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => new { e.GroupId, e.UserId }).IsUnique();
        });

        // Challenge Configuration
        modelBuilder.Entity<Challenge>(entity =>
        {
            entity.ToTable("challenges");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(e => e.TargetDistanceKm).HasColumnName("target_distance_km").IsRequired();
            entity.Property(e => e.StartDate).HasColumnName("start_date").IsRequired();
            entity.Property(e => e.EndDate).HasColumnName("end_date").IsRequired();
            entity.Property(e => e.ChallengeType).HasColumnName("challenge_type").HasMaxLength(50).HasDefaultValue("individual");
            entity.Property(e => e.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            
            entity.HasOne(e => e.CreatedBy)
                .WithMany(e => e.CreatedChallenges)
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ChallengeGroup Configuration (Many-to-Many: Challenge <-> Group)
        modelBuilder.Entity<ChallengeGroup>(entity =>
        {
            entity.ToTable("challenge_groups");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ChallengeId).HasColumnName("challenge_id").IsRequired();
            entity.Property(e => e.GroupId).HasColumnName("group_id").IsRequired();
            entity.Property(e => e.JoinedAt).HasColumnName("joined_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            
            entity.HasOne(e => e.Challenge)
                .WithMany(e => e.ParticipatingGroups)
                .HasForeignKey(e => e.ChallengeId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Group)
                .WithMany(e => e.ChallengeParticipations)
                .HasForeignKey(e => e.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => new { e.ChallengeId, e.GroupId }).IsUnique();
        });

        // ChallengeParticipant Configuration
        modelBuilder.Entity<ChallengeParticipant>(entity =>
        {
            entity.ToTable("challenge_participants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ChallengeId).HasColumnName("challenge_id").IsRequired();
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.JoinedAt).HasColumnName("joined_at").HasDefaultValueSql("NOW()");
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            
            entity.HasOne(e => e.Challenge)
                .WithMany(e => e.Participants)
                .HasForeignKey(e => e.ChallengeId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.User)
                .WithMany(e => e.ChallengeParticipations)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => new { e.ChallengeId, e.UserId }).IsUnique();
        });

        // ChallengeProgress Configuration
        modelBuilder.Entity<ChallengeProgress>(entity =>
        {
            entity.ToTable("challenge_progress");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ChallengeId).HasColumnName("challenge_id").IsRequired();
            entity.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(e => e.DistanceCoveredKm).HasColumnName("distance_covered_km").HasDefaultValue(0);
            entity.Property(e => e.ProgressPercentage).HasColumnName("progress_percentage").HasDefaultValue(0);
            entity.Property(e => e.CurrentPositionLat).HasColumnName("current_position_lat");
            entity.Property(e => e.CurrentPositionLng).HasColumnName("current_position_lng");
            entity.Property(e => e.LastActivityDate).HasColumnName("last_activity_date");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            
            entity.HasOne(e => e.Challenge)
                .WithMany(e => e.ProgressRecords)
                .HasForeignKey(e => e.ChallengeId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.User)
                .WithMany(e => e.ChallengeProgressRecords)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => new { e.ChallengeId, e.UserId }).IsUnique();
        });
    }
}

