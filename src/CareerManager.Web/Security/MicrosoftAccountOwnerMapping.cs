namespace CareerManager.Security;

public sealed class MicrosoftAccountOwnerMapping(string? approvedSubject, string? ownerId)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(approvedSubject)
        && approvedSubject.Length <= 256
        && !string.IsNullOrWhiteSpace(ownerId)
        && ownerId.Length <= 256;

    public bool TryMapValidatedSubject(string? subject, out string mappedOwnerId)
    {
        mappedOwnerId = string.Empty;
        if (!IsConfigured
            || string.IsNullOrWhiteSpace(subject)
            || !string.Equals(subject, approvedSubject, StringComparison.Ordinal))
        {
            return false;
        }

        mappedOwnerId = ownerId!;
        return true;
    }
}
