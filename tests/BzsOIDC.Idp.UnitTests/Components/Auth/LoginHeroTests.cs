using Bunit;
using BzsOIDC.Idp.Components.Auth.Shared;

namespace BzsOIDC.Idp.UnitTests.Components.Auth;

public sealed class LoginHeroTests
{
    [Fact]
    public void Render_ImportsModuleAndUsesConfiguredInputIds()
    {
        using var context = CreateContext();
        var module = context.JSInterop.SetupModule("./Components/Auth/Shared/LoginHero.razor.js");
        var init = module.SetupVoid("initHeroTracking", _ => true);

        var cut = context.Render<LoginHero>(parameters => parameters
            .Add(x => x.UserNameInputId, "login-user")
            .Add(x => x.PasswordInputId, "login-password")
            .Add(x => x.BrandName, "BzsOIDC")
            .Add(x => x.PrivacyPolicyText, "Privacy")
            .Add(x => x.TermsOfServiceText, "Terms"));

        Assert.NotNull(cut.Find(".login-hero"));
        Assert.Equal("login-user", cut.Find(".hero-art").GetAttribute("data-username-input-id"));
        Assert.Equal("login-password", cut.Find(".hero-art").GetAttribute("data-password-input-id"));
        init.VerifyInvoke("initHeroTracking");
    }

    [Fact]
    public async Task Dispose_CallsHeroCleanup()
    {
        using var context = CreateContext();
        var module = context.JSInterop.SetupModule("./Components/Auth/Shared/LoginHero.razor.js");
        module.SetupVoid("initHeroTracking", _ => true).SetVoidResult();
        var dispose = module.SetupVoid("disposeHeroTracking", _ => true);
        dispose.SetVoidResult();

        var cut = context.Render<LoginHero>();

        await cut.Instance.DisposeAsync();

        dispose.VerifyInvoke("disposeHeroTracking");
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }
}
