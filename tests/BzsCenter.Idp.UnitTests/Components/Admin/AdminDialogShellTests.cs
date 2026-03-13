using Bunit;
using BzsCenter.Idp.Components.Admin;
using Microsoft.AspNetCore.Components;

namespace BzsCenter.Idp.UnitTests.Components.Admin;

public sealed class AdminDialogShellTests
{
    [Fact]
    public void Render_WhenClosed_DoesNotRenderDialog()
    {
        using var context = CreateContext();
        var module = context.JSInterop.SetupModule("./Components/Admin/AdminDialogShell.razor.js");
        module.SetupVoid("activate", _ => true);

        var cut = context.Render<AdminDialogShell>(parameters => parameters
            .Add(x => x.IsOpen, false)
            .Add(x => x.Title, "Title")
            .Add(x => x.Lead, "Lead"));

        Assert.Empty(cut.FindAll("[role='dialog']"));
        Assert.Empty(module.Invocations);
    }

    [Fact]
    public void Render_WhenOpen_ImportsModuleAndActivatesShell()
    {
        using var context = CreateContext();
        var module = context.JSInterop.SetupModule("./Components/Admin/AdminDialogShell.razor.js");
        var activate = module.SetupVoid("activate", _ => true);

        var cut = context.Render<AdminDialogShell>(parameters => parameters
            .Add(x => x.IsOpen, true)
            .Add(x => x.Title, "Title")
            .Add(x => x.Lead, "Lead")
            .Add(x => x.InitialFocusSelector, "#editor-user-name"));

        var invocation = Assert.Single(module.Invocations);
        Assert.Equal("activate", invocation.Identifier);
        activate.VerifyInvoke("activate");
        Assert.NotNull(cut.Find(".admin-dialog-shell"));
    }

    [Fact]
    public void BackdropClick_DoesNotInvokeOnClose()
    {
        using var context = CreateContext();
        var module = context.JSInterop.SetupModule("./Components/Admin/AdminDialogShell.razor.js");
        module.SetupVoid("activate", _ => true);
        var closeCount = 0;

        var cut = context.Render<AdminDialogShell>(parameters => parameters
            .Add(x => x.IsOpen, true)
            .Add(x => x.Title, "Title")
            .Add(x => x.Lead, "Lead")
            .Add(x => x.OnClose, EventCallback.Factory.Create(new object(), () => closeCount++)));

        Assert.Throws<MissingEventHandlerException>(() => cut.Find(".admin-dialog-backdrop").Click());

        Assert.Equal(0, closeCount);
    }

    [Fact]
    public async Task CloseFromJs_WhenBusy_DoesNotInvokeOnClose()
    {
        using var context = CreateContext();
        var module = context.JSInterop.SetupModule("./Components/Admin/AdminDialogShell.razor.js");
        module.SetupVoid("activate", _ => true);
        var closeCount = 0;

        var cut = context.Render<AdminDialogShell>(parameters => parameters
            .Add(x => x.IsOpen, true)
            .Add(x => x.IsBusy, true)
            .Add(x => x.Title, "Title")
            .Add(x => x.Lead, "Lead")
            .Add(x => x.OnClose, EventCallback.Factory.Create(new object(), () => closeCount++)));

        await cut.Instance.CloseFromJs();

        Assert.Equal(0, closeCount);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }
}
