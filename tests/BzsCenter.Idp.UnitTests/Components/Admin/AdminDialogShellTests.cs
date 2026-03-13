using Bunit;
using BzsCenter.Idp.Components.Admin;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace BzsCenter.Idp.UnitTests.Components.Admin;

public sealed class AdminDialogShellTests
{
    [Fact]
    public void Render_WhenClosed_DoesNotRenderDialog()
    {
        using var context = CreateContext();
        var cut = context.Render<AdminDialogShell>(parameters => parameters
            .Add(x => x.IsOpen, false)
            .Add(x => x.Title, "Title")
            .Add(x => x.Lead, "Lead"));

        Assert.Empty(cut.FindAll("[role='dialog']"));
    }

    [Fact]
    public void BackdropClick_WhenNotBusy_InvokesOnClose()
    {
        using var context = CreateContext();
        var closeCount = 0;

        var cut = context.Render<AdminDialogShell>(parameters => parameters
            .Add(x => x.IsOpen, true)
            .Add(x => x.Title, "Title")
            .Add(x => x.Lead, "Lead")
            .Add(x => x.OnClose, EventCallback.Factory.Create(new object(), () => closeCount++)));

        cut.Find(".admin-dialog-backdrop").Click();

        Assert.Equal(1, closeCount);
    }

    [Fact]
    public void Escape_WhenBusy_DoesNotInvokeOnClose()
    {
        using var context = CreateContext();
        var closeCount = 0;

        var cut = context.Render<AdminDialogShell>(parameters => parameters
            .Add(x => x.IsOpen, true)
            .Add(x => x.IsBusy, true)
            .Add(x => x.Title, "Title")
            .Add(x => x.Lead, "Lead")
            .Add(x => x.OnClose, EventCallback.Factory.Create(new object(), () => closeCount++)));

        cut.Find(".admin-dialog-shell").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Equal(0, closeCount);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }
}
