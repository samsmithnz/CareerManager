DECLARE @appPrincipal sysname = N'REPLACE_WITH_APP_SERVICE_NAME';
DECLARE @appPrincipalObjectId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

IF @appPrincipal = N'REPLACE_WITH_APP_SERVICE_NAME'
    OR @appPrincipalObjectId = '00000000-0000-0000-0000-000000000000'
    THROW 50000, 'Replace the placeholders with the App Service name and managed identity object ID.', 1;

IF DATABASE_PRINCIPAL_ID(N'careermanager_runtime') IS NULL
    CREATE ROLE [careermanager_runtime];

IF DATABASE_PRINCIPAL_ID(@appPrincipal) IS NULL
BEGIN
    DECLARE @createUserSql nvarchar(max) =
        N'CREATE USER ' + QUOTENAME(@appPrincipal)
        + N' FROM EXTERNAL PROVIDER WITH OBJECT_ID = '''
        + CONVERT(nvarchar(36), @appPrincipalObjectId) + N''';';
    EXEC sys.sp_executesql @createUserSql;
END;

GRANT SELECT, INSERT ON OBJECT::dbo.CareerOwners TO [careermanager_runtime];
GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.CareerProfiles TO [careermanager_runtime];
GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.CareerEntries TO [careermanager_runtime];
GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.CareerOpportunities TO [careermanager_runtime];
GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.CareerDocuments TO [careermanager_runtime];
GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.CareerDocumentEvidence TO [careermanager_runtime];
GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.CareerPlans TO [careermanager_runtime];
GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.CareerPlanEvidence TO [careermanager_runtime];
GRANT INSERT ON OBJECT::dbo.DataDeletionAuditEvents TO [careermanager_runtime];

DECLARE @addMemberSql nvarchar(max) =
    N'ALTER ROLE [careermanager_runtime] ADD MEMBER ' + QUOTENAME(@appPrincipal) + N';';

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS roleMember
    INNER JOIN sys.database_principals AS rolePrincipal
        ON rolePrincipal.principal_id = roleMember.role_principal_id
    INNER JOIN sys.database_principals AS memberPrincipal
        ON memberPrincipal.principal_id = roleMember.member_principal_id
    WHERE rolePrincipal.name = N'careermanager_runtime'
        AND memberPrincipal.name = @appPrincipal
)
    EXEC sys.sp_executesql @addMemberSql;
