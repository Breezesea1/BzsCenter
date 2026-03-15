using Xunit;

namespace BzsCenter.Idp.E2ETests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class E2ETestCollection : ICollectionFixture<AppHostFixture>
{
    public const string Name = "BzsCenter E2E";
}
