using DaLat.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace DaLat.DAL;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<Place> Places => Set<Place>();
    public DbSet<PlaceTranslation> PlaceTranslations => Set<PlaceTranslation>();
    public DbSet<Visitor> Visitors => Set<Visitor>();
    public DbSet<VisitSession> VisitSessions => Set<VisitSession>();
    public DbSet<PlayLog> PlayLogs => Set<PlayLog>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Language>(e =>
        {
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(10);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.NativeName).HasMaxLength(100).IsRequired();
            e.Property(x => x.SpeechLocale).HasMaxLength(20).IsRequired();
        });

        b.Entity<Place>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.ImageUrl).HasMaxLength(500);
        });

        b.Entity<PlaceTranslation>(e =>
        {
            e.Property(x => x.LanguageCode).HasMaxLength(10).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.NarrationText).HasMaxLength(4000).IsRequired();
            e.Property(x => x.AudioUrl).HasMaxLength(500);
            e.HasIndex(x => new { x.PlaceId, x.LanguageCode }).IsUnique();
            e.HasOne(x => x.Place).WithMany(p => p.Translations)
                .HasForeignKey(x => x.PlaceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Language).WithMany(l => l.Translations)
                .HasForeignKey(x => x.LanguageCode).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Visitor>(e =>
        {
            e.HasIndex(x => x.ClientId).IsUnique();
            e.Property(x => x.ClientId).HasMaxLength(64).IsRequired();
            e.Property(x => x.DeviceLanguage).HasMaxLength(20);
        });

        b.Entity<VisitSession>(e =>
        {
            e.HasIndex(x => x.SessionKey).IsUnique();
            e.HasIndex(x => x.LastSeenAt);
            e.Property(x => x.SessionKey).HasMaxLength(64).IsRequired();
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.Property(x => x.UserAgent).HasMaxLength(500);
            e.Property(x => x.DeviceType).HasMaxLength(20);
            e.Property(x => x.Browser).HasMaxLength(50);
            e.Property(x => x.Os).HasMaxLength(50);
            e.Property(x => x.LanguageCode).HasMaxLength(10);
            e.HasOne(x => x.Visitor).WithMany(v => v.Sessions)
                .HasForeignKey(x => x.VisitorId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PlayLog>(e =>
        {
            e.HasIndex(x => x.PlayedAt);
            e.Property(x => x.LanguageCode).HasMaxLength(10).IsRequired();
            e.Property(x => x.TriggerType).HasMaxLength(10).IsRequired();
            e.HasOne(x => x.Place).WithMany(p => p.PlayLogs)
                .HasForeignKey(x => x.PlaceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Visitor).WithMany()
                .HasForeignKey(x => x.VisitorId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<AdminUser>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(100).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(300).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(100);
        });
    }
}
