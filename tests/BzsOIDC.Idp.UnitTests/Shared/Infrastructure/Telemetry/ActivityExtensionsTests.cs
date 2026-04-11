using System.Diagnostics;
using BzsOIDC.Shared.Infrastructure.Telemetry;

namespace BzsOIDC.Idp.UnitTests.Shared.Infrastructure.Telemetry;

public sealed class ActivityExtensionsTests
{
    [Fact]
    public void SetExceptionTags_WhenActivityNull_DoesNotThrow()
    {
        Activity? activity = null;

        var exception = Record.Exception(() => activity.SetExceptionTags(new InvalidOperationException("boom")));

        Assert.Null(exception);
    }

    [Fact]
    public void SetExceptionTags_WhenActivityPresent_AddsOpenTelemetryExceptionTags()
    {
        using var activity = new Activity("test-activity");
        activity.Start();

        var exception = new InvalidOperationException("boom");

        activity.SetExceptionTags(exception);

        var tags = activity.Tags.ToDictionary(static tag => tag.Key, static tag => tag.Value);
        Assert.Equal("boom", tags["exception.message"]);
        Assert.Contains(nameof(InvalidOperationException), tags["exception.stacktrace"], StringComparison.Ordinal);
        Assert.Equal(typeof(InvalidOperationException).FullName, tags["exception.type"]);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
    }
}
