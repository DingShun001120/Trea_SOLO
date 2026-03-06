using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Xunit;

namespace GlobalEventManager.Tests
{
    public class GlobalEventBusTests
    {
        public GlobalEventBusTests()
        {
            // 重置总线状态，避免测试间相互影响
            GlobalEventBus.Clear();
            GlobalEventBus.Configure(Dispatcher.CurrentDispatcher);
        }

        [Fact]
        public void Subscribe_Publish_Delivers()
        {
            var received = 0;
            using var t = GlobalEventBus.Subscribe<LoginSuccessEvent>(e => { received++; }, priority: 0, filter: null, delivery: DeliveryThread.Current);

            var countBefore = GlobalEventBus.GetSubscriberCount<LoginSuccessEvent>();
            Assert.Equal(1, countBefore);

            GlobalEventBus.Publish(new LoginSuccessEvent { UserName = "Alice" });
            Assert.Equal(1, received);
        }

        [Fact]
        public void Unsubscribe_Removes()
        {
            var received = 0;
            var token = GlobalEventBus.Subscribe<LoginSuccessEvent>(e => { received++; }, priority: 0, filter: null, delivery: DeliveryThread.Current);
            GlobalEventBus.Unsubscribe(token);

            Assert.Equal(0, GlobalEventBus.GetSubscriberCount<LoginSuccessEvent>());
            GlobalEventBus.Publish(new LoginSuccessEvent { UserName = "Bob" });
            Assert.Equal(0, received);
        }

        private sealed class TempSubscriber
        {
            public void Handle(LoginSuccessEvent e) { /* no-op */ }
        }

        [Fact]
        public void WeakReference_Cleanup_AfterGc()
        {
            var s = new TempSubscriber();
            GlobalEventBus.Subscribe<LoginSuccessEvent>(s.Handle, priority: 0, filter: null, delivery: DeliveryThread.Current);
            Assert.Equal(1, GlobalEventBus.GetSubscriberCount<LoginSuccessEvent>());

            s = null!;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // 发布一次以触发清理
            GlobalEventBus.Publish(new LoginSuccessEvent { UserName = "GC" });
            Assert.Equal(0, GlobalEventBus.GetSubscriberCount<LoginSuccessEvent>());
        }

        [Fact]
        public void Filter_Allows_MatchingOnly()
        {
            var received = 0;
            using var t = GlobalEventBus.Subscribe<LoginSuccessEvent>(e => { received++; }, priority: 0,
                filter: (e) => e.UserName.StartsWith("A", StringComparison.OrdinalIgnoreCase), delivery: DeliveryThread.Current);

            GlobalEventBus.Publish(new LoginSuccessEvent { UserName = "Bob" });
            GlobalEventBus.Publish(new LoginSuccessEvent { UserName = "Alice" });

            Assert.Equal(1, received);
        }

        [Fact]
        public void Priority_HigherRunsFirst()
        {
            var order = new List<string>();
            using var t1 = GlobalEventBus.Subscribe<LoginSuccessEvent>(e => order.Add("low"), priority: 0, filter: null, delivery: DeliveryThread.Current);
            using var t2 = GlobalEventBus.Subscribe<LoginSuccessEvent>(e => order.Add("high"), priority: 10, filter: null, delivery: DeliveryThread.Current);

            GlobalEventBus.Publish(new LoginSuccessEvent { UserName = "Alice" });
            Assert.Equal(new[] { "high", "low" }, order);
        }

        [Fact]
        public void UiDelivery_UsesDispatcher()
        {
            var uiThreadId = 0;
            var executedOnUiThread = false;

            var started = new ManualResetEventSlim(false);
            Dispatcher? uiDispatcher = null;

            var th = new Thread(() =>
            {
                uiThreadId = Thread.CurrentThread.ManagedThreadId;
                uiDispatcher = Dispatcher.CurrentDispatcher;
                GlobalEventBus.Configure(uiDispatcher);

                using var token = GlobalEventBus.Subscribe<LoginSuccessEvent>(e =>
                {
                    executedOnUiThread = Thread.CurrentThread.ManagedThreadId == uiThreadId;
                }, priority: 0, filter: null, delivery: DeliveryThread.UI);

                started.Set();
                Dispatcher.Run();
            });
            th.SetApartmentState(ApartmentState.STA);
            th.IsBackground = true;
            th.Start();

            started.Wait();
            GlobalEventBus.Publish(new LoginSuccessEvent { UserName = "Alice" });

            Assert.True(executedOnUiThread);

            // 关闭 Dispatcher
            uiDispatcher!.BeginInvokeShutdown(DispatcherPriority.Background);
            th.Join();
        }
    }
}