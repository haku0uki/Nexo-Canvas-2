using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;

public class TrackImageInfo : MonoBehaviour
{
    [SerializeField] private InputActionProperty m_TapAction;

    [SerializeField]
    ARTrackedImageManager m_TrackedImageManager;

    [System.Obsolete]
    void OnEnable() => m_TrackedImageManager.trackedImagesChanged += OnChanged;

    [System.Obsolete]
    void OnDisable() => m_TrackedImageManager.trackedImagesChanged -= OnChanged;

    [System.Obsolete]
    void OnChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var newImage in eventArgs.added)
        {
            ListImages();
        }

        foreach (var updatedImage in eventArgs.updated)
        {
            // Handle updated event
        }

        foreach (var removedImage in eventArgs.removed)
        {
            ListImages();
        }
    }

    void Update()
    {
        if (m_TapAction.action.triggered)
        {
            ListImages();
        }
    }

    void ListImages()
    {
        Debug.Log($"Currently tracked images: {m_TrackedImageManager.trackables.count}");

        foreach (var trackedImage in m_TrackedImageManager.trackables)
        {
            Debug.Log($"Tracked image: {trackedImage.referenceImage.name}");
        }
    }

}
