using CareerManager.Models;

namespace CareerManager.ViewModels;

public sealed record EvidencePrototypeEntry(
    int Id,
    string Category,
    string Title,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Context,
    string Contribution,
    string Outcome,
    string? Metric,
    IReadOnlyList<string> Tags,
    string? SourceNote,
    string? SourceUrl,
    string? RelatedEntry);

public sealed record ResumePrototype(
    string Summary,
    IReadOnlyList<EvidencePrototypeEntry> Experience,
    IReadOnlyList<string> Skills);

public sealed record DevelopmentPlanPrototype(
    string Goal,
    IReadOnlyList<string> Actions,
    string Milestone,
    string Review);

public sealed record CareerFitReflection(
    string Heading,
    string Prompt,
    IReadOnlyList<string> Examples);

public sealed class EvidenceWorkspaceViewModel
{
    public required ResumePrototype Resume { get; init; }
    public required IReadOnlyList<EvidencePrototypeEntry> Accomplishments { get; init; }
    public required DevelopmentPlanPrototype DevelopmentPlan { get; init; }
    public required IReadOnlyList<CareerFitReflection> CareerFit { get; init; }
    public required IReadOnlyList<string> Categories { get; init; }
    public required IReadOnlyList<EvidencePrototypeEntry> Entries { get; init; }
    public required IReadOnlyList<EvidencePrototypeEntry> SelectedEntries { get; init; }
    public IReadOnlyList<CareerOpportunity> Opportunities { get; init; } = [];
    public CareerPlan? Plan { get; init; }
    public string Search { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
}

public static class EvidenceWorkspacePrototype
{
    public static readonly string[] CategoryOptions =
    [
        "Work experience",
        "Education",
        "Skill",
        "Strength",
        "Interest",
        "Deep interest",
        "Achievement",
        "Side project"
    ];

    private static readonly EvidencePrototypeEntry[] SampleEntries =
    [
        new(
            1,
            "Work experience",
            "Fictional delivery coordinator",
            new DateOnly(2022, 1, 1),
            new DateOnly(2024, 1, 1),
            "Synthetic example coordinating a fictional cross-team project.",
            "Organized planning sessions and kept project notes current.",
            "The team completed the fictional project and recorded follow-up actions.",
            null,
            ["coordination", "planning"],
            "Synthetic example; no real source.",
            null,
            "Fictional project journal"),
        new(
            2,
            "Education",
            "Example learning programme",
            new DateOnly(2020, 1, 1),
            new DateOnly(2021, 1, 1),
            "Synthetic example of a fictional part-time programme.",
            "Completed example coursework and a practice project.",
            "Created a fictional project summary for the course.",
            null,
            ["learning"],
            null,
            null,
            null),
        new(
            3,
            "Skill",
            "Clear technical writing",
            null,
            null,
            "A synthetic example skill supported by fictional project notes.",
            "Explained example project decisions in plain language.",
            "Fictional teammates could refer back to the written decisions.",
            null,
            ["communication", "documentation"],
            null,
            null,
            "Fictional delivery coordinator"),
        new(
            4,
            "Strength",
            "Thoughtful problem solving",
            null,
            null,
            "A synthetic example strength observed in a fictional project.",
            "Asked clarifying questions and compared possible approaches.",
            "Recorded the chosen approach and its assumptions.",
            null,
            ["analysis"],
            null,
            null,
            null),
        new(
            5,
            "Interest",
            "Making complicated topics easier to understand",
            null,
            null,
            "A synthetic example of a work interest.",
            "Practiced rewriting fictional project notes for different audiences.",
            "Saved a fictional plain-language summary.",
            null,
            ["learning", "communication"],
            null,
            null,
            null),
        new(
            6,
            "Deep interest",
            "How teams share knowledge",
            null,
            null,
            "A synthetic example of a recurring topic of curiosity.",
            "Collected fictional examples of useful team documentation.",
            "Not recorded.",
            null,
            ["teams", "documentation"],
            null,
            null,
            null),
        new(
            7,
            "Achievement",
            "Documented a fictional project handover",
            null,
            null,
            "A synthetic example achievement from a fictional project.",
            "Prepared a handover summary and listed open questions.",
            "The fictional next team had a written starting point.",
            null,
            ["documentation", "handover"],
            "Synthetic example; no real source.",
            null,
            "Fictional delivery coordinator"),
        new(
            8,
            "Side project",
            "Example project journal",
            new DateOnly(2023, 1, 1),
            null,
            "A synthetic example of a personal project journal.",
            "Recorded fictional experiments and decisions.",
            "Kept an example record of what was tried.",
            null,
            ["personal project", "learning"],
            null,
            null,
            null),
        new(
            9,
            "Achievement",
            "Mentored a fictional new teammate",
            null,
            null,
            "A synthetic example of welcoming someone to a project.",
            "Explained the project notes and answered onboarding questions.",
            "The fictional teammate had a clear starting point.",
            null,
            ["mentoring", "communication"],
            "Synthetic example; no real source.",
            null,
            "Fictional delivery coordinator")
    ];

    public static EvidenceWorkspaceViewModel Create(
        string? search,
        string? category,
        IEnumerable<int>? selectedIds)
    {
        var normalizedSearch = search?.Trim() ?? string.Empty;
        var requestedCategory = category?.Trim() ?? string.Empty;
        var normalizedCategory = CategoryOptions.FirstOrDefault(
            option => string.Equals(option, requestedCategory, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        var hasInvalidCategory = requestedCategory.Length > 0 && normalizedCategory.Length == 0;
        var selected = selectedIds?.Distinct().ToHashSet() ?? [];

        var entries = SampleEntries
            .Where(entry => !hasInvalidCategory && (normalizedCategory.Length == 0
                || string.Equals(entry.Category, normalizedCategory, StringComparison.Ordinal))
            )
            .Where(entry => MatchesSearch(entry, normalizedSearch))
            .ToArray();

        return new EvidenceWorkspaceViewModel
        {
            Resume = new ResumePrototype(
                "A fictional coordinator who helps teams make complex work easier to understand through clear writing, planning, and knowledge sharing.",
                SampleEntries.Where(entry => entry.Category == "Work experience").ToArray(),
                ["Technical writing", "Project coordination", "Knowledge sharing"]),
            Accomplishments = SampleEntries.Where(entry => entry.Category == "Achievement").ToArray(),
            DevelopmentPlan = new DevelopmentPlanPrototype(
                "Build confidence presenting technical ideas.",
                ["Lead a short project demo.", "Ask a teammate for specific feedback."],
                "Complete two practice demos and collect feedback.",
                "Review after the next two practice sessions."),
            CareerFit =
            [
                new(
                    "What you are good at",
                    "Strengths supported by experience and accomplishments.",
                    ["Clear writing", "Organizing project work"]),
                new(
                    "What you like doing",
                    "Interests, deep interests, and work that energizes you.",
                    ["Explaining complex ideas", "Helping teammates learn"]),
                new(
                    "What makes money",
                    "Paid opportunities to investigate, not verified market demand.",
                    ["Explore technical writing roles", "Explore project coordination roles"])
            ],
            Categories = CategoryOptions,
            Entries = entries,
            SelectedEntries = SampleEntries.Where(entry => selected.Contains(entry.Id)).ToArray(),
            Search = normalizedSearch,
            Category = normalizedCategory
        };
    }

    public static EvidenceWorkspaceViewModel CreatePersisted(
        IEnumerable<CareerEntry> persistedEntries,
        CareerPlan? plan,
        IReadOnlyList<CareerOpportunity> opportunities,
        string? search,
        string? category,
        IEnumerable<int>? selectedIds)
    {
        var normalizedSearch = search?.Trim() ?? string.Empty;
        var requestedCategory = category?.Trim() ?? string.Empty;
        var normalizedCategory = CategoryOptions.FirstOrDefault(
            option => string.Equals(option, requestedCategory, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        var hasInvalidCategory = requestedCategory.Length > 0 && normalizedCategory.Length == 0;
        var storedEntries = persistedEntries
            .OrderBy(entry => entry.Category)
            .ThenBy(entry => entry.Title)
            .Select(entry => new EvidencePrototypeEntry(
                entry.Id,
                entry.Category,
                entry.Title,
                entry.StartDate,
                entry.EndDate,
                entry.Context,
                entry.Contribution ?? string.Empty,
                entry.Outcome ?? string.Empty,
                entry.Metric,
                (entry.Tags ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                entry.SourceNote,
                entry.SourceUrl,
                null))
            .ToArray();
        var selected = selectedIds?.Distinct().ToHashSet() ?? [];
        var filteredEntries = storedEntries
            .Where(entry => !hasInvalidCategory && (normalizedCategory.Length == 0
                || string.Equals(entry.Category, normalizedCategory, StringComparison.Ordinal)))
            .Where(entry => MatchesSearch(entry, normalizedSearch))
            .ToArray();
        var savedPlanActions = (plan?.NextActions ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var strengths = storedEntries.Where(entry => entry.Category == "Strength").Select(entry => entry.Title).ToArray();
        var interests = storedEntries
            .Where(entry => entry.Category is "Interest" or "Deep interest")
            .Select(entry => entry.Title)
            .ToArray();

        return new EvidenceWorkspaceViewModel
        {
            Resume = new ResumePrototype(
                "Choose persisted evidence below when you compare an opportunity; no resume summary is saved here.",
                storedEntries.Where(entry => entry.Category == "Work experience").ToArray(),
                storedEntries.Where(entry => entry.Category == "Skill").Select(entry => entry.Title).ToArray()),
            Accomplishments = storedEntries.Where(entry => entry.Category == "Achievement").ToArray(),
            DevelopmentPlan = new DevelopmentPlanPrototype(
                plan?.TargetRoles ?? "No saved career plan yet.",
                savedPlanActions,
                plan?.Progress ?? string.Empty,
                plan is null ? string.Empty : "Review the saved plan and its selected evidence when your goals change."),
            CareerFit =
            [
                new(
                    "What you are good at",
                    "Persisted strengths; use the prompts below to reflect on their context.",
                    strengths),
                new(
                    "What you like doing",
                    "Persisted interests and deep interests.",
                    interests),
                new(
                    "What makes money",
                    "Illustrative reflection only; these are not saved facts or verified market demand.",
                    ["Explore an opportunity, then verify its actual requirements and values."])
            ],
            Categories = CategoryOptions,
            Entries = filteredEntries,
            SelectedEntries = storedEntries.Where(entry => selected.Contains(entry.Id)).ToArray(),
            Opportunities = opportunities,
            Plan = plan,
            Search = normalizedSearch,
            Category = normalizedCategory
        };
    }

    private static bool MatchesSearch(EvidencePrototypeEntry entry, string search)
    {
        if (search.Length == 0)
        {
            return true;
        }

        return new[]
            {
                entry.Category,
                entry.Title,
                entry.Context,
                entry.Contribution,
                entry.Outcome,
                entry.SourceNote ?? string.Empty,
                entry.SourceUrl ?? string.Empty,
                string.Join(' ', entry.Tags)
            }
            .Any(value => value.Contains(search, StringComparison.OrdinalIgnoreCase));
    }
}
