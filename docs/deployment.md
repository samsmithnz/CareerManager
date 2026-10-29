# Azure deployment runbook

## Status and prerequisites

`infra/main.bicep` and `.github/workflows/deploy-azure.yml` provide repeatable infrastructure and package deployment. They do not mean the production app has been deployed or verified. Do not deploy until the Microsoft sign-in and stable approved-owner mapping are reviewed, the identity settings below are configured, and the synthetic end-to-end checks pass. Production credentials and an authorized Azure subscription/resource group are not available in this repository environment, so live provisioning and the synthetic end-to-end checks remain operator tasks.

The deployment uses no Blob Storage: PDF and DOCX files are generated on demand, are not persisted, and there are no other persisted binaries. The App Service is publicly reachable over HTTPS; the SQL server accepts connections only from the App Service's outbound IP addresses and the administrator's explicitly configured access. Anonymous routes must remain limited to the public landing/privacy pages.

Before provisioning, an authorized operator must choose the Azure subscription/resource group and region, confirm App Service Linux Basic B1 and Azure SQL serverless availability in that region, identify a Microsoft Entra security group for SQL administration, and authorize the GitHub OIDC application. Register the Microsoft identity application and the exact HTTPS redirect URL only after the App Service name is final. Keep its credentials and the approved-owner configuration in the protected App Service configuration; never put them in this repository or workflow logs.

## Microsoft account sign-in and owner setup

Register a confidential web application with Microsoft identity platform and enable **personal Microsoft accounts only**. Configure its redirect URI as `https://<app-service-host>/signin-microsoft` (and `https://localhost:7194/signin-microsoft` for the local HTTPS launch profile when used). Create a client secret and keep it only in protected application configuration. The app uses the `consumers` OpenID Connect authority, authorization-code flow with PKCE, and an eight-hour secure, HttpOnly cookie; it does not request or retain access tokens.

Configure these application settings before enabling private access:

- `Authentication__Microsoft__ClientId`
- `Authentication__Microsoft__ClientSecret`
- `Authentication__Microsoft__OwnerSubject` — the configured owner's exact validated OIDC `sub` claim, not an email address
- `Authentication__Microsoft__OwnerId` — the internal `CareerOwners.Id` to which that subject maps

Obtain the `sub` claim through a trusted operator process that does not persist or log the ID token. Do not use the mutable email claim as an owner key. The application rejects other subjects, does not create owner records during sign-in, and denies private access when the owner mapping is missing. After applying the initial database migration, provision the owner record explicitly using the same internal ID, for example in a trusted SQL administration session:

```sql
INSERT INTO dbo.CareerOwners (Id) VALUES (N'<configured-owner-id>');
```

For local development, store values with `dotnet user-secrets set "Authentication:Microsoft:ClientId" "<client-id>"`, `dotnet user-secrets set "Authentication:Microsoft:ClientSecret" "<client-secret>"`, `dotnet user-secrets set "Authentication:Microsoft:OwnerSubject" "<validated-subject>"`, and `dotnet user-secrets set "Authentication:Microsoft:OwnerId" "<internal-owner-id>"`. In Azure, set the same values in the App Service's protected configuration before deployment. A missing setting fails closed for private access and sign-in; there is no public registration or first-visitor enrollment.

Check current regional availability before deploying:

```sh
az account set --subscription "<authorized-subscription-id>"
az appservice list-locations --sku B1 --linux-workers-enabled true --output table
az sql db list-editions --location "<region>" --output table
```

Use an App Service **Basic B1 Linux** plan because Free/Shared plans do not provide the managed identity required for Azure SQL access; see [App Service managed identities](https://learn.microsoft.com/azure/app-service/overview-managed-identity). B1 is always-on and billed while provisioned; it is not a free production tier and has limited capacity and no deployment slots. The Bicep creates a **General Purpose serverless, one-vCore maximum, 0.5-vCore minimum, 32-GB** Azure SQL database with a 60-minute auto-pause and local backup redundancy. Auto-pause can delay the first request after inactivity; database storage and any usage outside an eligible free offer can still incur charges. The Azure SQL free offer is subscription- and region-dependent and has usage/storage limits; do not assume eligibility or zero cost. Exact monthly costs cannot be stated until the operator selects a region, subscription, and verified offer; the always-on B1 plan is a billed baseline. Review live prices and offer eligibility in the [App Service pricing page](https://azure.microsoft.com/pricing/details/app-service/linux/), [Azure SQL free offer](https://learn.microsoft.com/azure/azure-sql/database/free-offer?view=azuresql), and [Azure pricing calculator](https://azure.microsoft.com/pricing/calculator/) before confirming deployment. Recheck costs after deployment and set an Azure budget alert. No Blob, Key Vault, Application Insights, private endpoint, or premium plan is provisioned.

## Provision Azure resources

Create or select the authorized resource group, and use an existing Microsoft Entra security group as the SQL administrator. Preview and apply the Bicep template:

```sh
az deployment group what-if \
  --resource-group "<resource-group>" \
  --template-file infra/main.bicep \
  --parameters appServiceName="<globally-unique-app-name>" \
               sqlServerName="<globally-unique-sql-name>" \
               sqlDatabaseName="CareerManager-Production" \
               sqlAdminGroupName="<entra-group-display-name>" \
               sqlAdminGroupObjectId="<entra-group-object-id>" \
               location="<verified-region>"

az deployment group create \
  --resource-group "<resource-group>" \
  --template-file infra/main.bicep \
  --parameters appServiceName="<globally-unique-app-name>" \
               sqlServerName="<globally-unique-sql-name>" \
               sqlDatabaseName="CareerManager-Production" \
               sqlAdminGroupName="<entra-group-display-name>" \
               sqlAdminGroupObjectId="<entra-group-object-id>" \
               location="<verified-region>"
```

The SQL server is configured for Microsoft Entra-only authentication. Bicep enables HTTPS-only access, TLS 1.2, FTPS-off, and a system-assigned App Service identity. The SQL firewall uses a second template because App Service outbound addresses are only known after the first deployment. Retrieve and apply the current addresses:

```sh
APP_SERVICE_IPS="$(az deployment group show \
  --resource-group "<resource-group>" \
  --name "<main-deployment-name>" \
  --query properties.outputs.appServiceOutboundIpAddresses.value \
  --output json)"

az deployment group create \
  --resource-group "<resource-group>" \
  --template-file infra/sql-firewall.bicep \
  --parameters sqlServerName="<globally-unique-sql-name>" \
               appServiceOutboundIpAddresses="$APP_SERVICE_IPS"
```

The firewall is public-endpoint/IP-restricted, not a private endpoint. Add the operator's current public IP as a temporary SQL firewall rule before connecting for migrations and permissions, then remove that rule when setup is complete. If App Service outbound addresses change, apply the firewall template with the current addresses and remove obsolete `app-service-*` rules before relying on SQL access. The template creates no SQL password and does not grant application database permissions.

After the initial EF migration creates the schema, connect as the configured SQL administrator using Microsoft Entra authentication and run `infra/sql/grant-app-runtime.sql` after replacing its placeholders with the exact App Service name and `appServicePrincipalId` output by Bicep. This creates a custom runtime role: it grants only the table operations needed by the app and **INSERT only** on `DataDeletionAuditEvents`; it does not grant schema changes or audit reads/updates/deletes. The App Service identity must not be used as the migration identity.

## GitHub OIDC and package deployment

Create an Entra application/service principal for GitHub Actions and a federated credential with subject exactly `repo:samsmithnz/CareerManager:environment:production` and issuer `https://token.actions.githubusercontent.com`. Grant it only `Website Contributor` on the target App Service (or the narrowest role that permits package deployment); do not grant subscription-wide Contributor. In GitHub, create a protected `production` environment, restrict deployment branches to `main`, and require an operator approval. Add these environment variables (IDs are identifiers, not credentials):

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `AZURE_RESOURCE_GROUP`
- `AZURE_WEBAPP_NAME`

No client secret, publish profile, SQL password, or career data is used by the deployment workflow. The deploy workflow can be manually dispatched from `main`; leave `source_sha` empty for the selected workflow commit or provide a full SHA that is already reachable from `main` to redeploy/roll back application code. The workflow validates that restriction, restores/builds/tests the selected revision, and deploys a ZIP package via Azure CLI. Review CI results and the production environment approval before allowing deployment.

The Bicep provisioning operation is intentionally separate from application deployment. Run it from an authorized operator environment after reviewing `what-if`; do not give the app or deploy identity infrastructure-owner rights.

## Database migration and release procedure

Migrations are **not** run automatically by application startup or the deployment workflow. Before each release:

1. Review the migration diff, generated SQL, and its compatibility with the current app version and production data. Take into account Azure SQL's service-managed recovery only; this project has no application-level backup/restore or snapshots.
2. Plan an operator-approved maintenance window when a change is not backward-compatible. Prepare a forward-fix or restore/recovery plan before applying destructive schema changes; do not assume an EF `Down` migration safely restores deleted/changed data.
3. Use a separate, temporary Microsoft Entra migration principal. Have the SQL administrator create it and temporarily add it to `db_ddladmin`, `db_datareader`, and `db_datawriter`; EF must inspect and update its migration history as well as perform schema changes. These temporary data roles can read and modify all application tables, so use a trusted operator environment and revoke all three roles immediately after migration. Never add DDL rights to the App Service identity. Run EF from a trusted, network-authorized operator machine with an Entra-authenticated connection string supplied through a protected environment variable:

   ```sh
   ASPNETCORE_ENVIRONMENT=Production \
   ConnectionStrings__CareerManager="Server=tcp:<sql-server>.database.windows.net,1433;Database=CareerManager-Production;Encrypt=True;TrustServerCertificate=False;Authentication=Active Directory Default;" \
     dotnet ef database update \
       --project src/CareerManager.Web/CareerManager.Web.csproj \
       --startup-project src/CareerManager.Web/CareerManager.Web.csproj
   ```

   Authenticate the operator with `az login` first. Do not put the connection string in shell history or logs. Revoke the temporary migration rights after confirming the migration.
4. Dispatch the protected deployment workflow and approve the release. Verify the app starts and that the expected migration is applied before allowing the owner workflow.
5. If application code must be rolled back, dispatch the last known-good main commit SHA. A code rollback does **not** reverse a schema migration. For incompatible migrations, use the pre-approved forward-fix or explicit database recovery plan; do not run `dotnet ef database update <older-migration>` as an assumed safe rollback.

## Access, data handling, and operations

- Require HTTPS, secure/HttpOnly session cookies, CSRF protection, Microsoft sign-in, and server-side owner checks. Verify anonymous and authenticated non-owner requests to every private controller and every PDF, DOCX, and JSON download return 401/403 without data.
- Keep the generation provider set to `Local` unless the owner has reviewed the remote provider's regional availability, current terms/retention, explicit-consent flow, and spending limits. Never put AI keys or identity secrets in source control or logs.
- Production application logs default to warnings. Keep request/response bodies, query values, career content, tokens, prompts, and provider responses out of diagnostics. Do not enable HTTP request logging without a demonstrated redaction and access-control review. Deletion audit events remain in the SQL database; the runtime identity can insert but not read, update, or delete them. SQL administrators can access the audit table.
- Azure SQL point-in-time restore uses service-managed backups with a 7-day default retention for this General Purpose database; retention can be configured up to 35 days. Bicep requests locally redundant backup storage, so this configuration does not provide a regional backup copy. Verify the actual retention and free-offer backup limits in the Azure portal after provisioning. These platform backups are not an application-level restore feature or a completed recovery exercise. See [Azure SQL automated backups](https://learn.microsoft.com/azure/azure-sql/database/automated-backups-overview?view=azuresql).
- App Service diagnostic logging, Application Insights, and Log Analytics are not provisioned. The app defaults to warning-level logs; do not enable request-body or query-value logging without a reviewed redaction/access policy. Microsoft sign-in logs and any remote AI provider's abuse-monitoring or retention copies are governed by those services' current policies, not CareerManager. Remote AI is disabled by default; review the then-current data-handling terms before enabling. Users' downloaded exports cannot be recalled.
- Check App Service health/startup, Azure SQL availability/auto-pause, deployment history, service health, cost alerts, and deletion failures after each release. Avoid enabling telemetry that captures personal data. Record smoke-test outcomes without storing real career data or sensitive request/response payloads.
- Application-level backup/restore and recovery exercises remain Phase 2.

## Synthetic release verification

Run this checklist only after issues 1.2 and 1.8 prerequisites are complete and the authorized owner has deployed the app. Use a dedicated test owner and synthetic content only. Record pass/fail, UTC time, deployed commit, and redacted evidence in the release record; do not attach career data, cookies, tokens, or request bodies.

- [ ] Sign in as the configured owner; verify an unapproved Microsoft account cannot enter the workspace.
- [ ] Create, edit, search, and remove synthetic career evidence; create and update an opportunity.
- [ ] Preview selected evidence, verify consent is required before AI generation, review a draft's factual references, and explicitly approve it.
- [ ] Edit/regenerate approved content and confirm approval resets.
- [ ] Download the current resume and cover letter as both PDF and DOCX; compare each download with the approved content.
- [ ] Create, edit, and revisit a career plan; test approval reset after content changes.
- [ ] Export all owner data; verify the JSON contains the expected synthetic records.
- [ ] Delete all data; verify owner content is gone and the content-free audit record exists. Exercise failure/retry behavior against an isolated test environment; confirm a failed transaction leaves content intact and retry creates a valid outcome audit record.
- [ ] Request private pages and downloads anonymously and as a signed-in non-owner. Confirm they fail closed and reveal no private data.
- [ ] Complete a keyboard-only smoke check of sign-in, evidence/opportunity forms, consent, document review/approval, downloads, planning, export, and delete confirmation.

Do not mark deployment acceptance complete until every checklist item passes in the deployed HTTPS environment. This runbook and template are not evidence that the deployment or those manual checks have occurred.
