using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace GlobalEventManager.ViewModels
{
    public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SubscriptionToken _loginToken;
        private readonly SubscriptionToken _heavyToken;
        private readonly SubscriptionToken _priorityA;
        private readonly SubscriptionToken _priorityB;

        private string _userName = "Alice";
        private string _statusText = "就绪";
        private bool _filterEnabled = true;

        public ObservableCollection<string> Logs { get; } = new();

        public string UserName
        {
            get => _userName;
            set { _userName = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public bool FilterEnabled
        {
            get => _filterEnabled;
            set { _filterEnabled = value; OnPropertyChanged(); }
        }

        public ICommand PublishLoginUiCommand { get; }
        public ICommand PublishHeavyBackgroundCommand { get; }
        public ICommand PublishPriorityDemoCommand { get; }
        public ICommand ClearLogsCommand { get; }

        public MainWindowViewModel()
        {
            PublishLoginUiCommand = new RelayCommand(() =>
            {
                GlobalEventBus.Publish(new LoginSuccessEvent { UserName = UserName });
                Logs.Add($"[{Now()}] 发布 LoginSuccessEvent: {UserName}");
            });

            PublishHeavyBackgroundCommand = new RelayCommand(async () =>
            {
                await GlobalEventBus.PublishAsync(new HeavyWorkEvent { Payload = $"Job-{Guid.NewGuid():N}" });
                Logs.Add($"[{Now()}] 异步发布 HeavyWorkEvent");
            });

            PublishPriorityDemoCommand = new RelayCommand(() =>
            {
                GlobalEventBus.Publish(new LoginSuccessEvent { UserName = UserName });
                Logs.Add($"[{Now()}] 发布 LoginSuccessEvent（用于优先级演示）");
            });

            ClearLogsCommand = new RelayCommand(() => Logs.Clear());

            // 登录事件订阅：在 UI 线程执行，带过滤器与优先级
            _loginToken = GlobalEventBus.Subscribe<LoginSuccessEvent>(OnLoginSuccess,
                priority: 5,
                filter: (e) => !FilterEnabled || (!string.IsNullOrEmpty(e.UserName) && e.UserName.StartsWith("A", StringComparison.OrdinalIgnoreCase)),
                delivery: DeliveryThread.UI);

            // 重活事件订阅：在后台线程执行
            _heavyToken = GlobalEventBus.Subscribe<HeavyWorkEvent>(OnHeavyWork, priority: 0, filter: null, delivery: DeliveryThread.Background);

            // 额外订阅者用于优先级演示：两个订阅分别不同优先级
            _priorityA = GlobalEventBus.Subscribe<LoginSuccessEvent>(e =>
            {
                Logs.Add($"[{Now()}] 优先级10处理：欢迎 {e.UserName}");
            }, priority: 10, filter: null, delivery: DeliveryThread.UI);

            _priorityB = GlobalEventBus.Subscribe<LoginSuccessEvent>(e =>
            {
                Logs.Add($"[{Now()}] 优先级0处理：日志记录 {e.UserName}");
            }, priority: 0, filter: null, delivery: DeliveryThread.UI);
        }

        private void OnLoginSuccess(LoginSuccessEvent e)
        {
            StatusText = $"欢迎 {e.UserName}";
            Logs.Add($"[{Now()}] UI线程更新状态为: {StatusText}");
        }

        private void OnHeavyWork(HeavyWorkEvent e)
        {
            Task.Delay(300).Wait();
            Logs.Add($"[{Now()}] 后台处理重活：{e.Payload}");
        }

        public void Dispose()
        {
            _loginToken.Dispose();
            _heavyToken.Dispose();
            _priorityA.Dispose();
            _priorityB.Dispose();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        private static string Now() => DateTime.Now.ToString("HH:mm:ss.fff");
    }
}