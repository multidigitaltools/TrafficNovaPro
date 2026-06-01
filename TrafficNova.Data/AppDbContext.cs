using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TrafficNova.Core.Models;

namespace TrafficNova.Data;

public class AppDbContext : DbContext
{
    public DbSet<Campaign> Campaigns { get; set; }
    public DbSet<ProxyEntry> ProxyEntries { get; set; }
    public DbSet<TrafficSession> TrafficSessions { get; set; }
    public DbSet<ScheduledJob> ScheduledJobs { get; set; }

    private readonly string _dbPath;

    public AppDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite($"Data Source={_dbPath}");
        // Migrations are hand-authored and applied explicitly; the model
        // snapshot is intentionally minimal. Don't let EF 9's model-drift
        // guard turn MigrateAsync() into a fatal error.
        options.ConfigureWarnings(w =>
            w.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Campaign>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.ReferrerMode).HasConversion<string>();
            e.Property(x => x.UserAgentMode).HasConversion<string>();
            e.Property(x => x.DeviceType).HasConversion<string>();
            e.Property(x => x.ProxyRotation).HasConversion<string>();
            e.Property(x => x.ResourceBlockMode).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
        });

        model.Entity<ProxyEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Host).IsRequired().HasMaxLength(253);
            e.Property(x => x.Protocol).HasConversion<string>();
        });

        model.Entity<TrafficSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TargetUrl).HasMaxLength(2000);
            e.HasIndex(x => x.CampaignId);
            e.HasIndex(x => x.StartedAt);
            // BUG-082: declare the Proxy relationship so fresh databases get a real
            // FK on ProxyId with SetNull — deleting a proxy must NOT delete traffic
            // history, only clear the back-reference. Existing DBs (created by the
            // hand-authored migrations, which lack this constraint) are kept
            // consistent at the service layer in ProxyService.Delete*.
            e.HasOne<ProxyEntry>()
             .WithMany()
             .HasForeignKey(x => x.ProxyId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        model.Entity<ScheduledJob>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Campaign)
             .WithMany()
             .HasForeignKey(x => x.CampaignId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
