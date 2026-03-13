using BzsCenter.Idp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BzsCenter.Idp.UnitTests.TestDoubles;

internal sealed class TestUserManager : UserManager<BzsUser>
{
    private readonly Func<BzsUser, string, bool> _roleEvaluator;

    public TestUserManager(Func<BzsUser, string, bool>? roleEvaluator = null)
        : base(
            Substitute.For<IUserStore<BzsUser>>(),
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            new PasswordHasher<BzsUser>(),
            Array.Empty<IUserValidator<BzsUser>>(),
            Array.Empty<IPasswordValidator<BzsUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<UserManager<BzsUser>>>())
    {
        _roleEvaluator = roleEvaluator ?? ((_, _) => false);
    }

    public override Task<bool> IsInRoleAsync(BzsUser user, string role)
    {
        return Task.FromResult(_roleEvaluator(user, role));
    }
}
