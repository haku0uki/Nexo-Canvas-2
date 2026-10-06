using NUnit.Framework;

namespace SheepyCreations.EventBus.Tests
{
    public class EventGroupTests
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
        public void Subscribe_ReturnsSameGroupForChaining()
        {
            var group = new EventGroup();
            var returnedGroup = group.Subscribe<SampleEvent>(() => { });

            Assert.AreSame(group, returnedGroup);
        }

        [Test]
        public void Clear_RemovesAllSubscriptions()
        {
            bool wasInvoked = false;
            var group = new EventGroup();
            group.Subscribe<SampleEvent>(() => wasInvoked = true);

            group.Clear();
            EventBus<SampleEvent>.Raise(new SampleEvent());

            Assert.IsFalse(wasInvoked);
            Assert.AreEqual(0, group.Count);
        }

        [Test]
        public void Unsubscribe_OfType_OnlyRemovesThatType()
        {
            int sampleInvocationCount = 0;
            int anotherInvocationCount = 0;

            var group = new EventGroup();
            group.Subscribe<SampleEvent>(() => sampleInvocationCount++);
            group.Subscribe<AnotherSampleEvent>(() => anotherInvocationCount++);

            group.Unsubscribe<SampleEvent>();
            EventBus<SampleEvent>.Raise(new SampleEvent());
            EventBus<AnotherSampleEvent>.Raise(new AnotherSampleEvent());

            Assert.AreEqual(0, sampleInvocationCount);
            Assert.AreEqual(1, anotherInvocationCount);
        }

        [Test]
        public void HasSubscriptions_ReflectsCurrentState()
        {
            var group = new EventGroup();
            Assert.IsFalse(group.HasSubscriptions);

            group.Subscribe<SampleEvent>(() => { });
            Assert.IsTrue(group.HasSubscriptions);

            group.Clear();
            Assert.IsFalse(group.HasSubscriptions);
        }
    }
}
