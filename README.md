# Career Manager

A private workspace for turning your experience, strengths, interests, and projects into thoughtful, career plans that can be used to create role-specific job applications.

ResumeManager brings together your career story and your job search. Capture what you are good at, what you have achieved, and what you love working on; then use that evidence alongside a job description and company values to prepare tailored resumes, cover letters, and career planning documents.

**Status:** Planning. This repository does not yet contain an application. The capabilities below describe the intended MVP, not features available today.

## Why this exists

A resume rarely captures the full picture: side projects, deep interests, recurring obsessions, lessons learned, and achievements all provide useful evidence of fit. ResumeManager aims to make that information reusable without losing the specifics that make it credible.

The goal is to make relevant experience easier for recruiters and hiring managers to recognize, not to promise interviews or manipulate screening systems.

### Finding your career sweet spot

![Venn diagram showing what you are good at, what you like doing, and what makes money overlapping at your career sweet spot.](docs/images/career-sweet-spot.svg)

ResumeManager helps you explore the overlap between your strengths, the work you enjoy, and opportunities that pay. That overlap can guide which roles to pursue, which evidence to highlight, and where to focus your career development.

#### A career-focused lens inspired by ikigai

**Ikigai** is a Japanese concept referring to what makes life feel worth living. It can come from relationships, everyday activities, and interests as well as work; it does not require earning money. The popular four-circle career diagram associated with ikigai is a modern interpretation, not a traditional definition of the concept. It combines what you love, what you are good at, what you can be paid for, and **what the world needs**.

ResumeManager deliberately uses only three circles because its immediate focus is choosing paid roles and preparing credible applications: what you are good at provides evidence of fit, what you like doing guides motivation, and what makes money connects those strengths and interests to paid opportunities. This is a practical career-fit model, not a complete model of purpose or a claim that all meaningful interests should become income.

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

**Requirements:** ASP.NET Core application, hosted in Microsoft Azure.

The following is a proposed starting point, not a finalized architecture:

| Area | Proposed approach |
| --- | --- |
| Application | ASP.NET Core on a supported .NET LTS release, with a server-rendered UI |
| Authentication | Established ASP.NET Core-compatible identity provider; provider and MFA approach to be selected |
| Persistence | Entity Framework Core with Azure SQL Database |
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

## Decisions before implementation

- **Identity:** Which login provider and MFA policy should protect the initial owner account?
- **AI:** Which provider/model, Azure region, budget limits, and retention/training policies are acceptable?
- **Exports:** Which initial downloadable formats are required: PDF, DOCX, or both?
- **Data lifecycle:** What backup retention, recovery targets, and deletion policy are appropriate?

The starting proposal is a private, single-owner application. A broader audience would require an explicit scope change, not just enabling registration.

## Development and deployment

There are no build or run instructions yet because the application has not been scaffolded. The implementation should add local setup instructions, authentication configuration, database migrations, safe sample data, test commands, and deployment instructions as those components land.

Before the first Azure deployment, the owner will need to provision or authorize an Azure subscription/resource group, an identity application, the selected AI service if used, and GitHub Actions deployment access. Use managed identity or federated credentials where supported rather than long-lived deployment secrets.

Do not commit real resumes, personal career records, production database copies, or credentials. Use synthetic data in development and tests.

## Planning

The [1.0 MVP epic](https://github.com/samsmithnz/ResumeManager/issues/1) tracks the implementation sequence, acceptance criteria, and unresolved decisions.
