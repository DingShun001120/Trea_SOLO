using System;
using System.Threading.Tasks;
using System.Windows;

namespace GlobalEventManager
{
    // 定义事件类型
    public sealed class LoginSuccessEvent
    {
        public string UserName { get; set; } = string.Empty;
    }

    public sealed class HeavyWorkEvent
    {
        public string Payload { get; set; } = string.Empty;
    }

    // 应用启动时配置（示例）
    public static class AppStartup
    {
        public static void Initialize()
        {
            // 捕获 UI Dispatcher 与当前 SynchronizationContext
            GlobalEventBus.Configure();
            // 可选：设置异常处理回调
            GlobalEventBus.HandlerException = (ex, type, method) =>
            {
                // 生产环境可替换为日志框架
                System.Diagnostics.Debug.WriteLine($"Event handler error for {type.Name}.{method.Name}: {ex.Message}");
            };
        }
    }

    // ViewModel 示例：在 UI 线程订阅并更新界面
    public sealed class MainWindowViewModel : IDisposable
    {
        private readonly SubscriptionToken _loginToken;
        private readonly SubscriptionToken _heavyToken;

        public string StatusText { get; private set; } = string.Empty;

        public MainWindowViewModel()
        {
            // 登录成功事件：在 UI 线程执行（用于更新界面）
            _loginToken = GlobalEventBus.Subscribe<LoginSuccessEvent>(OnLoginSuccess, DeliveryThread.UI);

            // 重活事件：在后台线程执行（避免阻塞UI）
            _heavyToken = GlobalEventBus.Subscribe<HeavyWorkEvent>(OnHeavyWork, DeliveryThread.Background);
        }

        private void OnLoginSuccess(LoginSuccessEvent e)
        {
            StatusText = $"欢迎 {e.UserName}";
            // 如果实现了 INotifyPropertyChanged，请在此触发通知
        }

        private void OnHeavyWork(HeavyWorkEvent e)
        {
            // 在后台线程处理耗时任务
            System.Threading.Thread.Sleep(500);
            // 如需回到UI线程，可发布另一个事件或使用 Dispatcher
        }

        public void Dispose()
        {
            // 取消订阅（或依赖弱引用在GC后由管理器清理）
            _loginToken.Dispose();
            _heavyToken.Dispose();
        }
    }

    // 发布事件示例（如在服务层或命令中）
    public static class AuthService
    {
        public static async Task LoginAsync(string userName)
        {
            // 模拟登录
            await Task.Delay(100);

            // 登录成功后发布事件（同步发布）
            GlobalEventBus.Publish(new LoginSuccessEvent { UserName = userName });

            // 或使用异步发布（不阻塞调用方）
            // await GlobalEventBus.PublishAsync(new LoginSuccessEvent { UserName = userName });
        }
    }
}