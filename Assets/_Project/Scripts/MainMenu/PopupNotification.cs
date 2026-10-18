using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SheepyCreations.UIControl.Runtime
{
    /// <summary>
    /// Muestra notificaciones emergentes con un mismo UIDocumentPanel y un mismo formato.
    /// Solo cambia el texto. Los mensajes se definen en el Inspector y se disparan desde
    /// el evento onClick de un ButtonBridge (ShowByIndex, ShowByKey o ShowText).
    /// </summary>
    [AddComponentMenu("SheepyCreations/UI Control/Pop Up Notification")]
    public class PopUpNotification : MonoBehaviour
    {
        [Serializable]
        public class NotificationEntry
        {
            [Tooltip("Identificador para llamar a esta notificacion con ShowByKey.")]
            public string key;
            [TextArea(1, 4)] public string message;
            [Tooltip("Duracion en segundos. 0 o menos usa la duracion por defecto.")]
            public float durationOverride = 0f;
        }

        [Header("Panel")]
        [Tooltip("Panel que contiene el diseno de la notificacion (UIDocumentPanel).")]
        [SerializeField] private UIDocumentPanel panel;
        [Tooltip("Nombre del Label (UXML / UI Builder) donde se escribe el texto.")]
        [SerializeField] private string labelElementName = "NotificationLabel";

        [Header("Comportamiento")]
        [Min(0.1f)][SerializeField] private float defaultDuration = 2.5f;
        [Tooltip("Duracion del fade in / out. Negativo (-1) = usa la transicion configurada en el propio UIDocumentPanel.")]
        [SerializeField] private float fadeSeconds = -1f;
        [Tooltip("Duracion del fade cuando Fade Seconds es negativo. Pon el mismo valor que Transition Duration del panel.")]
        [Min(0f)][SerializeField] private float fadeFallback = 0.2f;

        [Header("Notificaciones")]
        [SerializeField] private List<NotificationEntry> notifications = new List<NotificationEntry>();

        private Coroutine hideRoutine;

        private void Reset() => panel = GetComponent<UIDocumentPanel>();

        private void Awake()
        {
            if (panel == null) panel = GetComponent<UIDocumentPanel>();
            if (panel == null)
                Debug.LogError($"[PopUpNotification:{name}] Falta asignar el UIDocumentPanel.", this);
        }

        //  API publica (usable desde UnityEvents del ButtonBridge) 

        /// <summary>Muestra la notificacion de la lista por posicion.</summary>
        public void ShowByIndex(int index)
        {
            if (index < 0 || index >= notifications.Count)
            {
                Debug.LogWarning($"[PopUpNotification:{name}] Indice {index} fuera de rango.", this);
                return;
            }
            Present(notifications[index].message, notifications[index].durationOverride);
        }

        /// <summary>Muestra la notificacion de la lista por su key.</summary>
        public void ShowByKey(string key)
        {
            var entry = notifications.Find(n => n.key == key);
            if (entry == null)
            {
                Debug.LogWarning($"[PopUpNotification:{name}] No existe la notificacion '{key}'.", this);
                return;
            }
            Present(entry.message, entry.durationOverride);
        }

        /// <summary>Muestra un texto escrito directamente en el argumento del UnityEvent.</summary>
        public void ShowText(string message) => Present(message, 0f);

        /// <summary>Oculta la notificacion manualmente.</summary>
        public void Hide()
        {
            StopHideRoutine();
            HidePanel();
        }

        //  Internos 

        private void Present(string message, float durationOverride)
        {
            if (panel == null) return;

            if (!panel.TryGetElement<Label>(labelElementName, out var label))
            {
                Debug.LogWarning($"[PopUpNotification:{name}] No se encontro el Label '{labelElementName}' en el documento.", this);
                return;
            }

            label.text = message;
            ShowPanel();

            // Una notificacion nueva reinicia el temporizador de la anterior.
            StopHideRoutine();
            float duration = durationOverride > 0f ? durationOverride : defaultDuration;
            hideRoutine = StartCoroutine(HideAfter(duration));
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            hideRoutine = null;
            HidePanel();
        }

        private void StopHideRoutine()
        {
            if (hideRoutine == null) return;
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        // Si tu UIPanel usa otra firma para mostrar/ocultar, ajustala solo aqui.
        // Show y Hide reciben un float (no nullable): sin override se usa fadeFallback.
        private float Fade => fadeSeconds >= 0f ? fadeSeconds : fadeFallback;
        private void ShowPanel() => panel.Show(Fade, null);
        private void HidePanel() => panel.Hide(Fade, null);

        private void OnDisable() => StopHideRoutine();
    }
}