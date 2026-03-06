using System.Windows;

namespace GlobalEventManager
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 捕获当前应用的 Dispatcher 和 SynchronizationContext
            GlobalEventBus.Configure(Application.Current.Dispatcher);

            // 可选：异常处理统一日志
            GlobalEventBus.HandlerException = (ex, type, method) =>
            {
                System.Diagnostics.Debug.WriteLine($"[EventBus] {type.Name}.{method.Name} threw: {ex.Message}");
            };
        }
    }
}