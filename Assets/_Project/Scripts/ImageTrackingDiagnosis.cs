using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace SheepyCreations.ARDebugging
{
    public class ImageTrackingDiagnostics : MonoBehaviour
    {
        [SerializeField] private ARTrackedImageManager trackedImageManager;
        [SerializeField] private float logIntervalSeconds = 1f;

        private float nextLogTime;

        private void OnEnable()
        {
            trackedImageManager.trackablesChanged.AddListener(OnTrackablesChanged);
        }

        private void OnDisable()
        {
            trackedImageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
        }

        private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
        {
            Debug.Log($"[ARDiagnostics] added: {eventArgs.added.Count}, updated: {eventArgs.updated.Count}, removed: {eventArgs.removed.Count}");
        }

        private void Update()
        {
            if (Time.time < nextLogTime) return;
            nextLogTime = Time.time + logIntervalSeconds;

            Debug.Log(
                $"[ARDiagnostics] session: {ARSession.state}, " +
                $"notTrackingReason: {ARSession.notTrackingReason}, " +
                $"managerEnabled: {trackedImageManager.enabled}, " +
                $"subsystemRunning: {trackedImageManager.subsystem?.running}, " +
                $"libraryCount: {trackedImageManager.referenceLibrary?.count}, " +
                $"trackables: {trackedImageManager.trackables.count}");
        }
    }
}