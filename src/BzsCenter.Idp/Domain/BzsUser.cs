using Microsoft.AspNetCore.Identity;

namespace BzsCenter.Idp.Domain;

public sealed class BzsUser : IdentityUser<Guid>
{
    public string DisplayName { get; private set; } = string.Empty;
}