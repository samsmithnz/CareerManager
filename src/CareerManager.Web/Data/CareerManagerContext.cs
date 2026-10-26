using CareerManager.Models;
using Microsoft.EntityFrameworkCore;

namespace CareerManager.Data;

public sealed class CareerManagerContext(DbContextOptions<CareerManagerContext> options) : DbContext(options)
{
    public DbSet<CareerOwner> CareerOwners => Set<CareerOwner>();
    public DbSet<CareerProfile> CareerProfiles => Set<CareerProfile>();
    public DbSet<CareerEntry> CareerEntries => Set<CareerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CareerOwner>(entity =>
        {
            entity.HasKey(owner => owner.Id);
            entity.Property(owner => owner.Id).HasMaxLength(256);
        });

        modelBuilder.Entity<CareerProfile>(entity =>
        {
            entity.HasKey(profile => profile.Id);
            entity.Property(profile => profile.OwnerId).HasMaxLength(256).IsRequired();
            entity.Property(profile => profile.Name).HasMaxLength(200).IsRequired();
            entity.HasAlternateKey(profile => new { profile.OwnerId, profile.Id });
            entity.HasOne(profile => profile.Owner)
                .WithMany(owner => owner.Profiles)
                .HasForeignKey(profile => profile.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CareerEntry>(entity =>
        {
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.OwnerId).HasMaxLength(256).IsRequired();
            entity.Property(entry => entry.Category).HasMaxLength(80).IsRequired();
            entity.Property(entry => entry.Title).HasMaxLength(200).IsRequired();
            entity.Property(entry => entry.Description).HasMaxLength(4000).IsRequired();
            entity.Property(entry => entry.SyntheticKey).HasMaxLength(80);
            entity.HasIndex(entry => new { entry.OwnerId, entry.ProfileId, entry.SyntheticKey })
                .IsUnique()
                .HasFilter("[SyntheticKey] IS NOT NULL");
            entity.HasOne(entry => entry.Profile)
                .WithMany(profile => profile.Entries)
                .HasForeignKey(entry => new { entry.OwnerId, entry.ProfileId })
                .HasPrincipalKey(profile => new { profile.OwnerId, profile.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
