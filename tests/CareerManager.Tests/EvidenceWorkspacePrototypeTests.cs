using CareerManager.ViewModels;

namespace CareerManager.Tests;

public class EvidenceWorkspacePrototypeTests
{
    [Fact]
    public void WorkspaceSeparatesResumeAccomplishmentsPlansAndCareerFit()
    {
        var model = EvidenceWorkspacePrototype.Create(null, null, null);

        Assert.False(string.IsNullOrWhiteSpace(model.Resume.Summary));
        Assert.NotEmpty(model.Resume.Experience);
        Assert.All(model.Resume.Experience, entry => Assert.Equal("Work experience", entry.Category));
        Assert.NotEmpty(model.Resume.Skills);
        Assert.Equal(2, model.Accomplishments.Count);
        Assert.All(model.Accomplishments, entry =>
        {
            Assert.Equal("Achievement", entry.Category);
            Assert.Contains(entry, model.Entries);
            Assert.False(string.IsNullOrWhiteSpace(entry.Contribution));
            Assert.False(string.IsNullOrWhiteSpace(entry.Outcome));
        });
        Assert.False(string.IsNullOrWhiteSpace(model.DevelopmentPlan.Goal));
        Assert.Equal(2, model.DevelopmentPlan.Actions.Count);
        Assert.False(string.IsNullOrWhiteSpace(model.DevelopmentPlan.Milestone));
        Assert.False(string.IsNullOrWhiteSpace(model.DevelopmentPlan.Review));
        Assert.Equal(
            ["What you are good at", "What you like doing", "What makes money"],
            model.CareerFit.Select(reflection => reflection.Heading).ToArray());
        Assert.All(model.CareerFit, reflection => Assert.NotEmpty(reflection.Examples));
        Assert.Contains("not verified market demand", model.CareerFit[2].Prompt);
    }

    [Fact]
    public void EvidenceFiltersDoNotRemoveWorkspaceOverview()
    {
        var model = EvidenceWorkspacePrototype.Create("not a match", "Skill", null);

        Assert.Empty(model.Entries);
        Assert.NotEmpty(model.Resume.Experience);
        Assert.NotEmpty(model.Accomplishments);
        Assert.Equal(3, model.CareerFit.Count);
    }

    [Fact]
    public void SearchAndCategoryFiltersAreAppliedToSyntheticEntries()
    {
        var model = EvidenceWorkspacePrototype.Create("  project  ", "Work experience", null);

        var entry = Assert.Single(model.Entries);
        Assert.Equal("Fictional delivery coordinator", entry.Title);
        Assert.Equal("project", model.Search);
        Assert.Equal("Work experience", model.Category);
    }

    [Fact]
    public void SelectionIsDeduplicatedAndIndependentOfCurrentFilters()
    {
        var model = EvidenceWorkspacePrototype.Create("not a match", "Skill", [2, 2, 999]);

        Assert.Empty(model.Entries);
        var selected = Assert.Single(model.SelectedEntries);
        Assert.Equal("Example learning programme", selected.Title);
    }

    [Fact]
    public void MissingMetricsRemainMissingInSyntheticEvidence()
    {
        var model = EvidenceWorkspacePrototype.Create(null, null, null);

        Assert.All(model.Entries, entry => Assert.Null(entry.Metric));
    }

    [Fact]
    public void UnknownCategoryDoesNotBroadenTheResults()
    {
        var model = EvidenceWorkspacePrototype.Create(null, "Unknown category", null);

        Assert.Empty(model.Entries);
        Assert.Empty(model.Category);
    }
}
