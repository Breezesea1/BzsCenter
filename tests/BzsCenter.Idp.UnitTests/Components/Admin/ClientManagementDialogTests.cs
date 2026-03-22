using Bunit;
using BzsCenter.Idp.Components.Admin;
using BzsCenter.Idp.Services.Oidc;
using BzsCenter.Idp.UnitTests.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace BzsCenter.Idp.UnitTests.Components.Admin;

public sealed class ClientManagementDialogTests
{
    [Fact]
    public void Render_WhenInteractiveProfile_ShowsRedirectUriEditors()
    {
        using var context = CreateContext();
        var cut = context.Render<ClientManagementDialog>(parameters => parameters
            .Add(x => x.IsOpen, true)
            .Add(x => x.Profile, OidcClientProfile.FirstPartyInteractive));

        Assert.NotNull(cut.Find("#editor-redirect-uris"));
        Assert.NotNull(cut.Find("#editor-post-logout-uris"));
        Assert.Empty(cut.FindAll("#editor-client-secret"));
    }

    [Fact]
    public void ProfileChange_WhenValid_InvokesProfileChanged()
    {
        using var context = CreateContext();
        var changedTo = OidcClientProfile.FirstPartyInteractive;

        var cut = context.Render<ClientManagementDialog>(parameters => parameters
            .Add(x => x.IsOpen, true)
            .Add(x => x.Profile, OidcClientProfile.FirstPartyInteractive)
            .Add(x => x.ProfileChanged, EventCallback.Factory.Create<OidcClientProfile>(new object(), value => changedTo = value)));

        cut.Find("#editor-profile").Click();
        cut.Find("[data-neo-select-index='1']").Click();

        Assert.Equal(OidcClientProfile.FirstPartyMachine, changedTo);
    }

    [Fact]
    public void Render_WhenEditing_DisablesClientIdAndUsesEditSaveLabel()
    {
        using var context = CreateContext();
        var cut = context.Render<ClientManagementDialog>(parameters => parameters
            .Add(x => x.IsOpen, true)
            .Add(x => x.IsEditing, true)
            .Add(x => x.Profile, OidcClientProfile.FirstPartyMachine));

        Assert.NotNull(cut.Find("#editor-client-id[disabled]"));
        Assert.Contains("SaveChanges", cut.Markup, StringComparison.Ordinal);
        Assert.NotNull(cut.Find("#editor-client-secret"));
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IStringLocalizer<ClientManagement>, TestStringLocalizer<ClientManagement>>();
        return context;
    }
}
