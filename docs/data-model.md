# Data model

CareerManager stores private career records in Azure SQL through EF Core. Every record read or changed by the application is filtered by the authenticated, registered `CareerOwner.Id`.

## Career evidence

`CareerEntry` belongs to a `CareerProfile` through the composite `(OwnerId, ProfileId)` relationship. The evidence form supports these categories: work experience, education, skill, strength, interest, deep interest, achievement, and side project.

| Field | Storage | Notes |
| --- | --- | --- |
| `Category` | required, up to 80 characters | One of the supported categories |
| `Title` | required, up to 200 characters | Owner-entered evidence title |
| `Context` | required, up to 4,000 characters | Preserves prior `Description` text by renaming the existing SQL column |
| `StartDate`, `EndDate` | optional dates | End date cannot precede start date |
| `Contribution`, `Outcome` | optional, up to 4,000 characters each | Owner-entered facts |
| `Metric` | optional, up to 1,000 characters | Missing values remain empty |
| `Tags` | optional, up to 2,000 characters | Comma-separated owner-entered tags |
| `SourceNote` | optional, up to 2,000 characters | Owner-entered reference context |
| `SourceUrl` | optional, up to 2,048 characters | Absolute HTTP or HTTPS URL |

The `PersistCareerEvidence` migration renames `Description` to `Context` without rewriting its content. It adds the other fields as empty/null; it does not infer dates, contributions, outcomes, metrics, tags, or sources.

## Evidence references and review

`CareerDocumentEvidence` and `CareerPlanEvidence` retain owner, document/plan, and evidence IDs. References intentionally remain after an evidence record is deleted so the UI can report it as missing; they do not contain a copy or snapshot of evidence. Affected saved documents and plans keep their text, set `RequiresFactualReview`, clear approval, and rotate their concurrency token. Owners can remove missing plan references explicitly when editing a plan. Document review and plan approval surface missing or changed support.

The application validates that selected evidence IDs belong to the authenticated owner before creating references or generation requests. Queries for referenced evidence are also owner-scoped. The evidence relationships are included in the full-data JSON export and are removed by delete-all.

## Migrations

Migrations are not applied automatically at application startup. Apply reviewed EF migrations to the authorized Azure SQL database before using changed schema:

```sh
dotnet ef database update --project src/CareerManager.Web/CareerManager.Web.csproj --startup-project src/CareerManager.Web/CareerManager.Web.csproj
```

Use the isolated synthetic-data development/test database described in the [README setup](../README.md#development-and-deployment) and [deployment runbook](deployment.md). Do not apply migrations to an unapproved or production database as part of local development.
