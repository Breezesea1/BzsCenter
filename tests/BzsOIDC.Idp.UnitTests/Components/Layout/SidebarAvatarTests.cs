using Bunit;
using BzsOIDC.Idp.Client.Components.Layout;
using Microsoft.AspNetCore.Components;
using System;

namespace BzsOIDC.Idp.UnitTests.Components.Layout;

public sealed class SidebarAvatarTests
{
    [Fact]
    public void SidebarAvatar_WhenInteractive_RendersButtonWrapper()
    {
        using var context = new BunitContext();

        var clicked = false;

        var cut = context.Render<SidebarAvatar>(parameters => parameters
            .Add(x => x.Value, "A")
            .Add(x => x.AriaLabel, "Open user menu")
            .Add(x => x.ControlsId, "user-panel")
            .Add(x => x.IsExpanded, true)
            .Add(x => x.OnClick, EventCallback.Factory.Create(new object(), () => clicked = true)));

        cut.Find("button.sidebar-avatar").Click();

        Assert.True(clicked);
        Assert.Contains("aria-controls=\"user-panel\"", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("sidebar-avatar is-open", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void SidebarAvatar_WhenStatic_RendersSpanWrapper()
    {
        using var context = new BunitContext();

        var cut = context.Render<SidebarAvatar>(parameters => parameters
            .Add(x => x.Value, "G"));

        Assert.Contains("<span class=\"sidebar-avatar\"", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<button", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("sidebar-avatar__surface", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void SidebarAvatar_WhenClosed_DoesNotRenderOpenStateClass()
    {
        using var context = new BunitContext();

        var cut = context.Render<SidebarAvatar>(parameters => parameters
            .Add(x => x.Value, "A")
            .Add(x => x.AriaLabel, "Open user menu")
            .Add(x => x.ControlsId, "user-panel")
            .Add(x => x.IsExpanded, false)
            .Add(x => x.OnClick, EventCallback.Factory.Create(new object(), () => { })));

        Assert.Contains("aria-expanded=\"false\"", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("sidebar-avatar is-open", cut.Markup, StringComparison.Ordinal);
    }
}
