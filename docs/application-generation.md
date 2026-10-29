# Application generation defaults

The first implementation deliberately uses `DeterministicApplicationGenerationProvider` (`keyword-overlap-v1`) instead of an external language-model service. It compares role-description terms with only the evidence the owner selected, copies factual text verbatim into draft claims, and flags unmatched requirements and evidence without metrics for review. It does not interpret pasted instructions, use tools, make network requests, or invent missing career facts.

The optional remote provider target is Azure OpenAI with a regional `gpt-4o-mini` deployment in East US 2 (Virginia; Azure region name `eastus2`). It is not enabled by default. The provider rejects any other configured region, and the consent preview identifies East US 2 before a request.

## Region and data handling

Generation runs in the same application process and deployment region as CareerManager. The provider has no separate region, account, retention policy, or training use because no third-party model receives the request. Existing application/database hosting and its configured region remain responsible for storage and access. The consent checkbox is still required for every generation request, and the page previews the exact role and selected evidence being processed. No career content is written to application logs.

The remote service is only enabled when an administrator configures the Azure endpoint, deployment, `Region=eastus2`, API key, and `ApplicationGeneration:AzureOpenAI:DataHandlingApproved=true`. Before setting that flag, the owner must review the currently applicable Microsoft contractual data-processing terms, retention/abuse-monitoring behavior, training use, subprocessors, and regional processing guarantees, and confirm the selected deployment is available in East US 2. Remote mode makes no claim that these terms have already been approved. The consent preview names the configured provider, deployment, and region and displays the exact role/evidence content being sent. Keep the key in an approved secret store or environment configuration; never put it in source control or logs.

## Request and spending limits

- At most 10 selected evidence entries and 20,000 combined input characters per request.
- At most 3 generation attempts per owner per minute per application instance.
- At most 10 attempts per owner per UTC day per application instance.
- Provider execution is cancelled after 15 seconds.
- Remote responses are limited to 2,500 generated tokens and 512 KiB; the local provider has no metered model usage or external request cost.
- Invalid provider output, limits, and provider failures return actionable errors and do not save partial documents.

These application-level request and token limits are not a monetary hard cap. Before enabling remote mode, also configure an Azure-side quota and spending alert/limit appropriate to the deployment. The local provider has no external usage cost.

## Review, approval, and document exports

Resume and cover-letter drafts can be explicitly approved after the owner reviews every factual claim and confirms unsupported claims have been resolved. Evidence links are references only and are not treated as proof. Approval is bound to the exact saved title and text, and editing clears it. Approval and export both require the current document concurrency token.

PDF and DOCX files are generated on demand from the current saved content through owner-authorized MVC endpoints. Exports are not persisted, so no blob account, public URL, retained artifact, or export cleanup is required. Stale links and changes detected during generation are rejected, and downloads are marked `no-store`. Drafts can still be exported; exporting does not approve them.

The PDF uses PDFsharp with a bundled DejaVu Sans font (Bitstream Vera Font License; notice at `src/CareerManager.Web/Fonts/DejaVuSans.LICENSE.txt`), and the DOCX uses the Open XML SDK. Both use a plain, readable layout. Apply the `AddDocumentApproval` database migration before using approval. Export failures return an error without changing saved document content.

## Setup

No AI account, API key, deployment, or paid model call is required for local generation. To enable remote generation, provision and authorize the selected Azure OpenAI East US 2 deployment, review its current handling terms, configure the settings above and the required quota, then explicitly enable it. Confirm current `gpt-4o-mini` availability and capacity in Azure before provisioning. Apply the `AddApplicationDrafts` database migration to the authorized database before using generated drafts. Automated tests use local deterministic/fake providers only.
