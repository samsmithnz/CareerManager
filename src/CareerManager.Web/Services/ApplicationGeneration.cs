using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace CareerManager.Services;

public sealed record GenerationEvidence(int Id, string Category, string Title, string Description);

public sealed record ApplicationGenerationRequest(
    string Company,
    string RoleTitle,
    string JobDescription,
    string CompanyContext,
    IReadOnlyList<GenerationEvidence> Evidence);

public sealed record SupportedRoleMatch(string Requirement, IReadOnlyList<int> EvidenceIds);

public sealed record EvidenceGap(string Requirement, string Question);

public sealed record SupportedClaim(string Text, IReadOnlyList<int> EvidenceIds);

public sealed record ApplicationGenerationResult(
    IReadOnlyList<SupportedRoleMatch> Matches,
    IReadOnlyList<EvidenceGap> Gaps,
    IReadOnlyList<string> Questions,
    IReadOnlyList<SupportedClaim> ResumeClaims,
    IReadOnlyList<SupportedClaim> CoverLetterClaims);

public interface IApplicationGenerationProvider
{
    string ProviderName { get; }
    string ModelName { get; }
    string Region { get; }
    bool SendsDataExternally { get; }

    Task<ApplicationGenerationResult> GenerateAsync(
        ApplicationGenerationRequest request,
        CancellationToken cancellationToken);
}

public sealed class DeterministicApplicationGenerationProvider : IApplicationGenerationProvider
{
    private static readonly Regex RequirementSeparator = new(@"[\r\n.!?]+", RegexOptions.Compiled);
    private static readonly Regex Words = new(@"[\p{L}\p{N}]{3,}", RegexOptions.Compiled);
    private static readonly HashSet<string> CommonWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "are", "for", "from", "have", "that", "the", "their", "this", "with", "you"
    };

    public string ProviderName => "CareerManager local provider";
    public string ModelName => "keyword-overlap-v1";
    public string Region => "Application deployment region";
    public bool SendsDataExternally => false;

    public Task<ApplicationGenerationResult> GenerateAsync(
        ApplicationGenerationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var matches = new List<SupportedRoleMatch>();
        var gaps = new List<EvidenceGap>();
        var evidenceWords = request.Evidence.ToDictionary(
            evidence => evidence.Id,
            evidence => Tokenize($"{evidence.Title} {evidence.Description}"));

        foreach (var requirement in RequirementSeparator.Split(request.JobDescription)
                     .Select(value => value.Trim())
                     .Where(value => value.Length >= 12)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var requirementWords = Tokenize(requirement);
            var supportingIds = request.Evidence
                .Where(evidence => requirementWords
                    .Intersect(evidenceWords[evidence.Id], StringComparer.OrdinalIgnoreCase)
                    .Count() >= 2)
                .Select(evidence => evidence.Id)
                .ToArray();

            if (supportingIds.Length > 0)
            {
                matches.Add(new SupportedRoleMatch(requirement, supportingIds));
            }
            else
            {
                gaps.Add(new EvidenceGap(
                    requirement,
                    "What verified experience, qualification, or outcome supports this requirement?"));
            }
        }

        var claims = request.Evidence
            .Select(evidence => new SupportedClaim(
                $"{evidence.Title}: {evidence.Description}",
                [evidence.Id]))
            .ToArray();

        var questions = request.Evidence
            .Where(evidence => !Regex.IsMatch(evidence.Description, @"\d"))
            .Select(evidence => $"Can you add a verified date, number, or result for “{evidence.Title}”?")
            .ToList();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ApplicationGenerationResult(
            matches,
            gaps,
            questions.Distinct(StringComparer.Ordinal).ToArray(),
            claims,
            claims));
    }

    private static HashSet<string> Tokenize(string value) =>
        Words.Matches(value)
            .Select(match => match.Value)
            .Where(word => !CommonWords.Contains(word))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}

public sealed class ApplicationGenerationService(IApplicationGenerationProvider provider)
{
    public const int MaximumEvidenceCount = 10;
    public const int MaximumInputCharacters = 20_000;
    public const int MaximumDraftCharacters = 12_000;
    private static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(15);
    private static readonly Regex MatchWords = new(@"[\p{L}\p{N}]{3,}", RegexOptions.Compiled);
    private static readonly HashSet<string> MatchStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "are", "for", "from", "have", "that", "the", "their", "this", "with", "you"
    };
    public string ProviderName => provider.ProviderName;
    public string ModelName => provider.ModelName;
    public string Region => provider.Region;
    public bool SendsDataExternally => provider.SendsDataExternally;

    public async Task<ApplicationGenerationResult> GenerateAsync(
        ApplicationGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var inputLength = request.Company.Length
            + request.RoleTitle.Length
            + request.JobDescription.Length
            + request.CompanyContext.Length
            + request.Evidence.Sum(evidence => evidence.Category.Length + evidence.Title.Length + evidence.Description.Length);
        if (request.Evidence.Count > MaximumEvidenceCount || inputLength > MaximumInputCharacters)
        {
            throw new ApplicationGenerationException(
                "The request exceeds the safe input limit. Select at most 10 evidence items and shorten the role context.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProviderTimeout);
        ApplicationGenerationResult result;
        try
        {
            result = await provider.GenerateAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApplicationGenerationException(
                "Generation timed out. Your saved drafts were not changed; try again with less context.");
        }
        catch (ApplicationGenerationException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new ApplicationGenerationException(
                "The generation provider is unavailable. Your saved drafts were not changed; try again later.");
        }

        ValidateResult(
            result,
            request.Evidence.ToDictionary(evidence => evidence.Id),
            request.JobDescription,
            request.RoleTitle,
            request.Company);
        return result;
    }

    public static void ValidateResult(
        ApplicationGenerationResult? result,
        IReadOnlyDictionary<int, GenerationEvidence> selectedEvidence,
        string jobDescription,
        string roleTitle = "[Role]",
        string company = "[Company]")
    {
        var selectedEvidenceIds = selectedEvidence.Keys.ToHashSet();
        if (result is null
            || result.Matches is null
            || result.Gaps is null
            || result.Questions is null
            || result.ResumeClaims is null
            || result.CoverLetterClaims is null
            || result.Matches.Any(match => match is null
                || string.IsNullOrWhiteSpace(match.Requirement)
                || !jobDescription.Contains(match.Requirement, StringComparison.OrdinalIgnoreCase)
                || !HasSupportedMatch(match, selectedEvidence, selectedEvidenceIds))
            || result.Gaps.Any(gap => gap is null
                || string.IsNullOrWhiteSpace(gap.Requirement)
                || !jobDescription.Contains(gap.Requirement, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(gap.Question))
            || result.Questions.Any(string.IsNullOrWhiteSpace)
            || !HasValidClaims(result.ResumeClaims, selectedEvidence)
            || !HasValidClaims(result.CoverLetterClaims, selectedEvidence))
        {
            throw new ApplicationGenerationException(
                "The provider returned an invalid response. Your saved drafts were not changed; try again later.");
        }

        if (RenderResume(result).Length > MaximumDraftCharacters
            || RenderCoverLetter(result, roleTitle, company).Length > MaximumDraftCharacters
            || RenderComparison(result).Length > MaximumDraftCharacters)
        {
            throw new ApplicationGenerationException(
                "The generated response is too long to save. Shorten the selected evidence or role description and try again.");
        }
    }

    public static string RenderResume(ApplicationGenerationResult result) =>
        RenderClaims("Selected evidence", result.ResumeClaims);

    public static string RenderCoverLetter(
        ApplicationGenerationResult result,
        string roleTitle = "[Role]",
        string company = "[Company]") =>
        $"Dear hiring team,\n\nI am applying for {roleTitle} at {company}.\n\n"
        + RenderClaims("Relevant evidence for review", result.CoverLetterClaims)
        + "\n\n[Add your motivation after factual review.]\n\nSincerely,\n[Your name]";

    public static string RenderComparison(ApplicationGenerationResult result)
    {
        var content = new StringBuilder();
        content.AppendLine("Supported matches");
        foreach (var match in result.Matches)
        {
            content.Append("- ").Append(match.Requirement)
                .Append(" [Evidence: ").Append(string.Join(", ", match.EvidenceIds)).AppendLine("]");
        }

        content.AppendLine().AppendLine("Evidence gaps and questions");
        foreach (var gap in result.Gaps)
        {
            content.Append("- ").Append(gap.Requirement).Append(": ").AppendLine(gap.Question);
        }

        foreach (var question in result.Questions)
        {
            content.Append("- ").AppendLine(question);
        }

        return content.ToString().TrimEnd();
    }

    private static string RenderClaims(string heading, IReadOnlyList<SupportedClaim> claims)
    {
        var content = new StringBuilder(heading);
        foreach (var claim in claims)
        {
            content.Append("\n- ").Append(claim.Text)
                .Append(" [Evidence: ").Append(string.Join(", ", claim.EvidenceIds)).Append(']');
        }

        return content.ToString();
    }

    private static bool HasValidClaims(
        IReadOnlyList<SupportedClaim> claims,
        IReadOnlyDictionary<int, GenerationEvidence> selectedEvidence) =>
        claims.All(claim => claim is not null
            && !string.IsNullOrWhiteSpace(claim.Text)
            && claim.EvidenceIds is { Count: 1 }
            && selectedEvidence.TryGetValue(claim.EvidenceIds[0], out var evidence)
            && string.Equals(claim.Text, $"{evidence.Title}: {evidence.Description}", StringComparison.Ordinal));

    private static bool HasSupportedMatch(
        SupportedRoleMatch match,
        IReadOnlyDictionary<int, GenerationEvidence> selectedEvidence,
        IReadOnlySet<int> selectedEvidenceIds)
    {
        if (!HasValidReferences(match.EvidenceIds, selectedEvidenceIds))
        {
            return false;
        }

        var requirementWords = Tokenize(match.Requirement);
        return match.EvidenceIds.All(id => requirementWords
            .Intersect(Tokenize($"{selectedEvidence[id].Title} {selectedEvidence[id].Description}"),
                StringComparer.OrdinalIgnoreCase)
            .Count() >= 2);
    }

    private static HashSet<string> Tokenize(string value) =>
        MatchWords.Matches(value)
            .Select(match => match.Value)
            .Where(word => !MatchStopWords.Contains(word))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool HasValidReferences(IReadOnlyList<int>? references, IReadOnlySet<int> selectedEvidenceIds) =>
        references is { Count: > 0 }
        && references.All(selectedEvidenceIds.Contains)
        && references.Distinct().Count() == references.Count;
}

public sealed class ApplicationGenerationException(string message) : Exception(message);

public sealed class ApplicationGenerationQuota
{
    public const int MaximumRequestsPerOwnerPerUtcDay = 10;
    private readonly ConcurrentDictionary<string, OwnerUsage> _usage = new(StringComparer.Ordinal);

    public bool TryConsume(string ownerId, DateOnly utcDate)
    {
        var usage = _usage.GetOrAdd(ownerId, _ => new OwnerUsage(utcDate));
        lock (usage.Gate)
        {
            if (usage.Date != utcDate)
            {
                usage.Date = utcDate;
                usage.Count = 0;
            }

            if (usage.Count >= MaximumRequestsPerOwnerPerUtcDay)
            {
                return false;
            }

            usage.Count++;
            return true;
        }
    }

    private sealed class OwnerUsage(DateOnly date)
    {
        public object Gate { get; } = new();
        public DateOnly Date { get; set; } = date;
        public int Count { get; set; }
    }
}
