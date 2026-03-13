using Bunit;
using BzsCenter.Idp.Components.Admin;
using BzsCenter.Idp.UnitTests.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace BzsCenter.Idp.UnitTests.Components.Admin;

public sealed class UserManagementDialogTests
{
    [Fact]
    public void Render_WhenEditing_ShowsEditLabelsAndErrors()
    {
        using var context = CreateContext();
        var cut = context.Render<UserManagementDialog>(parameters => parameters
            .Add(x => x.IsOpen, true)
            .Add(x => x.IsEditing, true)
            .Add(x => x.FormErrors, new[] { "duplicate user" }));

        Assert.Contains("EditUser", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("SaveChanges", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("duplicate user", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void AdminToggle_WhenChanged_InvokesIsAdminChanged()
    {
        using var context = CreateContext();
        var changed = false;

        var cut = context.Render<UserManagementDialog>(parameters => parameters
            .Add(x => x.IsOpen, true)
            .Add(x => x.IsAdmin, false)
            .Add(x => x.IsAdminChanged, EventCallback.Factory.Create<bool>(new object(), value => changed = value)));

        cut.Find("#editor-admin-role").Change(true);

        Assert.True(changed);
    }

    [Fact]
    public void ResetAndSaveButtons_WhenClicked_InvokeCallbacks()
    {
        using var context = CreateContext();
        var resetCount = 0;
        var saveCount = 0;

        var cut = context.Render<UserManagementDialog>(parameters => parameters
            .Add(x => x.IsOpen, true)
            .Add(x => x.OnReset, EventCallback.Factory.Create(new object(), () => resetCount++))
            .Add(x => x.OnSave, EventCallback.Factory.Create(new object(), () => saveCount++)));

        var buttons = cut.FindAll("button");
        buttons.Single(button => button.TextContent.Contains("Reset", StringComparison.Ordinal)).Click();
        buttons.Single(button => button.TextContent.Contains("CreateUser", StringComparison.Ordinal)).Click();

        Assert.Equal(1, resetCount);
        Assert.Equal(1, saveCount);
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddSingleton<IStringLocalizer<UserManagement>, TestStringLocalizer<UserManagement>>();
        return context;
    }
}
