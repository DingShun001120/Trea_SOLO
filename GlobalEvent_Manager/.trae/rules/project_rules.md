它的核心目标是 **在不同模块之间实现松耦合的通信** —— 不同模块不需要直接引用对方即可触发或响应事件。

---
## 🧩 一、核心原理概述
全局事件管理器的原理基于：

> “**发布-订阅模式（Publish–Subscribe Pattern）**”

也称 **消息中心（Message Bus）** 或 **事件聚合器（Event Aggregator）**。

它的关键思想是：

- **订阅者（Subscriber）** 监听某类事件。
- **发布者（Publisher）** 触发事件，但不关心谁在监听。
- **事件管理器（EventManager）** 作为中间人协调两者。

### 🔄 流程图：

```scss
[Module A] ---- Publish(EventX) ----> [Global EventManager] ----> [Module B] Handles(EventX)
```


---
## 🧠 二、核心设计要点
一个好的全局事件管理器通常满足以下特征：

|特性|描述|
|---|---|
|泛型化|支持不同类型的事件（如 `EventManager<T>`）|
|线程安全|订阅、取消订阅、触发都应可安全多线程操作|
|弱引用（WeakReference）|防止事件订阅者未释放导致内存泄漏|
|异步支持|支持同步或异步触发事件|
|解耦|发布者与订阅者无需相互引用|

---

## 🧱 三、基础实现（同步版）
以下是一个基础但实用的全局事件管理器实现：

```C#
using System;
using System.Collections.Generic;

public class EventManager
{
    private static readonly Dictionary<Type, List<Delegate>> eventTable = new();

    /// <summary> 订阅事件 </summary>
    public static void Subscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (!eventTable.ContainsKey(type))
            eventTable[type] = new List<Delegate>();
        eventTable[type].Add(handler);
    }

    /// <summary> 取消订阅事件 </summary>
    public static void Unsubscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (eventTable.ContainsKey(type))
        {
            eventTable[type].Remove(handler);
            if (eventTable[type].Count == 0)
                eventTable.Remove(type);
        }
    }

    /// <summary> 发布事件 </summary>
    public static void Publish<T>(T eventData)
    {
        var type = typeof(T);
        if (eventTable.TryGetValue(type, out var handlers))
        {
            foreach (var handler in handlers)
                (handler as Action<T>)?.Invoke(eventData);
        }
    }
}

```

### ✅ 使用示例：

```C#
// 定义事件类型
public class UserLoginEvent
{
    public string UserName { get; set; }
}

// 订阅事件（通常在启动时）
EventManager.Subscribe<UserLoginEvent>(OnUserLogin);

// 发布事件
EventManager.Publish(new UserLoginEvent { UserName = "Alice" });

// 事件处理器
void OnUserLogin(UserLoginEvent e)
{
    Console.WriteLine($"User {e.UserName} logged in!");
}

```

输出：

`User Alice logged in!`

---

## ⚙️ 四、增强版本（异步 + 弱引用）

在复杂场景中，比如 WPF / WinForms，需要防止窗口未释放导致内存泄漏，可以使用弱引用与异步触发：

```C#
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public static class GlobalEventManager
{
    private static readonly Dictionary<Type, List<WeakReference>> _subscribers = new();

    public static void Subscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (!_subscribers.ContainsKey(type))
            _subscribers[type] = new List<WeakReference>();
        _subscribers[type].Add(new WeakReference(handler));
    }

    public static void Unsubscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (_subscribers.TryGetValue(type, out var list))
        {
            list.RemoveAll(wr => !wr.IsAlive || wr.Target.Equals(handler));
        }
    }

    public static async Task PublishAsync<T>(T message)
    {
        var type = typeof(T);
        if (!_subscribers.ContainsKey(type)) return;

        var deadRefs = new List<WeakReference>();
        foreach (var weakRef in _subscribers[type])
        {
            if (weakRef.Target is Action<T> handler)
                await Task.Run(() => handler.Invoke(message));
            else
                deadRefs.Add(weakRef);
        }

        // 清理无效引用
        foreach (var dead in deadRefs)
            _subscribers[type].Remove(dead);
    }
}

```

---

## 🧩 五、在 WPF / MVVM 中的应用示例

在 WPF 项目中，全局事件管理器常用于 **ViewModel 之间通信**。

例如：

```C#
// 登录成功后通知主界面
EventManager.Publish(new LoginSuccessEvent { UserName = "Admin" });

// 主界面VM中订阅
EventManager.Subscribe<LoginSuccessEvent>(e =>
{
    StatusText = $"欢迎 {e.UserName}";
});

```

这避免了 ViewModel 间的直接引用，非常适合 **模块化 / 插件化系统**。

---

## 🧰 六、可扩展方向

|功能|说明|
|---|---|
|🔸 异步事件总线|支持 await 发布事件|
|🔸 优先级事件|支持按优先级顺序执行订阅者|
|🔸 事件过滤器|可在发布前过滤事件|
|🔸 日志与诊断|记录事件发布与订阅情况|
|🔸 与依赖注入集成|如与 `IServiceProvider` 集成，实现自动注册|

---

## 🧠 七、总结

|特点|说明|
|---|---|
|📡 原理|发布订阅模式（解耦通信）|
|💡 关键点|泛型、线程安全、弱引用、异步|
|🧩 场景|模块间通信、消息中心、插件架构、MVVM消息传递|
|🛠️ 扩展|异步、优先级、过滤器、日志、DI集成|