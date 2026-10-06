using NUnit.Framework;

namespace SheepyCreations.EventBus.Tests
{
    public class EventBusExtensionsTests
    {
        struct SampleEvent : IEvent
        {
        }

        struct AnotherSampleEvent : IEvent
        {
        }

        [TearDown]
        public void TearDown()
        {
            EventBusUtil.ClearAllBuses();
        }

        [Test]
        public void RaiseEvent_ExtensionMethod_InvokesSubscribers()
        {
            bool wasInvoked = false;
            var group = new EventGroup();
            group.Subscribe<SampleEvent>(() => wasInvoked = true);

            this.RaiseEvent(new SampleEvent());

            Assert.IsTrue(wasInvoked);
        }

        [Test]
        public void SubscribeOnce_FiresOnlyOnce()
        {
            int invocationCount = 0;
            var group = new EventGroup();
            group.SubscribeOnce<SampleEvent>(_ => invocationCount++);

            EventBus<SampleEvent>.Raise(new SampleEvent());
            EventBus<SampleEvent>.Raise(new SampleEvent());

            Assert.AreEqual(1, invocationCount);
        }

        // Guards the fix where SubscribeOnce bindings were not tracked by the group,
        // so Clear() would not know about them.
        [Test]
        public void SubscribeOnce_IsTrackedByGroup_ClearDoesNotThrowAfterFiring()
        {
            var group = new EventGroup();
            group.SubscribeOnce<SampleEvent>(_ => { });

            EventBus<SampleEvent>.Raise(new SampleEvent());

            Assert.DoesNotThrow(() => group.Clear());
        }

        [Test]
        public void SubscribeIf_OnlyInvokesWhenConditionIsTrue()
        {
            bool shouldInvoke = false;
            int invocationCount = 0;

            var group = new EventGroup();
            group.SubscribeIf<SampleEvent>(() => shouldInvoke, _ => invocationCount++);

            EventBus<SampleEvent>.Raise(new SampleEvent());
            Assert.AreEqual(0, invocationCount);

            shouldInvoke = true;
            EventBus<SampleEvent>.Raise(new SampleEvent());
            Assert.AreEqual(1, invocationCount);
        }

        [Test]
        public void SubscribeMultiple_SubscribesToBothEventTypes()
        {
            int invocationCount = 0;
            var group = new EventGroup();
            group.SubscribeMultiple<SampleEvent, AnotherSampleEvent>(() => invocationCount++);

            EventBus<SampleEvent>.Raise(new SampleEvent());
            EventBus<AnotherSampleEvent>.Raise(new AnotherSampleEvent());

            Assert.AreEqual(2, invocationCount);
        }
    }
}
