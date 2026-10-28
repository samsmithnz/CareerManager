# Application generation defaults

The first implementation deliberately uses `DeterministicApplicationGenerationProvider` (`keyword-overlap-v1`) instead of an external language-model service. It compares role-description terms with only the evidence the owner selected, copies factual text verbatim into draft claims, and flags unmatched requirements and evidence without metrics for review. It does not interpret pasted instructions, use tools, make network requests, or invent missing career facts.

The optional remote provider target is Azure OpenAI with a regional `gpt-4o-mini` deployment in Australia East. It is not enabled by default. The configured provider name, deployment, and region are shown in the consent preview before a request.

## Region and data handling

Generation runs in the same application process and deployment region as CareerManager. The provider has no separate region, account, retention policy, or training use because no third-party model receives the request. Existing application/database hosting and its configured region remain responsible for storage and access. The consent checkbox is still required for every generation request, and the page previews the exact role and selected evidence being processed. No career content is written to application logs.

The remote service is only enabled when an administrator configures the Azure endpoint, deployment, region, API key, and `ApplicationGeneration:AzureOpenAI:DataHandlingApproved=true`. Before setting that flag, the owner must review the currently applicable Microsoft contractual data-processing terms, retention/abuse-monitoring behavior, training use, subprocessors, and regional processing guarantees, and confirm the selected deployment is available in Australia East. Remote mode makes no claim that these terms have already been approved. The consent preview names the configured provider, deployment, and region and displays the exact role/evidence content being sent. Keep the key in an approved secret store or environment configuration; never put it in source control or logs.

## Request and spending limits

- At most 10 selected evidence entries and 20,000 combined input characters per request.
- At most 3 generation attempts per owner per minute per application instance.
- At most 10 attempts per owner per UTC day per application instance.
- Provider execution is cancelled after 15 seconds.
- Remote responses are limited to 2,500 generated tokens and 512 KiB; the local provider has no metered model usage or external request cost.
- Invalid provider output, limits, and provider failures return actionable errors and do not save partial documents.

These application-level request and token limits are not a monetary hard cap. Before enabling remote mode, also configure an Azure-side quota and spending alert/limit appropriate to the deployment. The local provider has no external usage cost.

## Setup

No AI account, API key, deployment, or paid model call is required for local generation. To enable remote generation, provision and authorize the selected Azure OpenAI regional deployment, review its current handling terms, configure the settings above and the required quota, then explicitly enable it. Apply the `AddApplicationDrafts` database migration to the authorized database before using generated drafts. Automated tests use local deterministic/fake providers only.
