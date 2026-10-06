using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SheepyCreations.EventBus.Tests
{
    public class EventBusListenerTests
    {
        struct SampleEvent : IEvent
        {
        }

        class SampleEventListener : EventBusListener<SampleEvent>
        {
        }

        GameObject testGameObject;
        SampleEventListener listener;

        [SetUp]
        public void SetUp()
        {
            testGameObject = new GameObject(nameof(EventBusListenerTests));
            listener = testGameObject.AddComponent<SampleEventListener>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(testGameObject);
            EventBusUtil.ClearAllBuses();
        }

        [UnityTest]
        public IEnumerator InvokesUnityEvent_WhenEventIsRaisedWhileEnabled()
        {
            bool wasInvoked = false;
            listener.OnEventRaised.AddListener(() => wasInvoked = true);

            yield return null;

            EventBus<SampleEvent>.Raise(new SampleEvent());

            Assert.IsTrue(wasInvoked);
        }

        [UnityTest]
        public IEnumerator DoesNotInvoke_AfterGameObjectIsDisabled()
        {
            bool wasInvoked = false;
            listener.OnEventRaised.AddListener(() => wasInvoked = true);

            yield return null;

            testGameObject.SetActive(false);
            yield return null;

            EventBus<SampleEvent>.Raise(new SampleEvent());

            Assert.IsFalse(wasInvoked);
        }

        [UnityTest]
        public IEnumerator ResubscribesCorrectly_AfterBeingReEnabled()
        {
            int invocationCount = 0;
            listener.OnEventRaised.AddListener(() => invocationCount++);

            yield return null;

            testGameObject.SetActive(false);
            yield return null;
            testGameObject.SetActive(true);
            yield return null;

            EventBus<SampleEvent>.Raise(new SampleEvent());

            Assert.AreEqual(1, invocationCount);
        }
    }
}
