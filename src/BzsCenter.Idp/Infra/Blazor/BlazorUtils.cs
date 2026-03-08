using Microsoft.JSInterop;

namespace BzsCenter.Idp.Infra.Blazor;

internal static class BlazorUtils
{
    internal static void DisposeJsModule(Action action)
    {
        try
        {
            action();
        }
        catch (ObjectDisposedException)
        {
            // 组件已被销毁，忽略异常。
        }
        catch (JSDisconnectedException)
        {
            // JS 运行时已断开连接，忽略异常。
        }
    }

    internal static async ValueTask DisposeJsModuleAsync(Func<ValueTask> action)
    {
        try
        {
            await action();
        }
        catch (ObjectDisposedException)
        {
            // 组件已被销毁，忽略异常。
        }
        catch (JSDisconnectedException)
        {
            // JS 运行时已断开连接，忽略异常。
        }
    }
}
