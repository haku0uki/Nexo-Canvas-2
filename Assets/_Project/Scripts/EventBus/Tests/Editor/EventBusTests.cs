using System;
using NUnit.Framework;

namespace SheepyCreations.EventBus.Tests
{
    public class EventBusTests
    {
        struct SampleEvent : IEvent
        {
            public int Value;
        }

        [TearDown]
        public void TearDown()
        {
            EventBusUtil.ClearAllBuses();
        }

        [Test]
        public void Raise_InvokesRegisteredBinding()
        {
            int receivedValue = 0;
            var binding = new EventBinding<SampleEvent>(@event => receivedValue = @event.Value);
            EventBus<SampleEvent>.Register(binding);

            EventBus<SampleEvent>.Raise(new SampleEvent { Value = 42 });

            Assert.AreEqual(42, receivedValue);
        }

        [Test]
        public void Raise_DoesNotInvokeAfterDeregister()
        {
            bool wasInvoked = false;
            var binding = new EventBinding<SampleEvent>(_ => wasInvoked = true);
            EventBus<SampleEvent>.Register(binding);
            EventBus<SampleEvent>.Deregister(binding);

            EventBus<SampleEvent>.Raise(new SampleEvent());

            Assert.IsFalse(wasInvoked);
        }

        [Test]
        public void Raise_InvokesEveryRegisteredBinding()
        {
            int invocationCount = 0;
            var firstBinding = new EventBinding<SampleEvent>(_ => invocationCount++);
            var secondBinding = new EventBinding<SampleEvent>(_ => invocationCount++);
            EventBus<SampleEvent>.Register(firstBinding);
            EventBus<SampleEvent>.Register(secondBinding);

            EventBus<SampleEvent>.Raise(new SampleEvent());

            Assert.AreEqual(2, invocationCount);
        }

        [Test]
        public void Register_NullBinding_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => EventBus<SampleEvent>.Register(null));
        }

        [Test]
        public void Deregister_NullBinding_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => EventBus<SampleEvent>.Deregister(null));
        }

        // Guards the cached-snapshot optimization in Raise: a binding registered after the
        // snapshot was already built and cached must still be invoked on the next Raise.
        [Test]
        public void Raise_IncludesBindingsRegisteredAfterSnapshotWasCached()
        {
            int invocationCount = 0;
            var firstBinding = new EventBinding<SampleEvent>(_ => invocationCount++);
            EventBus<SampleEvent>.Register(firstBinding);
            EventBus<SampleEvent>.Raise(new SampleEvent());

            var secondBinding = new EventBinding<SampleEvent>(_ => invocationCount++);
            EventBus<SampleEvent>.Register(secondBinding);
            EventBus<SampleEvent>.Raise(new SampleEvent());

            Assert.AreEqual(3, invocationCount);
        }
    }
}
