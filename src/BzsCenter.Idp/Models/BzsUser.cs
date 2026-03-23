using Microsoft.AspNetCore.Identity;

namespace BzsCenter.Idp.Models;

public sealed class BzsUser : IdentityUser<Guid>
{
    public string DisplayName { get; private set; } = string.Empty;

    public static BzsUser CreateExternal(string userName, string? email, string? displayName)
    {
        var normalizedUserName = NormalizeRequiredUserName(userName);
        var normalizedEmail = NormalizeOptionalEmail(email);

        return new BzsUser
        {
            UserName = normalizedUserName,
            Email = normalizedEmail,
            EmailConfirmed = normalizedEmail is not null,
            DisplayName = NormalizeDisplayName(displayName, normalizedUserName),
        };
    }

    public void UpdateDisplayName(string? displayName)
    {
        DisplayName = NormalizeDisplayName(displayName, NormalizeRequiredUserName(UserName));
    }

    private static string NormalizeRequiredUserName(string? userName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        return userName.Trim();
    }

    private static string? NormalizeOptionalEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    private static string NormalizeDisplayName(string? displayName, string fallbackUserName)
    {
        return string.IsNullOrWhiteSpace(displayName) ? fallbackUserName : displayName.Trim();
    }
}
