using Microsoft.JSInterop;

namespace BzsCenter.Idp.Client.Infra.Blazor;

public static class BlazorUtils
{
    public static async ValueTask DisposeJsModuleAsync(Func<ValueTask> action)
    {
        try
        {
            await action();
        }
        catch (ObjectDisposedException)
        {
            // Component was disposed, safe to ignore.
        }
        catch (JSDisconnectedException)
        {
            // JS runtime disconnected, safe to ignore.
        }
    }
}