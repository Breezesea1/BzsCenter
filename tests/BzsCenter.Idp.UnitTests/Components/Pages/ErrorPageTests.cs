using System.Diagnostics;
using Bunit;
using BzsCenter.Idp.Client.Components.Pages;
using BzsCenter.Idp.UnitTests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace BzsCenter.Idp.UnitTests.Components.Pages;

public sealed class ErrorPageTests
{
    [Fact]
    public void Error_RendersRecoveryActionsAndTechnicalDetails()
    {
        using var context = new BunitContext();
        context.Services.AddSingleton<IStringLocalizer<Error>, TestStringLocalizer<Error>>();

        using var activity = new Activity("error-page-test").Start();
        Assert.NotNull(activity.Id);

        var cut = context.Render<Error>();

        Assert.Contains("ErrorPrimaryAction", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("ErrorSecondaryAction", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("TechnicalDetailsHeading", cut.Markup, StringComparison.Ordinal);
        Assert.Contains(activity.Id!, cut.Markup, StringComparison.Ordinal);
    }
}
