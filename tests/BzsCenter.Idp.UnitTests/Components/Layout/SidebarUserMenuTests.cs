using Bunit;
using BzsCenter.Idp.Client.Components.Layout;
using Microsoft.AspNetCore.Components;
using System;

namespace BzsCenter.Idp.UnitTests.Components.Layout;

public sealed class SidebarUserMenuTests
{
    [Fact]
    public void SidebarUserMenu_WhenInteractive_RendersFullWidthUserRowContract()
    {
        using var context = new BunitContext();

        var clicked = false;

        var cut = context.Render<SidebarUserMenu>(parameters => parameters
            .Add(x => x.Avatar, "A")
            .Add(x => x.Name, "admin")
            .Add(x => x.SecondaryText, "Signed in")
            .Add(x => x.AvatarButtonAriaLabel, "Open user menu")
            .Add(x => x.AvatarControlsId, "user-panel")
            .Add(x => x.AvatarExpanded, false)
            .Add(x => x.OnAvatarClick, EventCallback.Factory.Create(new object(), () => clicked = true)));

        cut.Find("button.sidebar-avatar").Click();

        Assert.True(clicked);
        Assert.Contains("sidebar-user-menu__trigger", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("sidebar-nav-item", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("sidebar-user-menu__body", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("aria-expanded=\"false\"", cut.Markup, StringComparison.Ordinal);
    }
}
