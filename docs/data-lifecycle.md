# Data export and deletion

## Full-data export

`/AccountData/Export` downloads a UTF-8 JSON file with schema version `2`. It contains the registered owner identifier and every owner-scoped persisted SQL record: profiles, career evidence (including context, dates, contribution, outcomes, optional metrics, tags, and source notes/URLs), opportunities, saved documents and comparisons, career plans, approval/review metadata, and document/plan evidence relationships. Enum values are written as strings. Exported records retain their IDs and relationship values so their relationships can be reconstructed. Version 2 renames the legacy evidence `Description` field to `Context` without changing the saved text.

CareerManager currently has no persisted private binaries or blob storage. PDF and DOCX exports are generated on demand and are not retained, so they are not included in the download. Consent for generation is not persisted.

## Delete all application data

The owner-only, CSRF-protected delete action removes all of that owner's application content in one database transaction. A content-free audit record is inserted with a random request ID, UTC timestamp, and outcome in the same database. It contains no owner identifier, email, deleted content, prompt, response, or token. Audit records are retained in the protected application database until an operator removes them; there is no user-facing audit endpoint. The deployed runtime role should have insert-only access to this table; database administrators can access it.

Owner requests are serialized using a SQL Server application lock (and an in-process lock) so an in-flight generation, export, or other request finishes before deletion runs. Deletion leaves the owner registration and configured allowlist intact, allowing that owner to start fresh. SQL changes and the successful audit record commit together; a failed operation rolls back and is safe to retry.

Apply the `PersistCareerEvidence` migration and the `AddDataDeletionAuditEvent` EF migration to the configured Azure SQL database before deploying these features. Delete-all removes all new evidence fields together with their records and references.

This action cannot purge Azure service-managed backups or provider copies; they may retain data until their normal expiration. It does not delete the Microsoft account, and already-downloaded export copies cannot be recalled.
