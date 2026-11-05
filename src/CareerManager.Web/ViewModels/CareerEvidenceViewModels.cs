using System.ComponentModel.DataAnnotations;

namespace CareerManager.ViewModels;

public sealed class CareerEvidenceInputModel : IValidatableObject
{
    public Guid ConcurrencyToken { get; set; }

    [Required, StringLength(80)]
    public string Category { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(4000)]
    public string Context { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateOnly? StartDate { get; set; }

    [DataType(DataType.Date)]
    public DateOnly? EndDate { get; set; }

    [StringLength(4000)]
    public string? Contribution { get; set; }

    [StringLength(4000)]
    public string? Outcome { get; set; }

    [StringLength(1000)]
    public string? Metric { get; set; }

    [StringLength(2000)]
    public string? Tags { get; set; }

    [StringLength(2000)]
    public string? SourceNote { get; set; }

    [StringLength(2048), HttpUrl]
    public string? SourceUrl { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!EvidenceWorkspacePrototype.CategoryOptions.Contains(Category, StringComparer.Ordinal))
        {
            yield return new ValidationResult("Select a supported evidence category.", [nameof(Category)]);
        }

        if (StartDate.HasValue && EndDate.HasValue && EndDate.Value < StartDate.Value)
        {
            yield return new ValidationResult("The end date must be on or after the start date.", [nameof(EndDate)]);
        }
    }
}

public sealed class CareerEvidenceEditViewModel
{
    public int? Id { get; init; }
    public required CareerEvidenceInputModel Input { get; init; }
    public required IReadOnlyList<string> Categories { get; init; }
    public bool RequiresFactualReview { get; init; }
}

public sealed record CareerEvidenceDeleteViewModel(int Id, string Category, string Title);
