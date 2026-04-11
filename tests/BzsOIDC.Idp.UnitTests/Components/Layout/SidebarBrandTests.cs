using Bunit;
using BzsOIDC.Idp.Client.Components.Layout;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System;

namespace BzsOIDC.Idp.UnitTests.Components.Layout;

public sealed class SidebarBrandTests
{
    [Fact]
    public void SidebarBrand_RendersBrandLink()
    {
        using var context = new BunitContext();

        var clicked = false;

        var cut = context.Render<SidebarBrand>(parameters => parameters
            .Add(x => x.Href, string.Empty)
            .Add(x => x.Mark, "B")
            .Add(x => x.Text, "BzsOIDC")
            .Add(x => x.OnClick, EventCallback.Factory.Create<MouseEventArgs>(new object(), _ => clicked = true)));

        cut.Find("a.sidebar-brand").Click();

        Assert.True(clicked);
        Assert.Contains("BzsOIDC", cut.Markup, StringComparison.Ordinal);
    }
}
