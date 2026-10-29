using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using CareerManager.Data;
using CareerManager.Models;
using CareerManager.Security;
using CareerManager.Services;
using CareerManager.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CareerManager.Tests;

public sealed class OpportunityEndpointTests
{
    [Fact]
    public async Task AnonymousDirectRequestIsDenied()
    {
        var response = await GetOpportunityIndexStatusAsync(ownerId: null);
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.Unauthorized, response.Body);
    }

    [Fact]
    public async Task AuthenticatedRequestWithoutRegisteredOwnerIsDenied()
    {
        var response = await GetOpportunityIndexStatusAsync("unregistered-owner");
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.Forbidden, response.Body);
    }

    [Fact]
    public async Task AnonymousGenerationAndDocumentRequestsAreDenied()
    {
        const string connectionString =
            "Server=tcp:testing.database.windows.net,1433;Initial Catalog=CareerManager-Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=False";
        const string connectionStringEnvironmentVariable = "ConnectionStrings__CareerManager";
        var previousConnectionString = Environment.GetEnvironmentVariable(connectionStringEnvironmentVariable);
        Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, connectionString);

        try
        {
            await using var factory = new OpportunityWebApplicationFactory();
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var compareResponse = await client.GetAsync("/ApplicationDrafts/Compare/1");
            var documentResponse = await client.GetAsync("/Documents/Edit/1");
            var reviewResponse = await client.GetAsync("/Documents/Review/1");
            var exportResponse = await client.GetAsync(
                $"/Documents/Export/1?format=pdf&concurrencyToken={Guid.Empty}");
            var approvalResponse = await client.PostAsync(
                "/Documents/Approve/1",
                new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("ApprovalInput.ConcurrencyToken", Guid.Empty.ToString()),
                    new KeyValuePair<string, string>("ApprovalInput.FactualClaimsReviewed", "true"),
                    new KeyValuePair<string, string>("ApprovalInput.UnsupportedClaimsResolved", "true")
                ]));

            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, compareResponse.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, documentResponse.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, reviewResponse.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, exportResponse.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, approvalResponse.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, previousConnectionString);
        }
    }

    [Fact]
    public async Task AnonymousCareerPlanRequestsAreDenied()
    {
        const string connectionString =
            "Server=tcp:testing.database.windows.net,1433;Initial Catalog=CareerManager-Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=False";
        const string connectionStringEnvironmentVariable = "ConnectionStrings__CareerManager";
        var previousConnectionString = Environment.GetEnvironmentVariable(connectionStringEnvironmentVariable);
        Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, connectionString);

        try
        {
            await using var factory = new OpportunityWebApplicationFactory();
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            var index = await client.GetAsync("/CareerPlans");
            var create = await client.GetAsync("/CareerPlans/Create");
            var edit = await client.GetAsync("/CareerPlans/Edit/1");
            var generation = await client.GetAsync("/CareerPlans/ReviewGeneration/1");
            var approval = await client.PostAsync("/CareerPlans/Approve/1", new FormUrlEncodedContent([]));

            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, index.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, create.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, edit.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, generation.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, approval.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, previousConnectionString);
        }
    }

    [Fact]
    public async Task CareerPlanGeneratedSuggestionsArePresentedForReviewBeforeReplace()
    {
        const string connectionString =
            "Server=tcp:testing.database.windows.net,1433;Initial Catalog=CareerManager-Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=False";
        const string connectionStringEnvironmentVariable = "ConnectionStrings__CareerManager";
        var previousConnectionString = Environment.GetEnvironmentVariable(connectionStringEnvironmentVariable);
        Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, connectionString);

        try
        {
            await using var factory = new OpportunityWebApplicationFactory();
            int planId;
            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<CareerManagerContext>();
                var owner = new CareerOwner { Id = "owner-a" };
                var profile = new CareerProfile { OwnerId = owner.Id, Name = "Private profile" };
                var evidence = new CareerEntry
                {
                    OwnerId = owner.Id,
                    Profile = profile,
                    Category = "Achievement",
                    Title = "Reporting dashboard",
                    Description = "Built reporting dashboards using SQL for teams."
                };
                var plan = new CareerPlan
                {
                    OwnerId = owner.Id,
                    TargetRoles = "Reporting dashboards",
                    Strengths = "Communicating results",
                    Interests = "Data analysis",
                    EvidenceGaps = "Existing gap",
                    Recommendations = "Existing recommendation",
                    NextActions = "Existing action",
                    Progress = "Progress note"
                };
                plan.EvidenceReferences.Add(new CareerPlanEvidence
                {
                    OwnerId = owner.Id,
                    CareerEntryId = evidence.Id,
                    Entry = evidence,
                    Plan = plan
                });
                context.AddRange(owner, profile, evidence, plan);
                await context.SaveChangesAsync();
                planId = plan.Id;
            }

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });
            client.DefaultRequestHeaders.Add("X-Test-Owner", "owner-a");
            var reviewResponse = await client.GetAsync($"/CareerPlans/ReviewGeneration/{planId}");
            var reviewPage = await reviewResponse.Content.ReadAsStringAsync();
            var previewToken = Regex.Match(reviewPage, "name=\"PreviewToken\" value=\"([^\"]+)\"").Groups[1].Value;
            var antiforgeryToken = Regex.Match(
                reviewPage,
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

            Assert.Equal(System.Net.HttpStatusCode.OK, reviewResponse.StatusCode);
            Assert.Contains("Built reporting dashboards using SQL", reviewPage, StringComparison.Ordinal);
            Assert.False(string.IsNullOrEmpty(previewToken));
            Assert.False(string.IsNullOrEmpty(antiforgeryToken));

            var generatedResponse = await client.PostAsync(
                $"/CareerPlans/Generate/{planId}",
                new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("PreviewToken", previewToken),
                    new KeyValuePair<string, string>("Consent", "true"),
                    new KeyValuePair<string, string>("__RequestVerificationToken", antiforgeryToken)
                ]));
            var generatedPage = await generatedResponse.Content.ReadAsStringAsync();

            Assert.Equal(System.Net.HttpStatusCode.OK, generatedResponse.StatusCode);
            Assert.Contains("Suggested evidence gaps — not facts", generatedPage, StringComparison.Ordinal);
            Assert.Contains("Recommendations and possible alignments — not facts", generatedPage, StringComparison.Ordinal);
            Assert.Contains("Replace generated sections with this reviewed result", generatedPage, StringComparison.Ordinal);
            using var verificationScope = factory.Services.CreateScope();
            var savedPlan = await verificationScope.ServiceProvider.GetRequiredService<CareerManagerContext>()
                .CareerPlans.SingleAsync(plan => plan.Id == planId);
            Assert.Equal("Existing gap", savedPlan.EvidenceGaps);
            Assert.Equal("Existing recommendation", savedPlan.Recommendations);
            Assert.Equal("Existing action", savedPlan.NextActions);
        }
        finally
        {
            Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, previousConnectionString);
        }
    }

    [Fact]
    public async Task CareerPlanCanBeCreatedWithOnlyRequiredTargetRoles()
    {
        const string connectionString =
            "Server=tcp:testing.database.windows.net,1433;Initial Catalog=CareerManager-Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=False";
        const string connectionStringEnvironmentVariable = "ConnectionStrings__CareerManager";
        var previousConnectionString = Environment.GetEnvironmentVariable(connectionStringEnvironmentVariable);
        Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, connectionString);

        try
        {
            await using var factory = new OpportunityWebApplicationFactory();
            using (var seedScope = factory.Services.CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<CareerManagerContext>();
                context.CareerOwners.Add(new CareerOwner { Id = "owner-a" });
                await context.SaveChangesAsync();
            }

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });
            client.DefaultRequestHeaders.Add("X-Test-Owner", "owner-a");
            var createPage = await client.GetAsync("/CareerPlans/Create");
            var page = await createPage.Content.ReadAsStringAsync();
            var antiforgeryToken = Regex.Match(
                page,
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

            var response = await client.PostAsync(
                "/CareerPlans/Create",
                new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("Input.TargetRoles", "Technical writer"),
                    new KeyValuePair<string, string>("Input.Strengths", ""),
                    new KeyValuePair<string, string>("Input.Interests", ""),
                    new KeyValuePair<string, string>("Input.EvidenceGaps", ""),
                    new KeyValuePair<string, string>("Input.Recommendations", ""),
                    new KeyValuePair<string, string>("Input.NextActions", ""),
                    new KeyValuePair<string, string>("Input.Progress", ""),
                    new KeyValuePair<string, string>("__RequestVerificationToken", antiforgeryToken)
                ]));

            Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
            using var scope = factory.Services.CreateScope();
            var plan = await scope.ServiceProvider.GetRequiredService<CareerManagerContext>()
                .CareerPlans.SingleAsync();
            Assert.Equal("Technical writer", plan.TargetRoles);
            Assert.Empty(plan.Strengths);
            Assert.Empty(plan.Interests);
            Assert.Empty(plan.NextActions);
        }
        finally
        {
            Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, previousConnectionString);
        }
    }

    [Fact]
    public async Task ComparisonPreviewShowsSelectedOwnerEvidenceAndEncodesPastedText()
    {
        const string connectionString =
            "Server=tcp:testing.database.windows.net,1433;Initial Catalog=CareerManager-Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=False";
        const string connectionStringEnvironmentVariable = "ConnectionStrings__CareerManager";
        var previousConnectionString = Environment.GetEnvironmentVariable(connectionStringEnvironmentVariable);
        Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, connectionString);

        try
        {
            await using var factory = new OpportunityWebApplicationFactory();
            int opportunityId;
            int entryId;
            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<CareerManagerContext>();
                var owner = new CareerOwner { Id = "owner-a" };
                var profile = new CareerProfile { OwnerId = owner.Id, Name = "Private profile" };
                var entry = new CareerEntry
                {
                    OwnerId = owner.Id,
                    Profile = profile,
                    Category = "Skill",
                    Title = "Safe title",
                    Description = "A synthetic owner-selected skill."
                };
                var opportunity = new CareerOpportunity
                {
                    OwnerId = owner.Id,
                    Company = "Example",
                    Title = "Engineer",
                    JobDescription = "<script>alert(1)</script>"
                };
                context.AddRange(owner, profile, entry, opportunity);
                await context.SaveChangesAsync();
                opportunityId = opportunity.Id;
                entryId = entry.Id;
            }

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });
            client.DefaultRequestHeaders.Add("X-Test-Owner", "owner-a");

            var response = await client.GetAsync(
                $"/ApplicationDrafts/Compare/{opportunityId}?selectedEvidenceIds={entryId}");
            var page = await response.Content.ReadAsStringAsync();

            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("&lt;script&gt;", page, StringComparison.Ordinal);
            Assert.Contains("A synthetic owner-selected skill.", page, StringComparison.Ordinal);
            Assert.Contains($"value=\"{entryId}\"", page, StringComparison.Ordinal);
            Assert.Contains("nothing is sent to an external AI service", page, StringComparison.Ordinal);

            var antiforgeryToken = Regex.Match(
                page,
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.False(string.IsNullOrEmpty(antiforgeryToken));
            using var previewResponse = await client.PostAsync(
                $"/ApplicationDrafts/Preview/{opportunityId}",
                new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("SelectedEvidenceIds", entryId.ToString()),
                    new KeyValuePair<string, string>("__RequestVerificationToken", antiforgeryToken)
                ]));
            var previewPage = await previewResponse.Content.ReadAsStringAsync();

            Assert.Equal(System.Net.HttpStatusCode.OK, previewResponse.StatusCode);
            Assert.Contains("Confirm generation", previewPage, StringComparison.Ordinal);
            Assert.Contains("A synthetic owner-selected skill.", previewPage, StringComparison.Ordinal);
            Assert.Contains("&lt;script&gt;", previewPage, StringComparison.Ordinal);
            Assert.Contains("No generation request is sent until you confirm below.", previewPage, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, previousConnectionString);
        }
    }

    [Fact]
    public async Task DirectRequestForAnotherOwnersOpportunityReturnsNotFound()
    {
        const string connectionString =
            "Server=tcp:testing.database.windows.net,1433;Initial Catalog=CareerManager-Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=False";
        const string connectionStringEnvironmentVariable = "ConnectionStrings__CareerManager";
        var previousConnectionString = Environment.GetEnvironmentVariable(connectionStringEnvironmentVariable);
        Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, connectionString);

        try
        {
            await using var factory = new OpportunityWebApplicationFactory();
            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<CareerManagerContext>();
                context.CareerOwners.AddRange(
                    new CareerOwner { Id = "owner-a" },
                    new CareerOwner { Id = "owner-b" });
                var opportunity = new CareerOpportunity
                {
                    OwnerId = "owner-b",
                    Company = "Private Company",
                    Title = "Private role"
                };
                context.CareerOpportunities.Add(opportunity);
                context.CareerDocuments.Add(new CareerDocument
                {
                    OwnerId = "owner-b",
                    Title = "Saved document",
                    Text = "Preserved saved text.",
                    Opportunity = opportunity
                });
                await context.SaveChangesAsync();
            }

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            client.DefaultRequestHeaders.Add("X-Test-Owner", "owner-a");
            var response = await client.GetAsync("/Opportunities/Details/1");

            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);

            client.DefaultRequestHeaders.Remove("X-Test-Owner");
            client.DefaultRequestHeaders.Add("X-Test-Owner", "owner-b");
            var deleteResponse = await client.GetAsync("/Opportunities/Delete/1");
            var deletePage = await deleteResponse.Content.ReadAsStringAsync();

            Assert.Equal(System.Net.HttpStatusCode.OK, deleteResponse.StatusCode);
            Assert.Contains("Keep documents and unlink them", deletePage, StringComparison.Ordinal);
            Assert.Contains("Delete the documents", deletePage, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, previousConnectionString);
        }
    }

    [Fact]
    public async Task OwnerCanExportAllDataAndDeleteItWithConfirmationWhileOtherOwnersDataSurvives()
    {
        const string connectionString =
            "Server=tcp:testing.database.windows.net,1433;Initial Catalog=CareerManager-Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=False";
        const string connectionStringEnvironmentVariable = "ConnectionStrings__CareerManager";
        var previousConnectionString = Environment.GetEnvironmentVariable(connectionStringEnvironmentVariable);
        Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, connectionString);

        try
        {
            await using var factory = new OpportunityWebApplicationFactory();
            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<CareerManagerContext>();
                context.CareerOwners.AddRange(
                    new CareerOwner { Id = "owner-a" },
                    new CareerOwner { Id = "owner-b" });
                await context.SaveChangesAsync();

                var profileA = new CareerProfile { OwnerId = "owner-a", Name = "Private profile A" };
                var profileB = new CareerProfile { OwnerId = "owner-b", Name = "Private profile B" };
                context.CareerProfiles.AddRange(profileA, profileB);
                await context.SaveChangesAsync();

                var entryA = new CareerEntry
                {
                    OwnerId = "owner-a",
                    ProfileId = profileA.Id,
                    Category = "Achievement",
                    Title = "Private achievement A",
                    Description = "A detailed career achievement."
                };
                var entryB = new CareerEntry
                {
                    OwnerId = "owner-b",
                    ProfileId = profileB.Id,
                    Category = "Achievement",
                    Title = "Private achievement B",
                    Description = "Other owner's private data."
                };
                var opportunity = new CareerOpportunity
                {
                    OwnerId = "owner-a",
                    Company = "Example employer",
                    Title = "Example role",
                    JobUrl = "https://example.test/role",
                    JobDescription = "Role description",
                    CompanyContext = "Company context",
                    SourceUrls = "https://example.test/source",
                    Notes = "Opportunity notes",
                    DateFound = new DateOnly(2026, 1, 2),
                    ApplicationDeadline = new DateOnly(2026, 2, 3),
                    SubmissionDate = new DateOnly(2026, 1, 15),
                    Status = OpportunityStatus.Interviewing,
                    NextActionNotes = "Prepare interview examples",
                    NextActionDate = new DateOnly(2026, 1, 20)
                };
                context.AddRange(entryA, entryB, opportunity);
                await context.SaveChangesAsync();

                var documentA = new CareerDocument
                {
                    OwnerId = "owner-a",
                    OpportunityId = opportunity.Id,
                    Title = "Saved cover letter",
                    Text = "Private saved document text.",
                    Type = CareerDocumentType.CoverLetter,
                    ApprovedAtUtc = DateTime.UtcNow,
                    ApprovedContentHash = "approved-document-hash"
                };
                var documentB = new CareerDocument
                {
                    OwnerId = "owner-b",
                    Title = "Other owner's document",
                    Text = "Other owner's private document."
                };
                var plan = new CareerPlan
                {
                    OwnerId = "owner-a",
                    TargetRoles = "Product analyst",
                    Strengths = "Communication",
                    Interests = "Data analysis",
                    EvidenceGaps = "One possible gap",
                    Recommendations = "A saved recommendation",
                    NextActions = "Arrange a conversation",
                    Progress = "In progress",
                    ApprovedAtUtc = DateTime.UtcNow,
                    ApprovedContentHash = "approved-plan-hash"
                };
                context.AddRange(documentA, documentB, plan);
                await context.SaveChangesAsync();
                context.AddRange(
                    new CareerDocumentEvidence
                    {
                        OwnerId = "owner-a",
                        CareerDocumentId = documentA.Id,
                        CareerEntryId = entryA.Id
                    },
                    new CareerPlanEvidence
                    {
                        OwnerId = "owner-a",
                        CareerPlanId = plan.Id,
                        CareerEntryId = entryA.Id
                    });
                await context.SaveChangesAsync();
            }

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });
            client.DefaultRequestHeaders.Add("X-Test-Owner", "owner-a");
            var exportResponse = await client.GetAsync("/AccountData/Export");
            var exportJson = await exportResponse.Content.ReadAsStringAsync();
            Assert.Equal(System.Net.HttpStatusCode.OK, exportResponse.StatusCode);
            Assert.Contains("no-store", exportResponse.Headers.CacheControl?.ToString(), StringComparison.OrdinalIgnoreCase);
            using (var export = JsonDocument.Parse(exportJson))
            {
                var root = export.RootElement;
                Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
                Assert.Equal("owner-a", root.GetProperty("ownerId").GetString());
                Assert.Equal("Private profile A", root.GetProperty("profiles")[0].GetProperty("name").GetString());
                Assert.Equal("Private achievement A", root.GetProperty("entries")[0].GetProperty("title").GetString());
                Assert.Equal("Interviewing", root.GetProperty("opportunities")[0].GetProperty("status").GetString());
                Assert.Equal("2026-01-02", root.GetProperty("opportunities")[0].GetProperty("dateFound").GetString());
                Assert.Equal("2026-01-20", root.GetProperty("opportunities")[0].GetProperty("nextActionDate").GetString());
                Assert.Equal("Prepare interview examples",
                    root.GetProperty("opportunities")[0].GetProperty("nextActionNotes").GetString());
                Assert.Equal("Private saved document text.", root.GetProperty("documents")[0].GetProperty("text").GetString());
                Assert.Equal("CoverLetter", root.GetProperty("documents")[0].GetProperty("type").GetString());
                Assert.Equal(
                    root.GetProperty("opportunities")[0].GetProperty("id").GetInt32(),
                    root.GetProperty("documents")[0].GetProperty("opportunityId").GetInt32());
                Assert.Equal("approved-document-hash",
                    root.GetProperty("documents")[0].GetProperty("approvedContentHash").GetString());
                Assert.Equal(1, root.GetProperty("documentEvidence").GetArrayLength());
                Assert.Equal("A saved recommendation", root.GetProperty("plans")[0].GetProperty("recommendations").GetString());
                Assert.Equal("Arrange a conversation", root.GetProperty("plans")[0].GetProperty("nextActions").GetString());
                Assert.Equal("approved-plan-hash",
                    root.GetProperty("plans")[0].GetProperty("approvedContentHash").GetString());
                Assert.Equal(1, root.GetProperty("planEvidence").GetArrayLength());
                Assert.Equal(
                    root.GetProperty("entries")[0].GetProperty("id").GetInt32(),
                    root.GetProperty("documentEvidence")[0].GetProperty("careerEntryId").GetInt32());
            }

            Assert.DoesNotContain("Other owner's private data", exportJson, StringComparison.Ordinal);
            Assert.DoesNotContain("Other owner's private document", exportJson, StringComparison.Ordinal);

            client.DefaultRequestHeaders.Remove("X-Test-Owner");
            var anonymousExport = await client.GetAsync("/AccountData/Export");
            var anonymousDelete = await client.GetAsync("/AccountData/DeleteAll");
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, anonymousExport.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, anonymousDelete.StatusCode);
            client.DefaultRequestHeaders.Add("X-Test-Owner", "unregistered-owner");
            var unregisteredExport = await client.GetAsync("/AccountData/Export");
            var unregisteredDelete = await client.GetAsync("/AccountData/DeleteAll");
            Assert.Equal(System.Net.HttpStatusCode.Forbidden, unregisteredExport.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.Forbidden, unregisteredDelete.StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-Owner");
            client.DefaultRequestHeaders.Add("X-Test-Owner", "owner-a");

            var confirmationResponse = await client.GetAsync("/AccountData/DeleteAll");
            var confirmationPage = await confirmationResponse.Content.ReadAsStringAsync();
            var token = Regex.Match(
                confirmationPage,
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            Assert.Equal(System.Net.HttpStatusCode.OK, confirmationResponse.StatusCode);
            Assert.False(string.IsNullOrEmpty(token));

            var missingCsrf = await client.PostAsync("/AccountData/DeleteAll", new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>(nameof(DeleteAllDataInputModel.ConfirmDeletion), "true"),
                new KeyValuePair<string, string>(nameof(DeleteAllDataInputModel.ConfirmationText), "DELETE ALL MY DATA")
            ]));
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, missingCsrf.StatusCode);

            var invalidConfirmation = await PostDeleteConfirmationAsync(client, token, "DELETE MY DATA");
            Assert.Equal(System.Net.HttpStatusCode.OK, invalidConfirmation.StatusCode);
            Assert.Contains("DELETE ALL MY DATA exactly", await invalidConfirmation.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            var deleteToken = Regex.Match(
                await invalidConfirmation.Content.ReadAsStringAsync(),
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            var deleted = await PostDeleteConfirmationAsync(client, deleteToken, "DELETE ALL MY DATA");
            Assert.Equal(System.Net.HttpStatusCode.OK, deleted.StatusCode);
            Assert.Contains("Your saved application content has been deleted",
                await deleted.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<CareerManagerContext>();
                Assert.Equal(0, await context.CareerProfiles.CountAsync(item => item.OwnerId == "owner-a"));
                Assert.Equal(0, await context.CareerEntries.CountAsync(item => item.OwnerId == "owner-a"));
                Assert.Equal(0, await context.CareerOpportunities.CountAsync(item => item.OwnerId == "owner-a"));
                Assert.Equal(0, await context.CareerDocuments.CountAsync(item => item.OwnerId == "owner-a"));
                Assert.Equal(0, await context.CareerDocumentEvidence.CountAsync(item => item.OwnerId == "owner-a"));
                Assert.Equal(0, await context.CareerPlans.CountAsync(item => item.OwnerId == "owner-a"));
                Assert.Equal(0, await context.CareerPlanEvidence.CountAsync(item => item.OwnerId == "owner-a"));
                Assert.True(await context.CareerOwners.AnyAsync(item => item.Id == "owner-a"));
                Assert.True(await context.CareerOwners.AnyAsync(item => item.Id == "owner-b"));
                Assert.True(await context.CareerEntries.AnyAsync(item => item.OwnerId == "owner-b"));
                Assert.True(await context.CareerDocuments.AnyAsync(item => item.OwnerId == "owner-b"));
                var audit = await context.DataDeletionAuditEvents.SingleAsync();
                Assert.Equal("Completed", audit.Outcome);
                Assert.Equal(3, typeof(DataDeletionAuditEvent).GetProperties().Length);
            }

            Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/Opportunities/Create")).StatusCode);

            var repeatConfirmation = await client.GetAsync("/AccountData/DeleteAll");
            var repeatPage = await repeatConfirmation.Content.ReadAsStringAsync();
            var repeatToken = Regex.Match(
                repeatPage,
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            var repeatedDelete = await PostDeleteConfirmationAsync(client, repeatToken, "DELETE ALL MY DATA");
            Assert.Equal(System.Net.HttpStatusCode.OK, repeatedDelete.StatusCode);
            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<CareerManagerContext>();
                Assert.Equal(2, await context.DataDeletionAuditEvents.CountAsync());
                Assert.All(await context.DataDeletionAuditEvents.ToListAsync(),
                    audit => Assert.Equal("Completed", audit.Outcome));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, previousConnectionString);
        }
    }

    [Fact]
    public async Task ExportWaitsForInFlightGenerationAndIncludesItsSavedDrafts()
    {
        const string connectionString =
            "Server=tcp:testing.database.windows.net,1433;Initial Catalog=CareerManager-Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=False";
        const string connectionStringEnvironmentVariable = "ConnectionStrings__CareerManager";
        var previousConnectionString = Environment.GetEnvironmentVariable(connectionStringEnvironmentVariable);
        Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, connectionString);

        try
        {
            var provider = new BlockingGenerationProvider();
            await using var factory = new OpportunityWebApplicationFactory(provider);
            int opportunityId;
            int evidenceId;
            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<CareerManagerContext>();
                var owner = new CareerOwner { Id = "owner-a" };
                var profile = new CareerProfile { OwnerId = owner.Id, Name = "Private profile" };
                var evidence = new CareerEntry
                {
                    OwnerId = owner.Id,
                    Profile = profile,
                    Category = "Achievement",
                    Title = "Verified dashboard",
                    Description = "Built reporting dashboards for the analytics team."
                };
                var opportunity = new CareerOpportunity
                {
                    OwnerId = owner.Id,
                    Company = "Example employer",
                    Title = "Analyst",
                    JobDescription = "Build reporting dashboards for teams."
                };
                context.AddRange(owner, profile, evidence, opportunity);
                await context.SaveChangesAsync();
                opportunityId = opportunity.Id;
                evidenceId = evidence.Id;
            }

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = true
            });
            client.DefaultRequestHeaders.Add("X-Test-Owner", "owner-a");
            var compareResponse = await client.GetAsync(
                $"/ApplicationDrafts/Compare/{opportunityId}?selectedEvidenceIds={evidenceId}");
            var comparePage = await compareResponse.Content.ReadAsStringAsync();
            var compareToken = Regex.Match(
                comparePage,
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            var previewResponse = await client.PostAsync(
                $"/ApplicationDrafts/Preview/{opportunityId}",
                new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("SelectedEvidenceIds", evidenceId.ToString()),
                    new KeyValuePair<string, string>("__RequestVerificationToken", compareToken)
                ]));
            var previewPage = await previewResponse.Content.ReadAsStringAsync();
            var previewToken = Regex.Match(previewPage, "name=\"PreviewToken\" value=\"([^\"]+)\"").Groups[1].Value;
            var generationToken = Regex.Match(
                previewPage,
                "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;

            var generation = client.PostAsync(
                $"/ApplicationDrafts/Generate/{opportunityId}",
                new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("SelectedEvidenceIds", evidenceId.ToString()),
                    new KeyValuePair<string, string>("PreviewToken", previewToken),
                    new KeyValuePair<string, string>("Consent", "true"),
                    new KeyValuePair<string, string>("__RequestVerificationToken", generationToken)
                ]));
            await provider.GenerationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var export = client.GetAsync("/AccountData/Export");
            var exportDidFinishEarly = await Task.WhenAny(export, Task.Delay(100));
            Assert.NotSame(export, exportDidFinishEarly);

            provider.AllowGenerationToFinish.SetResult();
            var generationResponse = await generation;
            var exportResponse = await export;
            var exportJson = await exportResponse.Content.ReadAsStringAsync();

            Assert.Equal(System.Net.HttpStatusCode.Redirect, generationResponse.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.OK, exportResponse.StatusCode);
            using var exportedData = JsonDocument.Parse(exportJson);
            Assert.Equal(3, exportedData.RootElement.GetProperty("documents").GetArrayLength());
            Assert.Equal(3, exportedData.RootElement.GetProperty("documentEvidence").GetArrayLength());
        }
        finally
        {
            Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, previousConnectionString);
        }
    }

    private static Task<HttpResponseMessage> PostDeleteConfirmationAsync(
        HttpClient client,
        string token,
        string confirmationText) =>
        client.PostAsync("/AccountData/DeleteAll", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>(nameof(DeleteAllDataInputModel.ConfirmDeletion), "true"),
            new KeyValuePair<string, string>(nameof(DeleteAllDataInputModel.ConfirmationText), confirmationText),
            new KeyValuePair<string, string>("__RequestVerificationToken", token)
        ]));

    private static async Task<(System.Net.HttpStatusCode StatusCode, string Body)> GetOpportunityIndexStatusAsync(
        string? ownerId)
    {
        const string connectionString =
            "Server=tcp:testing.database.windows.net,1433;Initial Catalog=CareerManager-Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=False";
        const string connectionStringEnvironmentVariable = "ConnectionStrings__CareerManager";
        var previousConnectionString = Environment.GetEnvironmentVariable(connectionStringEnvironmentVariable);
        Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, connectionString);

        try
        {
            await using var factory = new OpportunityWebApplicationFactory();
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            if (ownerId is not null)
            {
                client.DefaultRequestHeaders.Add("X-Test-Owner", ownerId);
            }

            var response = await client.GetAsync("/Opportunities");
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        finally
        {
            Environment.SetEnvironmentVariable(connectionStringEnvironmentVariable, previousConnectionString);
        }
    }

    private sealed class OpportunityWebApplicationFactory(
        IApplicationGenerationProvider? generationProvider = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:CareerManager"] =
                        "Server=tcp:testing.database.windows.net,1433;Initial Catalog=CareerManager-Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=False"
                }));
            builder.ConfigureServices(services =>
            {
                var databaseName = Guid.NewGuid().ToString();
                services.RemoveAll<CareerManagerContext>();
                services.RemoveAll<DbContextOptions<CareerManagerContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<CareerManagerContext>>();
                services.AddDbContext<CareerManagerContext>(options =>
                    options.UseInMemoryDatabase(databaseName));

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
                if (generationProvider is not null)
                {
                    services.RemoveAll<IApplicationGenerationProvider>();
                    services.AddSingleton(generationProvider);
                }
            });
        }
    }

    private sealed class BlockingGenerationProvider : IApplicationGenerationProvider
    {
        private readonly DeterministicApplicationGenerationProvider _inner = new();

        public TaskCompletionSource GenerationStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource AllowGenerationToFinish { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string ProviderName => "Blocking test provider";
        public string ModelName => "blocking-test-v1";
        public string Region => "Test";
        public bool SendsDataExternally => false;

        public async Task<ApplicationGenerationResult> GenerateAsync(
            ApplicationGenerationRequest request,
            CancellationToken cancellationToken)
        {
            GenerationStarted.TrySetResult();
            await AllowGenerationToFinish.Task.WaitAsync(cancellationToken);
            return await _inner.GenerateAsync(request, cancellationToken);
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestOwner";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Owner", out var ownerId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [new Claim(CareerOwnerClaim.Type, ownerId.ToString())],
                SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
