# GlobalEventAggregator.cs

```CSharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace YourNamespace.Messaging
{
    /// <summary>
    /// 生产级的全局事件聚合器（Event Aggregator / Global Event Manager）
    /// 支持：线程安全、弱引用、UI Dispatcher 调度、同步/异步发布、订阅令牌(IDisposable)
    /// </summary>
    public sealed class GlobalEventAggregator
    {
        private static readonly Lazy<GlobalEventAggregator> _instance 
            = new(() => new GlobalEventAggregator());
        public static GlobalEventAggregator Instance => _instance.Value;

        // 每个事件类型对应多个订阅
        private readonly ConcurrentDictionary<Type, List<Subscription>> _subscriptions
            = new();

        // 锁字典：为每个事件类型单独加锁，避免全局锁
        private readonly ConcurrentDictionary<Type, object> _locks = new();

        // 默认 UI Dispatcher（如果在非 WPF 程序里，可在构造时/初始化时注入）
        private readonly Dispatcher _uiDispatcher;

        private GlobalEventAggregator()
        {
            _uiDispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        #region Subscribe / Unsubscribe

        /// <summary>
        /// 订阅事件
        /// </summary>
        /// <typeparam name="T">事件类型</typeparam>
        /// <param name="handler">处理器</param>
        /// <param name="runOnUIThread">是否将处理器调度到 UI 线程执行</param>
        /// <param name="keepTargetAlive">是否强引用目标（默认 false -> 使用弱引用）</param>
        /// <returns>订阅令牌，Dispose 会取消订阅</returns>
        public IDisposable Subscribe<T>(Action<T> handler, bool runOnUIThread = false, bool keepTargetAlive = false)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var type = typeof(T);
            var subscription = new Subscription(handler, runOnUIThread, keepTargetAlive);

            var list = _subscriptions.GetOrAdd(type, _ => new List<Subscription>());
            var lockObj = _locks.GetOrAdd(type, _ => new object());

            lock (lockObj)
            {
                // 防止重复订阅相同 handler
                if (!list.Any(s => s.Matches(handler)))
                {
                    list.Add(subscription);
                }
            }

            return new SubscriptionToken(() => UnsubscribeInternal(type, subscription));
        }

        /// <summary>
        /// 取消订阅（外部可以直接调用）
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (!_subscriptions.TryGetValue(type, out var list)) return;
            var lockObj = _locks.GetOrAdd(type, _ => new object());
            lock (lockObj)
            {
                list.RemoveAll(s => s.Matches(handler) || !s.IsAlive);
                if (list.Count == 0)
                {
                    _subscriptions.TryRemove(type, out _);
                    _locks.TryRemove(type, out _);
                }
            }
        }

        private void UnsubscribeInternal(Type eventType, Subscription subscription)
        {
            if (!_subscriptions.TryGetValue(eventType, out var list)) return;
            var lockObj = _locks.GetOrAdd(eventType, _ => new object());
            lock (lockObj)
            {
                list.RemoveAll(s => s == subscription || !s.IsAlive);
                if (list.Count == 0)
                {
                    _subscriptions.TryRemove(eventType, out _);
                    _locks.TryRemove(eventType, out _);
                }
            }
        }

        #endregion

        #region Publish (同步/异步)

        /// <summary>
        /// 同步发布事件（会在当前线程执行订阅者，若订阅者标记 runOnUIThread：会通过 UI Dispatcher 同步/异步调度）
        /// 注意：若订阅者长时间运行会阻塞发布者线程
        /// </summary>
        public void Publish<T>(T message)
        {
            PublishInternal(message, runAsyncHandlers: false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步发布事件（并行调用订阅者，等待所有订阅者完成）
        /// </summary>
        public Task PublishAsync<T>(T message)
        {
            return PublishInternal(message, runAsyncHandlers: true);
        }

        private async Task PublishInternal<T>(T message, bool runAsyncHandlers)
        {
            var type = typeof(T);
            if (!_subscriptions.TryGetValue(type, out var list) || list.Count == 0) return;

            // 拷贝到数组以避免枚举时被修改，同时过滤死掉的订阅
            Subscription[] snapshot;
            var lockObj = _locks.GetOrAdd(type, _ => new object());
            lock (lockObj)
            {
                // 清理 dead
                list.RemoveAll(s => !s.IsAlive);
                snapshot = list.ToArray();
                if (list.Count == 0)
                {
                    _subscriptions.TryRemove(type, out _);
                    _locks.TryRemove(type, out _);
                }
            }

            var tasks = new List<Task>(snapshot.Length);

            foreach (var sub in snapshot)
            {
                if (!sub.IsAlive) continue;

                if (sub.RunOnUIThread)
                {
                    // 将处理器调度到 UI 线程
                    var tcs = new TaskCompletionSource<object?>();
                    void ActionOnUi()
                    {
                        try
                        {
                            sub.Invoke((object)message);
                            tcs.SetResult(null);
                        }
                        catch (Exception ex)
                        {
                            tcs.SetException(ex);
                        }
                    }

                    if (_uiDispatcher.CheckAccess())
                    {
                        // 已在 UI 线程
                        if (runAsyncHandlers)
                            tasks.Add(Task.Run(() => sub.Invoke((object)message)));
                        else
                            sub.Invoke((object)message);
                    }
                    else
                    {
                        // BeginInvoke ensures non-blocking post; use Invoke for blocking if needed
                        _uiDispatcher.BeginInvoke((Action)ActionOnUi, DispatcherPriority.Normal);
                        // 如果发布者需要等待 UI 处理完成时，改为 _uiDispatcher.Invoke and wrap accordingly.
                        // 这里我们把 BeginInvoke 的完成包装到 tasks，用 tcs.Task 代表完成状态
                        tasks.Add(tcs.Task);
                    }
                }
                else
                {
                    if (runAsyncHandlers)
                    {
                        // 并行执行在线程池
                        tasks.Add(Task.Run(() => sub.Invoke((object)message)));
                    }
                    else
                    {
                        // 在当前线程直接调用
                        sub.Invoke((object)message);
                    }
                }
            }

            if (runAsyncHandlers && tasks.Count > 0)
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }

        #endregion

        #region Subscription 内部类型

        /// <summary>
        /// 订阅包装（弱引用或强引用）
        /// </summary>
        private sealed class Subscription
        {
            private readonly WeakReference? _weakTarget;   // 如果是实例方法且 keepTargetAlive=false，则使用弱引用
            private readonly object? _strongTarget;        // 当 keepTargetAlive=true 时的强引用
            private readonly MethodInfo _method;
            public bool RunOnUIThread { get; }
            public bool KeepTargetAlive { get; }
            private readonly bool _isStatic;

            public Subscription(Delegate handler, bool runOnUIThread, bool keepTargetAlive)
            {
                if (handler == null) throw new ArgumentNullException(nameof(handler));
                _method = handler.Method;
                _isStatic = handler.Target == null;
                RunOnUIThread = runOnUIThread;
                KeepTargetAlive = keepTargetAlive;

                if (_isStatic)
                {
                    _weakTarget = null;
                    _strongTarget = null;
                }
                else
                {
                    if (keepTargetAlive)
                    {
                        _strongTarget = handler.Target;
                        _weakTarget = null;
                    }
                    else
                    {
                        _weakTarget = new WeakReference(handler.Target);
                        _strongTarget = null;
                    }
                }
            }

            public bool IsAlive
            {
                get
                {
                    if (_isStatic) return true;
                    if (KeepTargetAlive) return _strongTarget != null;
                    return _weakTarget != null && _weakTarget.IsAlive;
                }
            }

            /// <summary>
            /// 调用（注意：入参为 object message，调用时需匹配类型）
            /// </summary>
            public void Invoke(object message)
            {
                // 获取 target
                object? target = null;
                if (!_isStatic)
                {
                    if (KeepTargetAlive)
                        target = _strongTarget;
                    else
                        target = _weakTarget?.Target;
                    if (target == null) return; // 已回收
                }

                // 调用方法（尽量少做装箱/反射，MethodInfo.Invoke 对性能有影响，但此实现可兼顾通用性）
                // 对于性能敏感的场景可以将 Delegate 存为 open delegate via DynamicMethod 或 Expression.Compile（可扩展）
                _method.Invoke(target, new[] { message });
            }

            /// <summary>
            /// 判断是否与给定 delegate 匹配（用于去重/取消订阅）
            /// </summary>
            public bool Matches(Delegate handler)
            {
                if (handler == null) return false;
                if (handler.Method != _method) return false;

                if (_isStatic && handler.Target == null) return true;

                // 比较 target 引用
                if (handler.Target == null) return false;

                if (KeepTargetAlive)
                    return ReferenceEquals(handler.Target, _strongTarget);

                return _weakTarget != null && ReferenceEquals(handler.Target, _weakTarget.Target);
            }
        }

        /// <summary>
        /// 订阅令牌：Dispose 删除订阅
        /// </summary>
        private sealed class SubscriptionToken : IDisposable
        {
            private Action? _disposeAction;
            private int _disposed; // 0 = no, 1 = yes

            public SubscriptionToken(Action dispose)
            {
                _disposeAction = dispose ?? throw new ArgumentNullException(nameof(dispose));
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    try { _disposeAction?.Invoke(); }
                    finally { _disposeAction = null; }
                }
            }
        }

        #endregion
    }
}

```

---

# 使用示例（WPF + MVVM）

## 1) 定义事件类（建议使用简单 POCO）

```C#
public class LoginSuccessEvent
{
    public string UserName { get; set; } = "";
}

```

## 2) 在 ViewModel 中订阅（并保存令牌用于取消）

```C#
public class MainViewModel : IDisposable
{
    private readonly IDisposable _loginToken;

    public string Status { get; set; }

    public MainViewModel()
    {
        // 订阅：在 UI 线程处理（更新 UI 绑定属性），并使用弱引用（默认）
        _loginToken = GlobalEventAggregator.Instance.Subscribe<LoginSuccessEvent>(OnLoginSuccess, runOnUIThread: true);
    }

    private void OnLoginSuccess(LoginSuccessEvent e)
    {
        // 这里在 UI 线程执行（因为 runOnUIThread: true），可以安全更新绑定属性
        Status = $"欢迎 {e.UserName}";
        // RaisePropertyChanged(nameof(Status)) -- 依据你所用的 MVVM 框架
    }

    public void Dispose()
    {
        _loginToken.Dispose(); // 显式取消订阅
    }
}

```

## 3) 发布事件（例如登录成功后）

```C#
// 在登录流程中（可能在后台线程）：
await GlobalEventAggregator.Instance.PublishAsync(new LoginSuccessEvent { UserName = "Alice" });

// 或者同步发布（注意同步发布会在当前线程执行订阅处理）
GlobalEventAggregator.Instance.Publish(new LoginSuccessEvent { UserName = "Bob" });

```
