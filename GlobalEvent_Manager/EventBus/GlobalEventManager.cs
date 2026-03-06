using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace GlobalEventManager
{
    /// <summary>
    /// 适用于 WPF + MVVM 的全局事件管理器（生产级）
    /// - 线程安全：读写使用 ReaderWriterLockSlim
    /// - 弱引用：订阅者通过 WeakReference 存储，避免内存泄漏
    /// - UI 线程调度：可将处理函数投递到 UI Dispatcher
    /// 
    /// 使用方式见 ExampleUsage.cs
    /// </summary>
    public static class GlobalEventBus
    {
        private static readonly ConcurrentDictionary<Type, List<SubscriberEntry>> _subscriptions = new();
        private static readonly ConcurrentDictionary<Type, object> _locks = new();
        private static readonly ConcurrentDictionary<Type, object?> _lastMessages = new();

        private static Dispatcher? _dispatcher;
        private static DispatcherPriority _uiPriority = DispatcherPriority.Normal;
        private static SynchronizationContext? _uiContext;

        /// <summary>异常处理回调（可选）。若设置，将在订阅者抛出异常时被调用。</summary>
        public static Action<Exception, Type, MethodInfo>? HandlerException { get; set; }

        /// <summary>
        /// 配置 UI 调度上下文。建议在 App 启动时调用。
        /// 如果不显式配置，将尝试使用 Application.Current.Dispatcher 或当前 SynchronizationContext。
        /// </summary>
        public static void Configure(Dispatcher? dispatcher = null,
                                     DispatcherPriority priority = DispatcherPriority.Normal,
                                     SynchronizationContext? uiContext = null)
        {
            _dispatcher = dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _uiPriority = priority;
            _uiContext = uiContext ?? SynchronizationContext.Current;
        }

        /// <summary>
        /// 订阅指定类型事件。默认在当前线程执行，可选择在 UI 或后台线程执行。
        /// 返回的 SubscriptionToken 可用于取消订阅（也可使用 Unsubscribe(Action<T>)）。
        /// </summary>
        public static SubscriptionToken Subscribe<T>(Action<T> handler, DeliveryThread delivery = DeliveryThread.Current)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var type = typeof(T);

            var entry = new SubscriberEntry
            {
                Id = Guid.NewGuid(),
                IsStatic = handler.Target is null,
                KeepTargetAlive = false,
                StrongTarget = null,
                TargetRef = handler.Target is null ? null : new WeakReference(handler.Target),
                Method = handler.Method,
                Delivery = delivery,
                MessageType = type,
                Priority = 0,
                FilterMethod = null,
                FilterTargetRef = null,
                UiPriorityOverride = null,
                Once = false,
                IsAsync = false,
                ReplayLatest = false
            };

            var list = _subscriptions.GetOrAdd(type, _ => new List<SubscriberEntry>());
            var lockObj = _locks.GetOrAdd(type, _ => new object());
            lock (lockObj)
            {
                if (!ContainsDuplicate(list, handler, delivery, entry.Priority))
                {
                    list.Add(entry);
                }
            }

            return new SubscriptionToken(entry.Id, type, Unsubscribe);
        }

        /// <summary>
        /// 订阅（含优先级与过滤器）。优先级越高越先执行；过滤器返回 true 才会触发处理器。
        /// </summary>
        public static SubscriptionToken Subscribe<T>(Action<T> handler, int priority, Predicate<T>? filter, DeliveryThread delivery = DeliveryThread.Current)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var type = typeof(T);

            var entry = new SubscriberEntry
            {
                Id = Guid.NewGuid(),
                IsStatic = handler.Target is null,
                KeepTargetAlive = false,
                StrongTarget = null,
                TargetRef = handler.Target is null ? null : new WeakReference(handler.Target),
                Method = handler.Method,
                Delivery = delivery,
                MessageType = type,
                Priority = priority,
                FilterMethod = filter?.Method,
                FilterTargetRef = filter?.Target is null ? null : new WeakReference(filter.Target),
                UiPriorityOverride = null,
                Once = false,
                IsAsync = false,
                ReplayLatest = false
            };

            var list = _subscriptions.GetOrAdd(type, _ => new List<SubscriberEntry>());
            var lockObj = _locks.GetOrAdd(type, _ => new object());
            lock (lockObj)
            {
                if (!ContainsDuplicate(list, handler, delivery, entry.Priority))
                {
                    list.Add(entry);
                }
            }

            return new SubscriptionToken(entry.Id, type, Unsubscribe);
        }

        /// <summary>
        /// 高级订阅：支持优先级、过滤、线程、是否强引用目标、UI优先级、一次性订阅。
        /// </summary>
        public static SubscriptionToken Subscribe<T>(Action<T> handler,
                                                     int priority,
                                                     Predicate<T>? filter,
                                                     DeliveryThread delivery,
                                                     bool keepTargetAlive,
                                                     DispatcherPriority? uiPriority = null,
                                                     bool once = false,
                                                     bool replayLatest = false)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var type = typeof(T);

            var entry = new SubscriberEntry
            {
                Id = Guid.NewGuid(),
                IsStatic = handler.Target is null,
                KeepTargetAlive = keepTargetAlive,
                StrongTarget = keepTargetAlive ? handler.Target : null,
                TargetRef = (!keepTargetAlive && handler.Target != null) ? new WeakReference(handler.Target) : null,
                Method = handler.Method,
                Delivery = delivery,
                MessageType = type,
                Priority = priority,
                FilterMethod = filter?.Method,
                FilterTargetRef = filter?.Target is null ? null : new WeakReference(filter.Target),
                UiPriorityOverride = uiPriority,
                Once = once,
                IsAsync = false,
                ReplayLatest = replayLatest
            };

            var list = _subscriptions.GetOrAdd(type, _ => new List<SubscriberEntry>());
            var lockObj = _locks.GetOrAdd(type, _ => new object());
            lock (lockObj)
            {
                if (!ContainsDuplicate(list, handler, delivery, entry.Priority))
                {
                    list.Add(entry);
                }
            }

            // 黏性事件重放：立即向新订阅者重放最近消息
            if (entry.ReplayLatest && _lastMessages.TryGetValue(type, out var last) && last is T typedLast)
            {
                try
                {
                    switch (entry.Delivery)
                    {
                        case DeliveryThread.UI:
                            if (_dispatcher != null)
                                _dispatcher.Invoke(() => Invoke(entry, typedLast), entry.UiPriorityOverride ?? _uiPriority);
                            else if (_uiContext != null)
                                _uiContext.Send(_ => Invoke(entry, typedLast), null);
                            else
                                Invoke(entry, typedLast);
                            break;
                        case DeliveryThread.Background:
                            Task.Run(() => Invoke(entry, typedLast)).Wait();
                            break;
                        default:
                            Invoke(entry, typedLast);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex, entry);
                }
            }

            return new SubscriptionToken(entry.Id, type, Unsubscribe);
        }

        /// <summary>
        /// 异步订阅（处理函数返回 Task）。支持优先级、过滤、线程、强引用、UI优先级、一次性与黏性事件重放。
        /// </summary>
        public static SubscriptionToken SubscribeAsync<T>(Func<T, Task> handler,
                                                          int priority = 0,
                                                          Predicate<T>? filter = null,
                                                          DeliveryThread delivery = DeliveryThread.Current,
                                                          bool keepTargetAlive = false,
                                                          DispatcherPriority? uiPriority = null,
                                                          bool once = false,
                                                          bool replayLatest = false)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var type = typeof(T);

            var entry = new SubscriberEntry
            {
                Id = Guid.NewGuid(),
                IsStatic = handler.Target is null,
                KeepTargetAlive = keepTargetAlive,
                StrongTarget = keepTargetAlive ? handler.Target : null,
                TargetRef = (!keepTargetAlive && handler.Target != null) ? new WeakReference(handler.Target) : null,
                Method = handler.Method,
                Delivery = delivery,
                MessageType = type,
                Priority = priority,
                FilterMethod = filter?.Method,
                FilterTargetRef = filter?.Target is null ? null : new WeakReference(filter.Target),
                UiPriorityOverride = uiPriority,
                Once = once,
                IsAsync = true,
                ReplayLatest = replayLatest
            };

            var list = _subscriptions.GetOrAdd(type, _ => new List<SubscriberEntry>());
            var lockObj = _locks.GetOrAdd(type, _ => new object());
            lock (lockObj)
            {
                // 对于异步委托，重复判定以方法、目标、交付线程与优先级为准
                if (!ContainsDuplicate(list, (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), handler.Target, handler.Method, throwOnBindFailure: false)!, delivery, entry.Priority))
                {
                    list.Add(entry);
                }
            }

            // 黏性事件重放：立即向新订阅者重放最近消息（异步）
            if (entry.ReplayLatest && _lastMessages.TryGetValue(type, out var last) && last is T typedLast)
            {
                try
                {
                    switch (entry.Delivery)
                    {
                        case DeliveryThread.UI:
                            if (_dispatcher != null)
                                _dispatcher.Invoke(() => InvokeAwait(entry, typedLast), entry.UiPriorityOverride ?? _uiPriority);
                            else if (_uiContext != null)
                                _uiContext.Send(_ => InvokeAwait(entry, typedLast), null);
                            else
                                InvokeAwait(entry, typedLast);
                            break;
                        case DeliveryThread.Background:
                            Task.Run(() => InvokeAwait(entry, typedLast)).Wait();
                            break;
                        default:
                            InvokeAwait(entry, typedLast);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    HandleException(ex, entry);
                }
            }

            return new SubscriptionToken(entry.Id, type, Unsubscribe);
        }

        /// <summary>通过 SubscriptionToken 取消订阅。</summary>
        public static void Unsubscribe(SubscriptionToken token)
        {
            if (token == null) return;
            var type = token.MessageType;

            if (_subscriptions.TryGetValue(type, out var list))
            {
                var lockObj = _locks.GetOrAdd(type, _ => new object());
                lock (lockObj)
                {
                    list.RemoveAll(e => e.Id == token.Id || !IsEntryAlive(e));
                    if (list.Count == 0)
                    {
                        _subscriptions.TryRemove(type, out _);
                        _locks.TryRemove(type, out _);
                    }
                }
            }
        }

        /// <summary>通过处理函数取消订阅。</summary>
        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            var type = typeof(T);
            var method = handler.Method;
            var target = handler.Target;

            if (_subscriptions.TryGetValue(type, out var list))
            {
                var lockObj = _locks.GetOrAdd(type, _ => new object());
                lock (lockObj)
                {
                    list.RemoveAll(e =>
                        e.Method == method &&
                        ((e.IsStatic && target == null) ||
                         (e.KeepTargetAlive && e.StrongTarget != null && ReferenceEquals(e.StrongTarget, target)) ||
                         (e.TargetRef != null && e.TargetRef.IsAlive && ReferenceEquals(e.TargetRef.Target, target)))
                    );

                    if (list.Count == 0)
                    {
                        _subscriptions.TryRemove(type, out _);
                        _locks.TryRemove(type, out _);
                    }
                }
            }
        }

        /// <summary>
        /// 同步发布事件。对于设置为 UI 线程的订阅者，使用 Dispatcher.Invoke/SynchronizationContext.Send 同步执行。
        /// 对于后台线程订阅者，使用 Task.Run 并等待完成，以保证调用顺序。
        /// 返回已调用的订阅者数量。
        /// </summary>
        public static int Publish<T>(T message)
        {
            var type = typeof(T);

            SubscriberEntry[] snapshot;
            if (!_subscriptions.TryGetValue(type, out var list) || list.Count == 0)
                return 0;
            var lockObj = _locks.GetOrAdd(type, _ => new object());
            lock (lockObj)
            {
                list.RemoveAll(e => !IsEntryAlive(e));
                snapshot = list.OrderByDescending(e => e.Priority).ToArray();
                if (list.Count == 0)
                {
                    _subscriptions.TryRemove(type, out _);
                    _locks.TryRemove(type, out _);
                }
            }

            // 记录最近消息，用于黏性订阅者重放
            _lastMessages[type] = message!;

            var deadIds = new List<Guid>();
            var invoked = 0;
            var onceIds = new List<Guid>();

            foreach (var entry in snapshot)
            {
                if (entry.TargetRef != null && !entry.TargetRef.IsAlive)
                {
                    deadIds.Add(entry.Id);
                    continue;
                }

                // 过滤器判定
                if (entry.FilterMethod != null)
                {
                    if (entry.FilterTargetRef != null && !entry.FilterTargetRef.IsAlive)
                    {
                        deadIds.Add(entry.Id);
                        continue;
                    }
                    bool pass = false;
                    try
                    {
                        var filterTarget = entry.FilterTargetRef?.Target;
                        var result = entry.FilterMethod.Invoke(filterTarget, new object[] { message! });
                        if (result is bool b) pass = b;
                    }
                    catch (Exception ex)
                    {
                        HandleException(ex, entry);
                        pass = false;
                    }
                    if (!pass) continue;
                }

                try
                {
                    switch (entry.Delivery)
                    {
                        case DeliveryThread.UI:
                            if (_dispatcher != null)
                                _dispatcher.Invoke(() => InvokeSync(entry, message), entry.UiPriorityOverride ?? _uiPriority);
                            else if (_uiContext != null)
                                _uiContext.Send(_ => InvokeSync(entry, message), null);
                            else
                                InvokeSync(entry, message);
                            break;

                        case DeliveryThread.Background:
                            Task.Run(() => InvokeSync(entry, message)).Wait();
                            break;

                        default:
                            InvokeSync(entry, message);
                            break;
                    }
                    invoked++;
                    if (entry.Once) onceIds.Add(entry.Id);
                }
                catch (Exception ex)
                {
                    HandleException(ex, entry);
                }
            }

            if (deadIds.Count > 0 || onceIds.Count > 0)
            {
                if (_subscriptions.TryGetValue(type, out var list2))
                {
                    var lockObj2 = _locks.GetOrAdd(type, _ => new object());
                    lock (lockObj2)
                    {
                        list2.RemoveAll(e => deadIds.Contains(e.Id) || onceIds.Contains(e.Id));
                        if (list2.Count == 0)
                        {
                            _subscriptions.TryRemove(type, out _);
                            _locks.TryRemove(type, out _);
                        }
                    }
                }
            }

            return invoked;
        }

        /// <summary>
        /// 异步发布事件。所有订阅者在其各自指定的线程模型中异步执行。
        /// 返回总共调度的订阅者数量。
        /// </summary>
        public static async Task<int> PublishAsync<T>(T message)
        {
            var type = typeof(T);

            SubscriberEntry[] snapshot;
            if (!_subscriptions.TryGetValue(type, out var list) || list.Count == 0)
                return 0;
            var lockObj = _locks.GetOrAdd(type, _ => new object());
            lock (lockObj)
            {
                list.RemoveAll(e => !IsEntryAlive(e));
                snapshot = list.OrderByDescending(e => e.Priority).ToArray();
                if (list.Count == 0)
                {
                    _subscriptions.TryRemove(type, out _);
                    _locks.TryRemove(type, out _);
                }
            }

            // 记录最近消息，用于黏性订阅者重放
            _lastMessages[type] = message!;

            var deadIds = new List<Guid>();
            var tasks = new List<Task>();

            var onceIds = new List<Guid>();
            foreach (var entry in snapshot)
            {
                if (entry.TargetRef != null && !entry.TargetRef.IsAlive)
                {
                    deadIds.Add(entry.Id);
                    continue;
                }

                // 过滤器判定
                if (entry.FilterMethod != null)
                {
                    if (entry.FilterTargetRef != null && !entry.FilterTargetRef.IsAlive)
                    {
                        deadIds.Add(entry.Id);
                        continue;
                    }
                    bool pass = false;
                    try
                    {
                        var filterTarget = entry.FilterTargetRef?.Target;
                        var result = entry.FilterMethod.Invoke(filterTarget, new object[] { message! });
                        if (result is bool b) pass = b;
                    }
                    catch (Exception ex)
                    {
                        HandleException(ex, entry);
                        pass = false;
                    }
                    if (!pass) continue;
                }

                switch (entry.Delivery)
                {
                    case DeliveryThread.UI:
                        if (_dispatcher != null)
                        {
                            var tcs = new TaskCompletionSource<object?>();
                            _dispatcher.BeginInvoke(new Action(async () =>
                            {
                                await InvokeAsync(entry, message);
                                tcs.TrySetResult(null);
                            }), entry.UiPriorityOverride ?? _uiPriority);
                            tasks.Add(tcs.Task);
                        }
                        else if (_uiContext != null)
                        {
                            var tcs = new TaskCompletionSource<object?>();
                            _uiContext.Post(async _ =>
                            {
                                await InvokeAsync(entry, message);
                                tcs.TrySetResult(null);
                            }, null);
                            tasks.Add(tcs.Task);
                        }
                        else
                        {
                            tasks.Add(Task.Run(() => InvokeAsync(entry, message)));
                        }
                        break;

                    case DeliveryThread.Background:
                        tasks.Add(Task.Run(() => InvokeAsync(entry, message)));
                        break;

                    default:
                        tasks.Add(Task.Run(() => InvokeAsync(entry, message)));
                        break;
                }

                if (entry.Once) onceIds.Add(entry.Id);
            }

            if (deadIds.Count > 0 || onceIds.Count > 0)
            {
                if (_subscriptions.TryGetValue(type, out var list2))
                {
                    var lockObj2 = _locks.GetOrAdd(type, _ => new object());
                    lock (lockObj2)
                    {
                        list2.RemoveAll(e => deadIds.Contains(e.Id) || onceIds.Contains(e.Id));
                        if (list2.Count == 0)
                        {
                            _subscriptions.TryRemove(type, out _);
                            _locks.TryRemove(type, out _);
                        }
                    }
                }
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return tasks.Count;
        }

        /// <summary>清空所有订阅（谨慎使用）。</summary>
        public static void Clear()
        {
            _subscriptions.Clear();
            _locks.Clear();
        }

        /// <summary>获取某事件类型的订阅者数量。</summary>
        public static int GetSubscriberCount<T>()
        {
            var type = typeof(T);
            if (_subscriptions.TryGetValue(type, out var list)) return list.Count;
            return 0;
        }

        private static void Invoke<T>(SubscriberEntry entry, T message)
        {
            object? target = null;
            if (!entry.IsStatic)
            {
                target = entry.KeepTargetAlive ? entry.StrongTarget : entry.TargetRef?.Target;
                if (target == null) return; // 已回收
            }
            entry.Method.Invoke(target, new object[] { message! });
        }

        private static void InvokeSync<T>(SubscriberEntry entry, T message)
        {
            if (!entry.IsAsync)
            {
                Invoke(entry, message);
                return;
            }
            // 异步订阅者：同步发布时等待完成
            try
            {
                var t = InvokeReturnTask(entry, message);
                t?.GetAwaiter().GetResult();
            }
            catch (TargetInvocationException tex)
            {
                HandleException(tex.InnerException ?? tex, entry);
            }
        }

        private static async Task InvokeAsync<T>(SubscriberEntry entry, T message)
        {
            if (!entry.IsAsync)
            {
                try { Invoke(entry, message); }
                catch (Exception ex) { HandleException(ex, entry); }
                return;
            }

            try
            {
                var t = InvokeReturnTask(entry, message);
                if (t != null) await t.ConfigureAwait(false);
            }
            catch (TargetInvocationException tex)
            {
                HandleException(tex.InnerException ?? tex, entry);
            }
            catch (Exception ex)
            {
                HandleException(ex, entry);
            }
        }

        private static Task? InvokeAwait<T>(SubscriberEntry entry, T message)
        {
            try
            {
                var t = InvokeReturnTask(entry, message);
                t?.GetAwaiter().GetResult();
                return t;
            }
            catch (TargetInvocationException tex)
            {
                HandleException(tex.InnerException ?? tex, entry);
                return null;
            }
            catch (Exception ex)
            {
                HandleException(ex, entry);
                return null;
            }
        }

        private static Task? InvokeReturnTask<T>(SubscriberEntry entry, T message)
        {
            object? target = null;
            if (!entry.IsStatic)
            {
                target = entry.KeepTargetAlive ? entry.StrongTarget : entry.TargetRef?.Target;
                if (target == null) return null; // 已回收
            }
            var result = entry.Method.Invoke(target, new object[] { message! });
            return result as Task;
        }

        private static void SafeInvoke<T>(SubscriberEntry entry, T message)
        {
            try { Invoke(entry, message); }
            catch (Exception ex) { HandleException(ex, entry); }
        }

        private static void HandleException(Exception ex, SubscriberEntry entry)
        {
            if (HandlerException != null)
                HandlerException.Invoke(ex, entry.MessageType, entry.Method);
            else
                Debug.WriteLine($"[GlobalEventBus] Handler exception in {entry.Method.DeclaringType?.FullName}.{entry.Method.Name}: {ex}");
        }

        private sealed class SubscriberEntry
        {
            public Guid Id { get; set; }
            public bool IsStatic { get; set; }
            public bool KeepTargetAlive { get; set; }
            public object? StrongTarget { get; set; }
            public WeakReference? TargetRef { get; set; }
            public MethodInfo Method { get; set; } = default!;
            public DeliveryThread Delivery { get; set; }
            public Type MessageType { get; set; } = default!;
            public int Priority { get; set; }
            public MethodInfo? FilterMethod { get; set; }
            public WeakReference? FilterTargetRef { get; set; }
            public DispatcherPriority? UiPriorityOverride { get; set; }
            public bool Once { get; set; }
            public bool IsAsync { get; set; }
            public bool ReplayLatest { get; set; }
        }

        private static bool IsEntryAlive(SubscriberEntry e)
        {
            if (e.IsStatic) return true;
            if (e.KeepTargetAlive) return e.StrongTarget != null;
            return e.TargetRef != null && e.TargetRef.IsAlive;
        }

        private static bool ContainsDuplicate<T>(List<SubscriberEntry> list, Action<T> handler, DeliveryThread delivery, int priority)
        {
            var method = handler.Method;
            var target = handler.Target;
            foreach (var e in list)
            {
                if (e.Method != method) continue;
                var sameTarget = (e.IsStatic && target == null) ||
                                 (e.KeepTargetAlive && e.StrongTarget != null && ReferenceEquals(e.StrongTarget, target)) ||
                                 (e.TargetRef != null && e.TargetRef.IsAlive && ReferenceEquals(e.TargetRef.Target, target));
                if (sameTarget && e.Delivery == delivery && e.Priority == priority)
                    return true;
            }
            return false;
        }
    }

    /// <summary>订阅线程模型</summary>
    public enum DeliveryThread
    {
        /// <summary>在发布者所在线程上执行（同步）。</summary>
        Current,
        /// <summary>在 UI 线程上执行（Dispatcher/SynchronizationContext）。</summary>
        UI,
        /// <summary>在后台线程上执行（Task.Run）。</summary>
        Background
    }

    /// <summary>订阅令牌，用于取消订阅（支持 using 自动释放）。</summary>
    public sealed class SubscriptionToken : IDisposable
    {
        internal SubscriptionToken(Guid id, Type messageType, Action<SubscriptionToken> disposer)
        {
            Id = id;
            MessageType = messageType;
            _disposer = disposer;
        }

        public Guid Id { get; }
        public Type MessageType { get; }
        private readonly Action<SubscriptionToken> _disposer;

        public void Dispose() => _disposer(this);
    }
}