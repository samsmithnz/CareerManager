using System.ComponentModel.DataAnnotations;
using CareerManager.Models;
using CareerManager.ViewModels;

namespace CareerManager.Tests;

public sealed class CareerEvidenceWorkspaceTests
{
    [Theory]
    [InlineData("Work experience")]
    [InlineData("Education")]
    [InlineData("Skill")]
    [InlineData("Strength")]
    [InlineData("Interest")]
    [InlineData("Deep interest")]
    [InlineData("Achievement")]
    [InlineData("Side project")]
    public void EvidenceFormAcceptsEverySupportedCategory(string category)
    {
        var input = new CareerEvidenceInputModel
        {
            Category = category,
            Title = "Saved evidence",
            Context = "Owner-entered factual context."
        };
        var results = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true));
    }

    [Fact]
    public void EvidenceFormRejectsReversedDatesAndNonHttpUrls()
    {
        var input = new CareerEvidenceInputModel
        {
            Category = "Work experience",
            Title = "Saved evidence",
            Context = "Owner-entered context.",
            StartDate = new DateOnly(2024, 1, 1),
            EndDate = new DateOnly(2023, 12, 31),
            SourceUrl = "https://source.example/item"
        };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(input.EndDate)));

        input.StartDate = input.EndDate;
        input.SourceUrl = "javascript:alert(1)";
        results.Clear();
        Assert.False(Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(input.SourceUrl)));
    }

    [Fact]
    public void WorkspaceFiltersAndSelectsPersistedRecordsWithoutPrototypeIdsOrFacts()
    {
        var entries = EvidenceWorkspacePrototype.CategoryOptions
            .Select((category, index) => new CareerEntry
            {
                Id = 101 + index,
                OwnerId = "owner-a",
                ProfileId = 1,
                Category = category,
                Title = $"Saved {category}",
                Context = "Owner-entered context.",
                Tags = "saved, verified"
            })
            .ToArray();

        var model = EvidenceWorkspacePrototype.CreatePersisted(
            entries,
            null,
            0,
            [],
            "verified",
            "Achievement",
            [entries[6].Id, 1, 999]);

        var filtered = Assert.Single(model.Entries);
        Assert.Equal(entries[6].Id, filtered.Id);
        Assert.Equal("Owner-entered context.", filtered.Context);
        Assert.Null(filtered.Metric);
        Assert.Equal(entries[6].Id, Assert.Single(model.SelectedEntries).Id);
        Assert.DoesNotContain(model.Entries, entry => entry.Title.Contains("Fictional", StringComparison.Ordinal));
        Assert.Single(model.Resume.Experience);
    }
}
