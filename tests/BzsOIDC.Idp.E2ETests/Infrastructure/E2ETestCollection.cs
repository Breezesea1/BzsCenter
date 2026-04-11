using Xunit;

namespace BzsOIDC.Idp.E2ETests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class E2ETestCollection : ICollectionFixture<AppHostFixture>
{
    public const string Name = "BzsOIDC E2E";
}
