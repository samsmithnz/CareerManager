using System.ComponentModel.DataAnnotations;

namespace CareerManager.ViewModels;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class HttpUrlAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return true;
        }

        return Uri.TryCreate(value.ToString(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrWhiteSpace(uri.Host);
    }

    public override string FormatErrorMessage(string name) =>
        $"{name} must be an absolute HTTP or HTTPS URL.";
}
