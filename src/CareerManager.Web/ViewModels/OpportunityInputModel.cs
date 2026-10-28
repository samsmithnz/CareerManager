using System.ComponentModel.DataAnnotations;
using CareerManager.Models;

namespace CareerManager.ViewModels;

public sealed class OpportunityInputModel : IValidatableObject
{
    [Required, StringLength(200)]
    public string Company { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2048), HttpUrl]
    public string? JobUrl { get; set; }

    [StringLength(12000)]
    public string? JobDescription { get; set; }

    [StringLength(8000)]
    public string? CompanyContext { get; set; }

    [StringLength(4000)]
    public string? SourceUrls { get; set; }

    [StringLength(8000)]
    public string? Notes { get; set; }

    public DateOnly? DateFound { get; set; }
    public DateOnly? ApplicationDeadline { get; set; }
    public DateOnly? SubmissionDate { get; set; }
    public OpportunityStatus Status { get; set; } = OpportunityStatus.Saved;

    [StringLength(2000)]
    public string? NextActionNotes { get; set; }

    public DateOnly? NextActionDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Status))
        {
            yield return new ValidationResult("Choose a valid application status.", [nameof(Status)]);
        }

        if (Status is OpportunityStatus.Applied
            or OpportunityStatus.Interviewing
            or OpportunityStatus.Offer
            or OpportunityStatus.Rejected
            && SubmissionDate is null)
        {
            yield return new ValidationResult(
                "Enter the submission date for this application status.",
                [nameof(SubmissionDate)]);
        }

        foreach (var sourceUrl in (SourceUrls ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!new HttpUrlAttribute().IsValid(sourceUrl))
            {
                yield return new ValidationResult(
                    "Enter one HTTP or HTTPS source URL per line.",
                    [nameof(SourceUrls)]);
                break;
            }
        }
    }

    public CareerOpportunity ToEntity(string ownerId) =>
        new()
        {
            OwnerId = ownerId,
            Company = Company.Trim(),
            Title = Title.Trim(),
            JobUrl = EmptyToNull(JobUrl),
            JobDescription = EmptyToNull(JobDescription),
            CompanyContext = EmptyToNull(CompanyContext),
            SourceUrls = EmptyToNull(SourceUrls),
            Notes = EmptyToNull(Notes),
            DateFound = DateFound,
            ApplicationDeadline = ApplicationDeadline,
            SubmissionDate = SubmissionDate,
            Status = Status,
            NextActionNotes = EmptyToNull(NextActionNotes),
            NextActionDate = NextActionDate
        };

    public void ApplyTo(CareerOpportunity opportunity)
    {
        opportunity.Company = Company.Trim();
        opportunity.Title = Title.Trim();
        opportunity.JobUrl = EmptyToNull(JobUrl);
        opportunity.JobDescription = EmptyToNull(JobDescription);
        opportunity.CompanyContext = EmptyToNull(CompanyContext);
        opportunity.SourceUrls = EmptyToNull(SourceUrls);
        opportunity.Notes = EmptyToNull(Notes);
        opportunity.DateFound = DateFound;
        opportunity.ApplicationDeadline = ApplicationDeadline;
        opportunity.SubmissionDate = SubmissionDate;
        opportunity.Status = Status;
        opportunity.NextActionNotes = EmptyToNull(NextActionNotes);
        opportunity.NextActionDate = NextActionDate;
    }

    public static OpportunityInputModel FromEntity(CareerOpportunity opportunity) =>
        new()
        {
            Company = opportunity.Company,
            Title = opportunity.Title,
            JobUrl = opportunity.JobUrl,
            JobDescription = opportunity.JobDescription,
            CompanyContext = opportunity.CompanyContext,
            SourceUrls = opportunity.SourceUrls,
            Notes = opportunity.Notes,
            DateFound = opportunity.DateFound,
            ApplicationDeadline = opportunity.ApplicationDeadline,
            SubmissionDate = opportunity.SubmissionDate,
            Status = opportunity.Status,
            NextActionNotes = opportunity.NextActionNotes,
            NextActionDate = opportunity.NextActionDate
        };

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class OpportunityDeleteViewModel
{
    public required CareerOpportunity Opportunity { get; init; }
    public int LinkedDocumentCount { get; init; }
    public string? DocumentDisposition { get; set; }
}
