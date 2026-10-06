using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro; // Asegúrate de incluir esto si usas TextMeshPro

[RequireComponent(typeof(ARTrackedImageManager))]
public class ARImageTextController : MonoBehaviour
{
    private ARTrackedImageManager imageManager;

    [Tooltip("El Prefab de tu objeto de Texto (TextMeshPro) que se mostrará.")]
    public GameObject uiTextPrefab;

    private GameObject spawnedText;

    void Awake()
    {
        imageManager = GetComponent<ARTrackedImageManager>();
    }

    void OnEnable()
    {
        imageManager.trackablesChanged.AddListener(OnTrackablesChanged);
    }

    void OnDisable()
    {
        imageManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
    }

    private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        // Cuando se detecta por primera vez
        foreach (var trackedImage in eventArgs.added)
        {
            HandleTextPresentation(trackedImage);
        }

        // Cuando se actualiza el rastreo
        foreach (var trackedImage in eventArgs.updated)
        {
            HandleTextPresentation(trackedImage);
        }

        // Cuando la imagen sale de la vista de la cámara
        foreach (var trackedImage in eventArgs.removed)
        {
            if (spawnedText != null)
            {
                spawnedText.SetActive(false);
            }
        }
    }

    private void HandleTextPresentation(ARTrackedImage trackedImage)
    {
        // Verifica si la cámara realmente está siguiendo la imagen en este frame
        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            if (spawnedText == null)
            {
                // Instancia el texto como hijo del objeto rastreado para que lo siga
                spawnedText = Instantiate(uiTextPrefab, trackedImage.transform);

                // Opcional: Ajusta la posición local si quieres que flote un poco más arriba de la imagen
                spawnedText.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            }
            else
            {
                // Asegura su posición y lo mantiene visible
                spawnedText.transform.position = trackedImage.transform.position;
                spawnedText.transform.rotation = trackedImage.transform.rotation;
                spawnedText.SetActive(true);
            }
        }
        else
        {
            // Si se oculta o pierde el tracking, desactivamos el texto
            if (spawnedText != null)
            {
                spawnedText.SetActive(false);
            }
        }
    }
}