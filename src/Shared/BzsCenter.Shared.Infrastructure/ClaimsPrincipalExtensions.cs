using System.Security.Claims;

namespace BzsCenter.Shared.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    extension(ClaimsPrincipal user)
    {
        public string GetRequiredName()
        {
            var name = user.Identity?.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Authenticated user has no Name claim.");
            }

            return name;
        }
    }
}