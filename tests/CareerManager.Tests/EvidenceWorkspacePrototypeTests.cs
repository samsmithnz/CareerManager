using CareerManager.ViewModels;

namespace CareerManager.Tests;

public class EvidenceWorkspacePrototypeTests
{
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
