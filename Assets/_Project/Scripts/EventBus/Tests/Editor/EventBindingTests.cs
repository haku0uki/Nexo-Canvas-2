using System;
using NUnit.Framework;

namespace SheepyCreations.EventBus.Tests
{
    public class EventBindingTests
    {
        struct SampleEvent : IEvent
        {
        }

        [TearDown]
        public void TearDown()
        {
            EventBusUtil.ClearAllBuses();
        }

        [Test]
        public void Constructor_NullActionWithData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new EventBinding<SampleEvent>((Action<SampleEvent>)null));
        }

        [Test]
        public void Constructor_NullActionWithoutData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new EventBinding<SampleEvent>((Action)null));
        }

        [Test]
        public void Add_NullActionWithData_ThrowsArgumentNullException()
        {
            var binding = new EventBinding<SampleEvent>(() => { });
            Assert.Throws<ArgumentNullException>(() => binding.Add((Action<SampleEvent>)null));
        }

        [Test]
        public void Add_NullActionWithoutData_ThrowsArgumentNullException()
        {
            var binding = new EventBinding<SampleEvent>(() => { });
            Assert.Throws<ArgumentNullException>(() => binding.Add((Action)null));
        }

        [Test]
        public void ConstructedWithNoArgsCallback_InvokesOnRaise()
        {
            bool wasInvoked = false;
            var binding = new EventBinding<SampleEvent>(() => wasInvoked = true);
            EventBus<SampleEvent>.Register(binding);

            EventBus<SampleEvent>.Raise(new SampleEvent());

            Assert.IsTrue(wasInvoked);
        }

        // Guards the wrapper lookup in EventBinding.Remove(Action): removing a no-args
        // callback must unhook the exact wrapper that the matching Add created.
        [Test]
        public void RemovingNoArgsCallback_StopsReceivingFurtherEvents()
        {
            int invocationCount = 0;
            void Callback() => invocationCount++;

            var binding = new EventBinding<SampleEvent>(Callback);
            EventBus<SampleEvent>.Register(binding);

            EventBus<SampleEvent>.Raise(new SampleEvent());
            binding.Remove((Action)Callback);
            EventBus<SampleEvent>.Raise(new SampleEvent());

            Assert.AreEqual(1, invocationCount);
        }

        [Test]
        public void RemovingOneOfMultipleNoArgsCallbacks_OnlyRemovesThatSubscription()
        {
            int invocationCount = 0;
            void Callback() => invocationCount++;

            var binding = new EventBinding<SampleEvent>(Callback);
            binding.Add(Callback);
            EventBus<SampleEvent>.Register(binding);

            EventBus<SampleEvent>.Raise(new SampleEvent());
            binding.Remove((Action)Callback);
            EventBus<SampleEvent>.Raise(new SampleEvent());

            Assert.AreEqual(2, invocationCount);
        }
    }
}
