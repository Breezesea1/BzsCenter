using Microsoft.AspNetCore.Identity;

namespace BzsCenter.Idp.Models;

public sealed class BzsUser : IdentityUser<Guid>
{
    public string DisplayName { get; private set; } = string.Empty;
}
