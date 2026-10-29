using CareerManager.Models;
using Microsoft.EntityFrameworkCore;

namespace CareerManager.Data;

public sealed class CareerManagerContext(DbContextOptions<CareerManagerContext> options) : DbContext(options)
{
    public DbSet<CareerOwner> CareerOwners => Set<CareerOwner>();
    public DbSet<CareerProfile> CareerProfiles => Set<CareerProfile>();
    public DbSet<CareerEntry> CareerEntries => Set<CareerEntry>();
    public DbSet<CareerOpportunity> CareerOpportunities => Set<CareerOpportunity>();
    public DbSet<CareerDocument> CareerDocuments => Set<CareerDocument>();
    public DbSet<CareerDocumentEvidence> CareerDocumentEvidence => Set<CareerDocumentEvidence>();
    public DbSet<CareerPlan> CareerPlans => Set<CareerPlan>();
    public DbSet<CareerPlanEvidence> CareerPlanEvidence => Set<CareerPlanEvidence>();
    public DbSet<DataDeletionAuditEvent> DataDeletionAuditEvents => Set<DataDeletionAuditEvent>();

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
            entity.HasAlternateKey(entry => new { entry.OwnerId, entry.Id });
            entity.HasOne(entry => entry.Profile)
                .WithMany(profile => profile.Entries)
                .HasForeignKey(entry => new { entry.OwnerId, entry.ProfileId })
                .HasPrincipalKey(profile => new { profile.OwnerId, profile.Id })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CareerOpportunity>(entity =>
        {
            entity.HasKey(opportunity => opportunity.Id);
            entity.Property(opportunity => opportunity.OwnerId).HasMaxLength(256).IsRequired();
            entity.Property(opportunity => opportunity.Company).HasMaxLength(200).IsRequired();
            entity.Property(opportunity => opportunity.Title).HasMaxLength(200).IsRequired();
            entity.Property(opportunity => opportunity.JobUrl).HasMaxLength(2048);
            entity.Property(opportunity => opportunity.JobDescription).HasMaxLength(12000);
            entity.Property(opportunity => opportunity.CompanyContext).HasMaxLength(8000);
            entity.Property(opportunity => opportunity.SourceUrls).HasMaxLength(4000);
            entity.Property(opportunity => opportunity.Notes).HasMaxLength(8000);
            entity.Property(opportunity => opportunity.NextActionNotes).HasMaxLength(2000);
            entity.Property(opportunity => opportunity.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasAlternateKey(opportunity => new { opportunity.OwnerId, opportunity.Id });
            entity.HasIndex(opportunity => new { opportunity.OwnerId, opportunity.Status });
            entity.HasOne(opportunity => opportunity.Owner)
                .WithMany(owner => owner.Opportunities)
                .HasForeignKey(opportunity => opportunity.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CareerDocument>(entity =>
        {
            entity.HasKey(document => document.Id);
            entity.Property(document => document.OwnerId).HasMaxLength(256).IsRequired();
            entity.Property(document => document.Title).HasMaxLength(200).IsRequired();
            entity.Property(document => document.Text).HasMaxLength(12000).IsRequired();
            entity.Property(document => document.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(document => document.ConcurrencyToken).IsConcurrencyToken();
            entity.Property(document => document.ApprovedContentHash).HasMaxLength(64);
            entity.HasAlternateKey(document => new { document.OwnerId, document.Id });
            entity.HasOne(document => document.Owner)
                .WithMany(owner => owner.Documents)
                .HasForeignKey(document => document.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(document => document.Opportunity)
                .WithMany(opportunity => opportunity.Documents)
                .HasForeignKey(document => new { document.OwnerId, document.OpportunityId })
                .HasPrincipalKey(opportunity => new { opportunity.OwnerId, opportunity.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CareerDocumentEvidence>(entity =>
        {
            entity.HasKey(reference => new
            {
                reference.OwnerId,
                reference.CareerDocumentId,
                reference.CareerEntryId
            });
            entity.Property(reference => reference.OwnerId).HasMaxLength(256).IsRequired();
            entity.HasOne(reference => reference.Document)
                .WithMany(document => document.EvidenceReferences)
                .HasForeignKey(reference => new { reference.OwnerId, reference.CareerDocumentId })
                .HasPrincipalKey(document => new { document.OwnerId, document.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(reference => new { reference.OwnerId, reference.CareerEntryId });
        });

        modelBuilder.Entity<CareerPlan>(entity =>
        {
            entity.HasKey(plan => plan.Id);
            entity.Property(plan => plan.OwnerId).HasMaxLength(256).IsRequired();
            entity.Property(plan => plan.TargetRoles).HasMaxLength(4000).IsRequired();
            entity.Property(plan => plan.Strengths).HasMaxLength(4000).IsRequired();
            entity.Property(plan => plan.Interests).HasMaxLength(4000).IsRequired();
            entity.Property(plan => plan.EvidenceGaps).HasMaxLength(4000).IsRequired();
            entity.Property(plan => plan.Recommendations).HasMaxLength(4000).IsRequired();
            entity.Property(plan => plan.NextActions).HasMaxLength(4000).IsRequired();
            entity.Property(plan => plan.Progress).HasMaxLength(4000).IsRequired();
            entity.Property(plan => plan.ConcurrencyToken).IsConcurrencyToken();
            entity.Property(plan => plan.ApprovedContentHash).HasMaxLength(64);
            entity.HasAlternateKey(plan => new { plan.OwnerId, plan.Id });
            entity.HasIndex(plan => plan.OwnerId).IsUnique();
            entity.HasOne(plan => plan.Owner)
                .WithMany(owner => owner.Plans)
                .HasForeignKey(plan => plan.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CareerPlanEvidence>(entity =>
        {
            entity.HasKey(reference => new
            {
                reference.OwnerId,
                reference.CareerPlanId,
                reference.CareerEntryId
            });
            entity.Property(reference => reference.OwnerId).HasMaxLength(256).IsRequired();
            entity.HasOne(reference => reference.Plan)
                .WithMany(plan => plan.EvidenceReferences)
                .HasForeignKey(reference => new { reference.OwnerId, reference.CareerPlanId })
                .HasPrincipalKey(plan => new { plan.OwnerId, plan.Id })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(reference => reference.Entry)
                .WithMany()
                .HasForeignKey(reference => new { reference.OwnerId, reference.CareerEntryId })
                .HasPrincipalKey(entry => new { entry.OwnerId, entry.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(reference => new { reference.OwnerId, reference.CareerEntryId });
        });

        modelBuilder.Entity<DataDeletionAuditEvent>(entity =>
        {
            entity.HasKey(auditEvent => auditEvent.RequestId);
            entity.Property(auditEvent => auditEvent.Outcome).HasMaxLength(20).IsRequired();
        });
    }
}
