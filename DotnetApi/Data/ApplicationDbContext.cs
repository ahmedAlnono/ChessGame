// Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using ChessAPI.Models.Entities;

namespace ChessAPI.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) 
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Game> Games { get; set; }
    public DbSet<Move> Moves { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<Friend> Friends { get; set; }
    public DbSet<UserStatistics> UserStatistics { get; set; }
    public DbSet<Achievement> Achievements { get; set; }
    public DbSet<UserAchievement> UserAchievements { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Use PostgreSQL-specific configurations
        modelBuilder.HasPostgresExtension("uuid-ossp");

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Rating);
            entity.HasIndex(e => e.Status);
            
            // PostgreSQL UUID generation
            entity.Property(e => e.Id)
                .HasColumnType("uuid")
                .HasDefaultValueSql("uuid_generate_v4()");
            
            entity.HasMany(u => u.WhiteGames)
                  .WithOne(g => g.WhitePlayer)
                  .HasForeignKey(g => g.WhitePlayerId)
                  .OnDelete(DeleteBehavior.Restrict);
                  
            entity.HasMany(u => u.BlackGames)
                  .WithOne(g => g.BlackPlayer)
                  .HasForeignKey(g => g.BlackPlayerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(u => u.Messages)
                  .WithOne(m => m.Sender)
                  .HasForeignKey(m => m.SenderId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Game entity
        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.WhitePlayerId);
            entity.HasIndex(e => e.BlackPlayerId);
            
            entity.Property(e => e.Id)
                .HasColumnType("uuid")
                .HasDefaultValueSql("uuid_generate_v4()");
            
            entity.HasOne(g => g.Winner)
                  .WithMany()
                  .HasForeignKey(g => g.WinnerId)
                  .OnDelete(DeleteBehavior.Restrict);
                  
            entity.HasMany(g => g.ChatMessages)
                  .WithOne(m => m.Game)
                  .HasForeignKey(m => m.GameId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Move entity
        modelBuilder.Entity<Move>(entity =>
        {
            entity.HasIndex(e => e.GameId);
            entity.HasIndex(e => e.MoveNumber);
            entity.HasIndex(e => e.CreatedAt);
            
            entity.Property(e => e.Id)
                .HasColumnType("uuid")
                .HasDefaultValueSql("uuid_generate_v4()");
            
            entity.HasOne(m => m.Game)
                  .WithMany(g => g.Moves)
                  .HasForeignKey(m => m.GameId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(m => m.Player)
                  .WithMany()
                  .HasForeignKey(m => m.PlayerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure ChatMessage entity
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasIndex(e => e.GameId);
            entity.HasIndex(e => e.SenderId);
            entity.HasIndex(e => e.ReceiverId);
            entity.HasIndex(e => e.CreatedAt);
            
            entity.Property(e => e.Id)
                .HasColumnType("uuid")
                .HasDefaultValueSql("uuid_generate_v4()");
            
            entity.HasOne(m => m.Sender)
                  .WithMany(u => u.Messages)
                  .HasForeignKey(m => m.SenderId)
                  .OnDelete(DeleteBehavior.Restrict);
                  
            entity.HasOne(m => m.Game)
                  .WithMany(g => g.ChatMessages)
                  .HasForeignKey(m => m.GameId)
                  .OnDelete(DeleteBehavior.Cascade)
                  .IsRequired(false);
                  
            entity.HasOne(m => m.Receiver)
                  .WithMany()
                  .HasForeignKey(m => m.ReceiverId)
                  .OnDelete(DeleteBehavior.Restrict)
                  .IsRequired(false);
        });

        // Configure Friend entity
        modelBuilder.Entity<Friend>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.FriendId }).IsUnique();
            entity.HasIndex(e => e.Status);
            
            entity.Property(e => e.Id)
                .HasColumnType("uuid")
                .HasDefaultValueSql("uuid_generate_v4()");
            
            entity.HasOne(f => f.User)
                  .WithMany(u => u.Friends)
                  .HasForeignKey(f => f.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
                  
            entity.HasOne(f => f.FriendUser)
                  .WithMany(u => u.FriendOf)
                  .HasForeignKey(f => f.FriendId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure UserStatistics entity
        modelBuilder.Entity<UserStatistics>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.Date }).IsUnique();
            
            entity.Property(e => e.Id)
                .HasColumnType("uuid")
                .HasDefaultValueSql("uuid_generate_v4()");
            
            entity.HasOne(s => s.User)
                  .WithMany(u => u.Statistics)
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Achievement entity
        modelBuilder.Entity<Achievement>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            
            entity.Property(e => e.Id)
                .HasColumnType("uuid")
                .HasDefaultValueSql("uuid_generate_v4()");
        });

        // Configure UserAchievement entity
        modelBuilder.Entity<UserAchievement>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.AchievementId }).IsUnique();
            
            entity.Property(e => e.Id)
                .HasColumnType("uuid")
                .HasDefaultValueSql("uuid_generate_v4()");
            
            entity.HasOne(ua => ua.User)
                  .WithMany(u => u.Achievements)
                  .HasForeignKey(ua => ua.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(ua => ua.Achievement)
                  .WithMany(a => a.UserAchievements)
                  .HasForeignKey(ua => ua.AchievementId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}