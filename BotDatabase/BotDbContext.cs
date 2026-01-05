using Microsoft.EntityFrameworkCore;
using BotDatabase.Entities;

namespace BotDatabase;

public class BotDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Chat> Chats { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<ForwardSource> ForwardSources { get; set; }
    public DbSet<AnalysisResult> AnalysisResults { get; set; }
    public DbSet<Todo> Todos { get; set; }
    public DbSet<Note> Notes { get; set; }
    public DbSet<MediaFile> MediaFiles { get; set; }
    public DbSet<TeamMember> TeamMembers { get; set; }
    public DbSet<WeeklyReport> WeeklyReports { get; set; }
    public DbSet<ReportWeek> ReportWeeks { get; set; }
    public DbSet<TaskBacklog> TaskBacklogs { get; set; }

    private readonly string _dbPath;

    public BotDbContext(string dbPath = "bot.db")
    {
        _dbPath = dbPath;
    }

    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseSqlite($"Data Source={_dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).ValueGeneratedNever();
            entity.HasIndex(e => e.Username);
        });

        // Chat
        modelBuilder.Entity<Chat>(entity =>
        {
            entity.HasKey(e => e.ChatId);
            entity.Property(e => e.ChatId).ValueGeneratedNever();
            entity.HasIndex(e => e.Username);
        });

        // Message
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TelegramMessageId, e.ChatId }).IsUnique();
            entity.HasIndex(e => e.ChatId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.SentAt);

            entity.HasOne(e => e.Chat)
                .WithMany(c => c.Messages)
                .HasForeignKey(e => e.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Messages)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.ForwardSource)
                .WithOne(f => f.Message)
                .HasForeignKey<ForwardSource>(f => f.Id)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ForwardSource
        modelBuilder.Entity<ForwardSource>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OriginId);
        });

        // AnalysisResult
        modelBuilder.Entity<AnalysisResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MessageId).IsUnique();
            entity.HasIndex(e => e.Status);

            entity.HasOne(e => e.Message)
                .WithOne(m => m.AnalysisResult)
                .HasForeignKey<AnalysisResult>(e => e.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Todo
        modelBuilder.Entity<Todo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.DueAt);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Todos)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Note
        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.IsPinned);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Notes)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // MediaFile
        modelBuilder.Entity<MediaFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ChatId);
            entity.HasIndex(e => e.TelegramFileUniqueId);
            entity.HasIndex(e => e.ConvertStatus);
            entity.HasIndex(e => e.IsIndexed);
            entity.HasIndex(e => e.CreatedAt);
        });

        // TeamMember
        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Alias);
            entity.HasIndex(e => e.TelegramUserId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Role);
        });

        // WeeklyReport
        modelBuilder.Entity<WeeklyReport>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TeamMemberId, e.WeekStart }).IsUnique();
            entity.HasIndex(e => e.WeekStart);
            entity.HasIndex(e => e.SubmittedAt);

            entity.HasOne(e => e.TeamMember)
                .WithMany(m => m.WeeklyReports)
                .HasForeignKey(e => e.TeamMemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ReportWeek
        modelBuilder.Entity<ReportWeek>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.WeekStart).IsUnique();
            entity.HasIndex(e => new { e.Year, e.WeekNumber });
        });

        // TaskBacklog
        modelBuilder.Entity<TaskBacklog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ChatId);
        });
    }
}
