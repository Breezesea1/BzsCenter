using BzsCenter.Idp.Components.Admin;

namespace BzsCenter.Idp.UnitTests.Components.Admin;

public sealed class AdminTableSearchTests
{
    [Fact]
    public void Score_WhenQueryEmpty_ReturnsZero()
    {
        var score = AdminTableSearch.Score(string.Empty, "alpha");

        Assert.Equal(0, score);
    }

    [Fact]
    public void Score_WhenContainsMatch_ReturnsScore()
    {
        var score = AdminTableSearch.Score("alice", "alice@example.com");

        Assert.NotNull(score);
    }

    [Fact]
    public void Score_WhenSubsequenceMatch_ReturnsScore()
    {
        var score = AdminTableSearch.Score("clnt", "client-service");

        Assert.NotNull(score);
    }

    [Fact]
    public void Score_WhenNoMatch_ReturnsNull()
    {
        var score = AdminTableSearch.Score("zzz", "alpha", "beta");

        Assert.Null(score);
    }
}
