using CareerManager.Data;
using CareerManager.Models;
using CareerManager.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CareerManager.Services;

public sealed class OwnerDataService(CareerManagerContext context)
{
    public async Task<OwnerDataExport?> ExportAsync(string ownerId, CancellationToken cancellationToken)
    {
        if (!await context.CareerOwners.AnyAsync(owner => owner.Id == ownerId, cancellationToken))
        {
            return null;
        }

        return new OwnerDataExport
        {
            ExportedAtUtc = DateTime.UtcNow,
            OwnerId = ownerId,
            Profiles = (await context.CareerProfiles.AsNoTracking()
                .Where(profile => profile.OwnerId == ownerId).OrderBy(profile => profile.Id)
                .ToListAsync(cancellationToken))
                .Select(profile => new CareerProfileExport(profile.Id, profile.OwnerId, profile.Name)).ToArray(),
            Entries = (await context.CareerEntries.AsNoTracking()
                .Where(entry => entry.OwnerId == ownerId).OrderBy(entry => entry.Id)
                .ToListAsync(cancellationToken))
                .Select(entry => new CareerEntryExport(
                    entry.Id, entry.OwnerId, entry.ProfileId, entry.SyntheticKey,
                    entry.Category, entry.Title, entry.Description)).ToArray(),
            Opportunities = (await context.CareerOpportunities.AsNoTracking()
                .Where(opportunity => opportunity.OwnerId == ownerId).OrderBy(opportunity => opportunity.Id)
                .ToListAsync(cancellationToken))
                .Select(opportunity => new CareerOpportunityExport(
                    opportunity.Id, opportunity.OwnerId, opportunity.Company, opportunity.Title,
                    opportunity.JobUrl, opportunity.JobDescription, opportunity.CompanyContext,
                    opportunity.SourceUrls, opportunity.Notes, opportunity.DateFound,
                    opportunity.ApplicationDeadline, opportunity.SubmissionDate,
                    opportunity.Status.ToString(), opportunity.NextActionNotes, opportunity.NextActionDate)).ToArray(),
            Documents = (await context.CareerDocuments.AsNoTracking()
                .Where(document => document.OwnerId == ownerId).OrderBy(document => document.Id)
                .ToListAsync(cancellationToken))
                .Select(document => new CareerDocumentExport(
                    document.Id, document.OwnerId, document.OpportunityId, document.Title, document.Text,
                    document.Type.ToString(), document.ConcurrencyToken, document.ApprovedAtUtc,
                    document.ApprovedContentHash, document.CreatedAtUtc, document.UpdatedAtUtc)).ToArray(),
            DocumentEvidence = (await context.CareerDocumentEvidence.AsNoTracking()
                .Where(reference => reference.OwnerId == ownerId)
                .OrderBy(reference => reference.CareerDocumentId).ThenBy(reference => reference.CareerEntryId)
                .ToListAsync(cancellationToken))
                .Select(reference => new CareerDocumentEvidenceExport(
                    reference.OwnerId, reference.CareerDocumentId, reference.CareerEntryId)).ToArray(),
            Plans = (await context.CareerPlans.AsNoTracking()
                .Where(plan => plan.OwnerId == ownerId).OrderBy(plan => plan.Id)
                .ToListAsync(cancellationToken))
                .Select(plan => new CareerPlanExport(
                    plan.Id, plan.OwnerId, plan.TargetRoles, plan.Strengths, plan.Interests,
                    plan.EvidenceGaps, plan.Recommendations, plan.NextActions, plan.Progress,
                    plan.ConcurrencyToken, plan.ApprovedAtUtc, plan.ApprovedContentHash,
                    plan.CreatedAtUtc, plan.UpdatedAtUtc)).ToArray(),
            PlanEvidence = (await context.CareerPlanEvidence.AsNoTracking()
                .Where(reference => reference.OwnerId == ownerId)
                .OrderBy(reference => reference.CareerPlanId).ThenBy(reference => reference.CareerEntryId)
                .ToListAsync(cancellationToken))
                .Select(reference => new CareerPlanEvidenceExport(
                    reference.OwnerId, reference.CareerPlanId, reference.CareerEntryId)).ToArray()
        };
    }

    public async Task DeleteAllAsync(string ownerId, Guid requestId, CancellationToken cancellationToken)
    {
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var ownerExists = await context.CareerOwners
                .AnyAsync(owner => owner.Id == ownerId, cancellationToken);
            if (!ownerExists)
            {
                throw new InvalidOperationException("The authenticated owner is not registered.");
            }

            context.CareerDocumentEvidence.RemoveRange(await context.CareerDocumentEvidence
                .Where(reference => reference.OwnerId == ownerId).ToListAsync(cancellationToken));
            context.CareerPlanEvidence.RemoveRange(await context.CareerPlanEvidence
                .Where(reference => reference.OwnerId == ownerId).ToListAsync(cancellationToken));
            context.CareerDocuments.RemoveRange(await context.CareerDocuments
                .Where(document => document.OwnerId == ownerId).ToListAsync(cancellationToken));
            context.CareerPlans.RemoveRange(await context.CareerPlans
                .Where(plan => plan.OwnerId == ownerId).ToListAsync(cancellationToken));
            context.CareerOpportunities.RemoveRange(await context.CareerOpportunities
                .Where(opportunity => opportunity.OwnerId == ownerId).ToListAsync(cancellationToken));
            context.CareerEntries.RemoveRange(await context.CareerEntries
                .Where(entry => entry.OwnerId == ownerId).ToListAsync(cancellationToken));
            context.CareerProfiles.RemoveRange(await context.CareerProfiles
                .Where(profile => profile.OwnerId == ownerId).ToListAsync(cancellationToken));
            context.DataDeletionAuditEvents.Add(new DataDeletionAuditEvent
            {
                RequestId = requestId,
                OccurredAtUtc = DateTime.UtcNow,
                Outcome = "Completed"
            });

            await context.SaveChangesAsync(cancellationToken);
            if (await HasContentAsync(ownerId, cancellationToken))
            {
                throw new InvalidOperationException("Owner content remains after deletion.");
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (Exception exception)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            context.ChangeTracker.Clear();
            await TryRecordOutcomeAsync(
                requestId,
                exception is OperationCanceledException ? "Cancelled" : "Failed");
            throw;
        }
    }

    private async Task<bool> HasContentAsync(string ownerId, CancellationToken cancellationToken) =>
        await context.CareerProfiles.AnyAsync(profile => profile.OwnerId == ownerId, cancellationToken)
        || await context.CareerEntries.AnyAsync(entry => entry.OwnerId == ownerId, cancellationToken)
        || await context.CareerOpportunities.AnyAsync(opportunity => opportunity.OwnerId == ownerId, cancellationToken)
        || await context.CareerDocuments.AnyAsync(document => document.OwnerId == ownerId, cancellationToken)
        || await context.CareerDocumentEvidence.AnyAsync(reference => reference.OwnerId == ownerId, cancellationToken)
        || await context.CareerPlans.AnyAsync(plan => plan.OwnerId == ownerId, cancellationToken)
        || await context.CareerPlanEvidence.AnyAsync(reference => reference.OwnerId == ownerId, cancellationToken);

    private async Task TryRecordOutcomeAsync(Guid requestId, string outcome)
    {
        try
        {
            context.DataDeletionAuditEvents.Add(new DataDeletionAuditEvent
            {
                RequestId = requestId,
                OccurredAtUtc = DateTime.UtcNow,
                Outcome = outcome
            });
            await context.SaveChangesAsync(CancellationToken.None);
        }
        catch
        {
            context.ChangeTracker.Clear();
        }
    }
}
