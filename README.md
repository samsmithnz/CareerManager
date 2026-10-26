# Career Manager

A private workspace to understand what you’re good at, what work energizes you, and where you want to go next. CareerManager helps you turn that insight into a practical career plan—and, when you’re ready, use your experience to prepare thoughtful, role-specific applications.

**Status:** The .NET 10 MVC, Azure SQL, and CI foundation is implemented. Authentication and career features are not yet implemented; the current application exposes only the public MVC skeleton.

## Why this exists

A resume rarely captures the full picture: side projects, deep interests, recurring obsessions, lessons learned, and achievements all provide useful evidence of fit. CareerManager aims to make that information reusable without losing the specifics that make it credible.

The goal is to make relevant experience easier for recruiters and hiring managers to recognize, not to promise interviews or manipulate screening systems.

### Finding your career sweet spot

![Venn diagram showing what you are good at, what you like doing, and what makes money overlapping at your career sweet spot.](docs/images/career-sweet-spot.svg)

CareerManager helps you explore the overlap between your strengths, the work you enjoy, and opportunities that pay. That overlap can guide which roles to pursue, which evidence to highlight, and where to focus your career development.

#### A career-focused lens inspired by ikigai

**Ikigai** is a Japanese concept referring to what makes life feel worth living. It can come from relationships, everyday activities, and interests as well as work; it does not require earning money. The popular four-circle career diagram associated with ikigai is a modern interpretation, not a traditional definition of the concept. It combines what you love, what you are good at, what you can be paid for, and **what the world needs**.

CareerManager deliberately uses only three circles because its immediate focus is choosing paid roles and preparing credible applications: what you are good at provides evidence of fit, what you like doing guides motivation, and what makes money connects those strengths and interests to paid opportunities. This is a practical career-fit model, not a complete model of purpose or a claim that all meaningful interests should become income.

Leaving "what the world needs" out of the diagram does not mean impact is unimportant, or that market demand is the same as social value. Consider contribution, company values, and the problems you want to help solve when evaluating individual roles and building your career plan.

## Core workflow

1. Sign in to your private workspace.
2. Build a reusable career profile with experience, skills, strengths, interests, achievements, and side projects.
3. Add a role, its job description, and relevant company values or context.
4. Compare the opportunity with your evidence, identifying strengths, gaps, and questions.
5. Generate and edit a tailored resume and cover letter, reviewing the evidence behind the suggestions.
6. Save the application version, export the documents, and track the role through the hiring process.
7. Use recurring strengths, interests, and gaps to maintain a simple career plan.

## MVP scope

### Career knowledge base

- Capture work experience, education, skills, strengths, interests, deep interests/obsessions, successes, and side projects.
- Record achievements with context, your contribution, outcomes, and optional measurable results.
- Attach evidence references such as project URLs and source notes; tag entries for reuse.
- Edit and remove entries as your career evolves.

### Roles and applications

- Create a role record with company, title, job URL, job description, company values, notes, and important dates.
- Start with manually entered or pasted job descriptions and company context; retain source URLs when available.
- Track roles through saved, preparing, applied, interviewing, offer, rejected, or withdrawn states.
- Keep generated drafts, edited versions, and the exact documents used for each application together.

### Tailored documents and career planning

- Use the career profile, job description, and company context to propose relevant experience and language.
- Produce a resume and cover letter for each role, with a visible link from factual claims to supporting profile entries.
- Highlight missing evidence and uncertain matches instead of inventing qualifications, dates, employers, responsibilities, or results.
- Allow editing and explicit user approval before a document is marked final.
- Preserve approved versions so later profile edits do not change an application already sent.
- Export a simple, text-based, applicant-tracking-system-friendly resume and cover letter. Initial download formats still need to be selected.
- Create an editable career planning document covering target roles, strengths, development gaps, and next actions.

### Privacy and security

Career information and application history are private data. Security is a core requirement, not a later enhancement.

- Require login for all career records, role records, and document access; do not enable public registration in the initial personal-use MVP.
- Enforce server-side ownership checks for every read, write, generation, and export operation.
- Use an established authentication solution, secure session cookies, HTTPS, and protection against cross-site request forgery.
- Use encryption at rest through the selected Azure services and managed identity where supported; keep secrets out of source control.
- Keep resumes, job descriptions, personal data, access tokens, and AI prompts/responses out of application logs.
- Before enabling AI processing, show what data will leave the application and obtain explicit user consent. Provider retention, training use, and deployment-region requirements must be agreed before production use.
- Treat pasted job descriptions, company text, and model output as untrusted input; never let them override application rules or access other users' data.
- Provide personal data export and deletion, including generated documents; explain backup retention and deletion limitations.

## Technology and hosting

The implemented foundation is one ASP.NET Core MVC application targeting .NET 10, Entity Framework Core with the SQL Server provider, and Azure SQL Database. The app is compatible with a single Azure App Service deployment. The initial database model outlines an owner, one profile per owner, and owner-keyed career entries; it does not yet implement authentication or expose career data through endpoints.

The rest of the MVP remains planned:

| Area | Proposed approach |
| --- | --- |
| Application | ASP.NET Core MVC on .NET 10, with a server-rendered UI |
| Authentication | Established ASP.NET Core-compatible identity provider; provider and MFA approach to be selected |
| Persistence | Entity Framework Core SQL Server provider with Azure SQL Database |
| Hosting | Azure App Service |
| Document storage | Private Azure Blob Storage if binary exports require persistent storage |
| AI-assisted drafting | Provider abstraction kept small; provider, model, region, and data handling to be selected |
| Secrets and access | Managed identity and Azure Key Vault where needed |
| Operations | Azure Monitor/Application Insights with sensitive-data logging disabled |
| Delivery | GitHub Actions, automated checks, and repeatable Azure infrastructure configuration |

Prefer a single deployable application for the MVP. Avoid microservices and additional infrastructure unless a demonstrated requirement calls for them.

## Quality bar

- No fabricated experience or unsupported factual claims in approved documents.
- Clear errors and recoverable generation failures; failed requests must not overwrite saved documents.
- Private data cannot be accessed without authentication or by another account.
- Readable, keyboard-accessible forms and document previews.
- Automated coverage for ownership boundaries, role tracking, document versioning, and generation validation.
- A repeatable deployment process and a documented, exercised backup/restore procedure before production use.

## Not in the first MVP

- Automated job scraping, bulk applications, or submitting applications on your behalf.
- Email/calendar integrations, recruiter portals, or public profile sharing.
- Complex resume design tools, multiple visual themes, or guarantees about screening-system rankings.
- Automatic ingestion of every existing document or external profile.
- A commercial multi-tenant service, billing, or collaboration.

## Decisions for later feature work

- **Identity:** Which login provider and MFA policy should protect the initial owner account? Until selected and implemented, no career-data endpoints should be added.
- **AI:** Which provider/model, Azure region, budget limits, and retention/training policies are acceptable?
- **Exports:** Which initial downloadable formats are required: PDF, DOCX, or both?
- **Data lifecycle:** What backup retention, recovery targets, and deletion policy are appropriate?

The starting proposal is a private, single-owner application. A broader audience would require an explicit scope change, not just enabling registration.

## Development and deployment

### Prerequisites and environment boundaries

- Install the .NET SDK pinned in [`global.json`](global.json) (10.0.401).
- Provision or obtain authorization to a **separate synthetic-data Azure SQL development database** and allow the development machine to connect through the Azure SQL firewall. `CareerManager-Dev` or `CareerManager-Development` is the documented database name.
- Configure Azure SQL connections with encryption and server-certificate validation enabled (`Encrypt=True;TrustServerCertificate=False`).
- Store the development connection string in .NET User Secrets; do not put it in `appsettings*.json`, source control, or logs:

  ```sh
  dotnet user-secrets set "ConnectionStrings:CareerManager" "<authorized Azure SQL connection string>" --project src/CareerManager/CareerManager.csproj
  ```

- Use a different, isolated Azure SQL database named `CareerManager-Test` or `CareerManager-Testing` for database-dependent tests. The test command accepts its connection string only through `CAREERMANAGER_TEST_SQL_CONNECTION`; the integration workflow reads the repository secret `AZURE_SQL_TEST_CONNECTION_STRING`.
- Production configuration is separate: deploy with `ASPNETCORE_ENVIRONMENT=Production` and provide `ConnectionStrings__CareerManager` through the App Service configuration or an approved secret store. Do not reuse development or test credentials or databases. Provisioning and deployment are part of issue 1.9.
- For development/test cost, use the Azure SQL Database free offer when eligible; otherwise choose the lowest currently supported General Purpose serverless compute with auto-pause and the smallest suitable storage. Confirm subscription, region, offer limits, and current pricing in the [Azure SQL free offer](https://learn.microsoft.com/azure/azure-sql/database/free-offer?view=azuresql) and [serverless tier documentation](https://learn.microsoft.com/azure/azure-sql/database/serverless-tier-overview?view=azuresql) before provisioning. No Azure resources or credentials are included here.
- The application fails clearly at startup when `ConnectionStrings:CareerManager` is missing. It does not automatically apply migrations or seed data.

### Restore, build, and run

From the repository root:

```sh
dotnet restore CareerManager.sln
dotnet tool restore
dotnet build CareerManager.sln --configuration Release --no-restore
dotnet test tests/CareerManager.Tests/CareerManager.Tests.csproj --configuration Release
dotnet run --project src/CareerManager
```

The unit tests do not require Azure credentials or a live database. `dotnet run` uses the Development environment from the launch profile and reads the connection string from User Secrets.

### Migrations and synthetic development data

Set `ASPNETCORE_ENVIRONMENT=Development` when running EF commands so the design-time factory can read Development User Secrets. Apply the initial migration only to the authorized development database:

```sh
dotnet ef database update --project src/CareerManager/CareerManager.csproj --startup-project src/CareerManager/CareerManager.csproj
```

Synthetic data is opt-in, repeatable, and guarded to the Development environment and a database named `CareerManager-Dev` or `CareerManager-Development`. It is never applied during application startup:

```sh
CAREERMANAGER_ENABLE_SYNTHETIC_SEED=true dotnet run --project src/CareerManager -- --seed-synthetic
```

Use `dotnet ef migrations add <MigrationName> --project src/CareerManager/CareerManager.csproj --startup-project src/CareerManager/CareerManager.csproj` to add future migrations.

### Azure SQL integration tests and CI

The integration-test project is intentionally separate from the solution so the normal solution test command remains database-free. Run the database-dependent test explicitly against the isolated `CareerManager-Test` database:

```sh
CAREERMANAGER_TEST_SQL_CONNECTION="<authorized test database connection string>" \
  dotnet test tests/CareerManager.IntegrationTests/CareerManager.IntegrationTests.csproj --configuration Release
```

Source `CAREERMANAGER_TEST_SQL_CONNECTION` from an approved local secret facility; do not put its value in source control or shell history. The integration test fails with a setup message if the connection is missing and rejects database names that are not clearly marked as test. It applies migrations, uses a unique test owner, and deletes that owner's data in a `finally` block. Schema migration history remains in the isolated test database.

The standard GitHub Actions CI restores, builds, audits dependencies, and runs only database-free unit tests; it needs no Azure credentials. To run SQL tests in GitHub Actions, configure the `AZURE_SQL_TEST_CONNECTION_STRING` repository secret for the isolated test database and dispatch the **Azure SQL integration tests** workflow. The workflow fails explicitly when that secret is absent. NuGet audit warnings fail the build, pull requests also run dependency review, and Dependabot checks NuGet and GitHub Actions updates.

No local database, SQLite substitute, or database container is used. The initial capability is only the MVC skeleton and database schema: authentication, ownership enforcement at the application layer, and all career features remain future work. The public landing/privacy pages contain no career data; do not expose private career endpoints until authentication is implemented.

Before Azure deployment, the owner will need to provision or authorize an Azure subscription/resource group, an identity application, the selected AI service if used, and GitHub Actions deployment access. Use managed identity or federated credentials where supported rather than long-lived deployment secrets.

Do not commit real resumes, personal career records, production database copies, or credentials. Use synthetic data in development and tests.

## Planning

The [1.0 MVP epic](https://github.com/samsmithnz/CareerManager/issues/1) tracks the implementation sequence, acceptance criteria, and unresolved decisions.
