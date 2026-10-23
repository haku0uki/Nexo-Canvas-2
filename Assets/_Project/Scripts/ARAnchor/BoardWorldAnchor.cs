using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Mantiene el tablero fijo en el espacio fisico despues de detectar la hoja (AR Foundation 6.x).
//
//  1. Cuando la imagen se detecta, el tablero se muestra de inmediato sobre ella y, hasta
//     "Plane Search Seconds", se busca un plano detectado (mesa o pared) bajo la hoja.
//  2. Con plano: el tablero se apoya en el (queda a su altura y con su orientacion) y se crea un
//     ARAnchor ADJUNTO al plano, que se corrige junto con el. Sin plano: ARAnchor normal en la
//     pose de la hoja. Sin ARAnchorManager: una raiz propia, sin correccion del sistema.
//  3. El BoardCanvas pasa a colgar de esa ancla. Aunque la hoja salga de la camara, el tablero se
//     queda donde estaba (el seguimiento de movimiento del telefono lo mantiene).
//  4. Cada vez que la hoja vuelve a verse en Tracking, el tablero se realinea con ella, con
//     suavizado (y salta de golpe solo si la diferencia es grande).
//  5. El estado (BoardState) refleja el seguimiento del DISPOSITIVO, no el de la imagen.
//
// Escena: GameObject hijo del XR Origin (posicion 0, rotacion 0, escala 1) con este componente.
// El XR Origin necesita ARAnchorManager y, para apoyar el tablero, ARPlaneManager.
public class BoardWorldAnchor : MonoBehaviour
{
    public static BoardWorldAnchor Instance { get; private set; }

    [Header("AR Foundation (componentes del XR Origin; si quedan vacios se buscan en la escena)")]
    [SerializeField] private ARAnchorManager anchorManager;
    [SerializeField] private ARPlaneManager planeManager;

    [Header("Superficie de apoyo")]
    [Tooltip("Tiempo maximo esperando un plano bajo la hoja antes de anclar sin plano.")]
    [SerializeField] private float planeSearchSeconds = 1.5f;
    [Tooltip("Distancia maxima (m) entre la hoja y el plano para considerarlo su superficie.")]
    [SerializeField] private float planeMaxDistance = 0.15f;
    [Tooltip("Inclinacion maxima (grados) entre la hoja y el plano.")]
    [SerializeField] private float planeMaxAngle = 25f;
    [Tooltip("Margen (m) fuera del borde detectado del plano; la hoja suele quedar en el borde.")]
    [SerializeField] private float planeEdgeMargin = 0.3f;

    [Header("Realineacion con la hoja")]
    [Tooltip("Permite acomodar el tablero a la hoja cuando vuelve a verse.")]
    [SerializeField] private bool realignWhileImageTracked = true;
    [Tooltip("La hoja debe estar en Tracking de forma continua este tiempo antes de usar su pose. Evita lecturas inestables al reaparecer.")]
    [SerializeField] private float realignStableSeconds = 0.75f;
    [Tooltip("Duracion de la correccion cada vez que la hoja reaparece. Pasado ese tiempo el tablero ignora la hoja hasta que se pierda y vuelva a verse. 0 = seguirla siempre (puede temblar).")]
    [SerializeField] private float realignWindowSeconds = 2f;
    [Tooltip("Velocidad del deslizamiento. Mas bajo = mas suave y lento.")]
    [SerializeField] private float alignSpeed = 3f;

    [Header("Estabilidad del estado")]
    [Tooltip("Segundos que un cambio de estado debe mantenerse antes de aplicarse. Evita parpadeos por cambios breves.")]
    [SerializeField] private float stateChangeDelay = 0.3f;
    [Tooltip("Lo mismo para pasar a Lost (oculta el tablero), que conviene que sea mas lento.")]
    [SerializeField] private float lostDelay = 1.5f;

    private ARCanvasController.BoardState pendingState;
    private float pendingSince;
    private bool hasPendingState;

    private BoardCanvas board;
    private bool placed;
    private bool placing;
    private bool listening;

    // Pose del tablero relativa a la imagen, tal como quedo armado el prefab.
    private Vector3 offsetPosition;
    private Quaternion offsetRotation;

    private ARPlane supportPlane;
    private float nextPlaneSearch;
    private GameObject fallbackRoot;
    private ARAnchor currentAnchor;      // null si el tablero cuelga de la raiz propia
    private bool anchoredToPlane;
    private float nextUpgradeTry;

    // Imagen que esta guiando el tablero. Si otro prefab de imagen tambien reporta (por ejemplo,
    // por haber dos XR Origin), se ignora: dos poses distintas harian saltar el tablero.
    private Transform imageSource;
    private bool warnedSecondSource;

    private TrackingState lastImageState = TrackingState.None;
    private float trackingSince = -1f;   // Inicio del Tracking continuo de la hoja (-1 = no esta en Tracking)
    private float windowStart = -1f;     // Inicio de la ventana de correccion actual

    private bool hasTarget;
    private Pose targetPose;
    private int lastReportFrame = -10;
    private float nextWaitingLog;

    private ARCanvasController.BoardState state = ARCanvasController.BoardState.None;

    public bool IsPlaced => placed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[BoardWorldAnchor] Hay mas de uno en la escena; se ignora este.");
            enabled = false;
            return;
        }

        Instance = this;

        if (anchorManager == null) anchorManager = FindFirstObjectByType<ARAnchorManager>();
        if (planeManager == null) planeManager = FindFirstObjectByType<ARPlaneManager>();

        if (anchorManager == null)
            Debug.LogWarning("[BoardWorldAnchor] No hay ARAnchorManager: el tablero se fijara sin ancla del sistema.");
        if (planeManager == null)
            Debug.LogWarning("[BoardWorldAnchor] No hay ARPlaneManager: el tablero no se apoyara en una superficie.");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (fallbackRoot != null) Destroy(fallbackRoot);
    }

    // ARCanvasController lo llama cada frame mientras su prefab existe.
    public void ReportImage(BoardCanvas canvas, Transform image, TrackingState imageState)
    {
        if (!isActiveAndEnabled || image == null) return;

        if (imageSource != null && imageSource != image && imageSource.gameObject.activeInHierarchy)
        {
            if (!warnedSecondSource)
            {
                warnedSecondSource = true;
                Debug.LogWarning("[BoardWorldAnchor] Hay mas de una imagen reportando; se ignora la segunda. " +
                                 "Revisa que haya un solo XR Origin y un solo ARTrackedImageManager en la escena.");
            }

            return;
        }

        imageSource = image;
        lastImageState = imageState;

        if (!placed)
        {
            if (placing) return;

            // Igual que antes del ancla: con la imagen en Tracking o Limited el tablero ya se muestra.
            if (imageState == TrackingState.None || canvas == null)
            {
                LogWaiting(imageState, canvas);
                return;
            }

            _ = PlaceAsync(canvas, image);
            return;
        }

        if (imageState != TrackingState.Tracking)
        {
            trackingSince = -1f;
            windowStart = -1f;
            return;
        }

        if (trackingSince < 0f) trackingSince = Time.unscaledTime;

        UpgradeAnchorToPlane();

        targetPose = ApplySupportPlane(new Pose(image.TransformPoint(offsetPosition), image.rotation * offsetRotation));
        hasTarget = true;
        lastReportFrame = Time.frameCount;
    }

    // ------------------------------------------------------------
    // Colocacion
    // ------------------------------------------------------------

    private async Awaitable PlaceAsync(BoardCanvas canvas, Transform image)
    {
        placing = true;

        try
        {
            board = canvas;
            Transform boardTransform = canvas.transform;

            offsetPosition = image.InverseTransformPoint(boardTransform.position);
            offsetRotation = Quaternion.Inverse(image.rotation) * boardTransform.rotation;

            // Se muestra ya, sobre la imagen, mientras se busca la superficie.
            ShowBoardNow();

            Pose pose = new Pose(boardTransform.position, boardTransform.rotation);
            ARPlane plane = null;
            float deadline = Time.unscaledTime + planeSearchSeconds;
            float hardDeadline = Time.unscaledTime + 6f;

            while (true)
            {
                if (image != null)
                    pose = new Pose(image.TransformPoint(offsetPosition), image.rotation * offsetRotation);

                plane = FindSupportPlane(pose);

                // La pose con la hoja en Limited es poco fiable: se fija con la hoja en Tracking (maximo 6 s de espera).
                bool planeDone = plane != null || planeManager == null || Time.unscaledTime >= deadline;
                bool imageGood = lastImageState == TrackingState.Tracking;

                if ((planeDone && imageGood) || Time.unscaledTime >= hardDeadline) break;

                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }

            if (board == null) return;

            if (plane != null) pose = SnapToPlane(pose, plane);
            else LogPlaneDiagnostics(pose);

            Transform parent = await CreateAnchorAsync(plane, pose);
            if (board == null) return;

            boardTransform.SetPositionAndRotation(pose.position, pose.rotation);
            boardTransform.SetParent(parent, true);

            supportPlane = plane;
            placed = true;

            Debug.Log("[BoardWorldAnchor] Tablero fijado al espacio. " +
                      (plane != null ? "Apoyado en un plano detectado." : "Sin plano de apoyo."));

            EvaluateState(force: true);
        }
        catch (OperationCanceledException)
        {
            // El objeto se destruyo durante la espera.
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            placing = false;
        }
    }

    // Devuelve el Transform del que colgara el tablero. Nunca null.
    private async Awaitable<Transform> CreateAnchorAsync(ARPlane plane, Pose pose)
    {
        if (anchorManager != null)
        {
            try
            {
                if (plane != null)
                {
                    // Ancla adjunta al plano: ARCore la corrige junto con el plano.
                    // En AR Foundation 6.x es sincrona; devuelve null si falla.
                    ARAnchor attached = anchorManager.AttachAnchor(plane, pose);

                    if (attached != null)
                    {
                        currentAnchor = attached;
                        anchoredToPlane = true;
                        return attached.transform;
                    }

                    Debug.Log("[BoardWorldAnchor] No se pudo adjuntar el ancla al plano; se usa un ancla normal.");
                }

                var added = await anchorManager.TryAddAnchorAsync(pose);

                if (added.status.IsSuccess() && added.value != null)
                {
                    currentAnchor = added.value;
                    return added.value.transform;
                }

                Debug.LogWarning("[BoardWorldAnchor] El sistema no creo el ancla; se fija sin ancla.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[BoardWorldAnchor] Error creando el ancla: " + e.Message);
            }
        }

        fallbackRoot = new GameObject("BoardWorldRoot");
        fallbackRoot.transform.SetParent(transform, false);
        fallbackRoot.transform.SetPositionAndRotation(pose.position, pose.rotation);
        return fallbackRoot.transform;
    }

    private void ShowBoardNow()
    {
        if (board == null) return;

        if (!board.gameObject.activeSelf) board.gameObject.SetActive(true);

        if (!listening)
        {
            listening = true;
            board.StartListening(); // No-op si ya estaba escuchando.
        }
    }

    private void LogWaiting(TrackingState imageState, BoardCanvas canvas)
    {
        if (Time.unscaledTime < nextWaitingLog) return;
        nextWaitingLog = Time.unscaledTime + 1f;

        Debug.Log($"[BoardWorldAnchor] Esperando para colocar el tablero. Imagen: {imageState}. " +
                  (canvas == null ? "El campo Board Canvas del ARCanvasController esta vacio o fue destruido." : "BoardCanvas OK."));
    }

    // ------------------------------------------------------------
    // Planos
    // ------------------------------------------------------------

    // Plano detectado que coincide con la superficie donde esta la hoja, o null.
    private ARPlane FindSupportPlane(Pose pose)
    {
        if (planeManager == null) return null;

        ARPlane best = null;
        float bestDistance = planeMaxDistance;
        Vector3 up = pose.rotation * Vector3.up;

        foreach (ARPlane plane in planeManager.trackables)
        {
            if (plane == null || plane.trackingState != TrackingState.Tracking || plane.subsumedBy != null)
                continue;

            float distance = Mathf.Abs(plane.infinitePlane.GetDistanceToPoint(pose.position));
            if (distance > bestDistance) continue;

            float angle = Vector3.Angle(plane.normal, up);
            if (Mathf.Min(angle, 180f - angle) > planeMaxAngle) continue;

            // Dentro de lo detectado del plano (con margen): evita apoyarse en otra mesa lejana.
            Vector3 fromCenter = Vector3.ProjectOnPlane(pose.position - plane.transform.position, plane.normal);
            float reach = Mathf.Max(plane.size.x, plane.size.y) * 0.5f + planeEdgeMargin;
            if (fromCenter.magnitude > reach) continue;

            best = plane;
            bestDistance = distance;
        }

        return best;
    }

    // Si el tablero se fijo sin plano y despues aparece uno, se cambia el ancla por una adjunta a ese plano
    // (la pose del tablero no se mueve). Solo ocurre una vez por plano encontrado.
    private void UpgradeAnchorToPlane()
    {
        if (anchoredToPlane || supportPlane == null || anchorManager == null || board == null) return;
        if (Time.unscaledTime < nextUpgradeTry) return; // Si falla, no reintentar cada frame.
        nextUpgradeTry = Time.unscaledTime + 1f;

        Transform boardTransform = board.transform;
        ARAnchor attached = anchorManager.AttachAnchor(supportPlane, new Pose(boardTransform.position, boardTransform.rotation));
        if (attached == null) return;

        ARAnchor previous = currentAnchor;

        boardTransform.SetParent(attached.transform, true);
        currentAnchor = attached;
        anchoredToPlane = true;

        if (previous != null) anchorManager.TryRemoveAnchor(previous);

        if (fallbackRoot != null)
        {
            Destroy(fallbackRoot);
            fallbackRoot = null;
        }

        Debug.Log("[BoardWorldAnchor] Plano encontrado despues de colocar el tablero: ancla actualizada para apoyarse en el.");
    }

    // Explica por que no se hallo plano: cuantos hay y que tan cerca esta el mas parecido.
    private void LogPlaneDiagnostics(Pose pose)
    {
        if (planeManager == null)
        {
            Debug.Log("[BoardWorldAnchor] Sin plano: no hay ARPlaneManager.");
            return;
        }

        int count = 0;
        string closest = "ninguno";
        float closestDistance = float.MaxValue;
        Vector3 up = pose.rotation * Vector3.up;

        foreach (ARPlane plane in planeManager.trackables)
        {
            if (plane == null) continue;
            count++;

            float distance = Mathf.Abs(plane.infinitePlane.GetDistanceToPoint(pose.position));
            if (distance >= closestDistance) continue;

            float angle = Vector3.Angle(plane.normal, up);
            angle = Mathf.Min(angle, 180f - angle);
            float planar = Vector3.ProjectOnPlane(pose.position - plane.transform.position, plane.normal).magnitude;
            float reach = Mathf.Max(plane.size.x, plane.size.y) * 0.5f + planeEdgeMargin;

            closestDistance = distance;
            closest = $"distancia {distance:F2} m (max {planeMaxDistance:F2}), angulo {angle:F0} grados (max {planeMaxAngle:F0}), " +
                      $"separacion lateral {planar:F2} m (alcance {reach:F2}), estado {plane.trackingState}";
        }

        Debug.Log($"[BoardWorldAnchor] Sin plano de apoyo. Planos detectados: {count}. Mas cercano: {closest}.");
    }

    // Lleva la pose sobre el plano: misma posicion proyectada, "arriba" alineado con la normal.
    private static Pose SnapToPlane(Pose pose, ARPlane plane)
    {
        Vector3 up = pose.rotation * Vector3.up;
        Vector3 normal = plane.normal;
        if (Vector3.Dot(normal, up) < 0f) normal = -normal;

        Vector3 position = plane.infinitePlane.ClosestPointOnPlane(pose.position);
        Quaternion rotation = Quaternion.FromToRotation(up, normal) * pose.rotation;

        return new Pose(position, rotation);
    }

    // Si el plano de apoyo sigue valido, la pose se acomoda a el; si no, se busca otro de vez en cuando.
    private Pose ApplySupportPlane(Pose pose)
    {
        if (supportPlane != null && supportPlane.subsumedBy != null)
            supportPlane = supportPlane.subsumedBy;

        if (supportPlane != null && supportPlane.trackingState != TrackingState.Tracking)
            supportPlane = null;

        if (supportPlane == null && Time.unscaledTime >= nextPlaneSearch)
        {
            nextPlaneSearch = Time.unscaledTime + 0.5f;
            supportPlane = FindSupportPlane(pose);
        }

        return supportPlane != null ? SnapToPlane(pose, supportPlane) : pose;
    }

    // ------------------------------------------------------------
    // Actualizacion
    // ------------------------------------------------------------

    private void Update()
    {
        if (!placed) return;

        if (board == null) // El tablero se destruyo (cambio de escena, etc.).
        {
            placed = false;
            listening = false;
            SetState(ARCanvasController.BoardState.None);
            return;
        }

        EvaluateState(force: false);
    }

    private void LateUpdate()
    {
        if (!placed || board == null || !realignWhileImageTracked) return;

        // La hoja no se ve en Tracking este frame: se reinicia el conteo para la proxima vez.
        if (!hasTarget || Time.frameCount - lastReportFrame > 1)
        {
            trackingSince = -1f;
            windowStart = -1f;
            return;
        }

        float now = Time.unscaledTime;

        // Espera a que la lectura de la hoja se estabilice.
        if (trackingSince < 0f || now - trackingSince < realignStableSeconds) return;

        // Una sola correccion por reaparicion: pasada la ventana, el tablero ignora la hoja.
        if (realignWindowSeconds > 0f)
        {
            Transform start = board.transform;

            if (windowStart < 0f)
            {
                windowStart = now;
                Debug.Log($"[BoardWorldAnchor] Realineando con la hoja: diferencia {Vector3.Distance(start.position, targetPose.position):F2} m, " +
                          $"{Quaternion.Angle(start.rotation, targetPose.rotation):F0} grados.");
            }

            if (now - windowStart > realignWindowSeconds) return;
        }

        Transform t = board.transform;
        float k = 1f - Mathf.Exp(-alignSpeed * Time.deltaTime);

        t.SetPositionAndRotation(
            Vector3.Lerp(t.position, targetPose.position, k),
            Quaternion.Slerp(t.rotation, targetPose.rotation, k));
    }

    // Estado segun el seguimiento del DISPOSITIVO (no de la imagen):
    //   Tracking -> sesion siguiendo bien; Limited -> sesion reinicializandose; Lost -> sin seguimiento.
    private void EvaluateState(bool force)
    {
        ARCanvasController.BoardState desired;

        switch (ARSession.state)
        {
            case ARSessionState.SessionTracking:
                desired = ARCanvasController.BoardState.Tracking;
                break;

            case ARSessionState.SessionInitializing:
                desired = ARCanvasController.BoardState.Limited;
                break;

            default:
                desired = ARCanvasController.BoardState.Lost;
                break;
        }

        if (force)
        {
            hasPendingState = false;
            SetState(desired);
            return;
        }

        if (desired == state)
        {
            hasPendingState = false; // Volvio al estado actual: el cambio breve se descarta.
            return;
        }

        if (!hasPendingState || pendingState != desired)
        {
            hasPendingState = true;
            pendingState = desired;
            pendingSince = Time.unscaledTime;
            return;
        }

        float delay = desired == ARCanvasController.BoardState.Lost ? lostDelay : stateChangeDelay;

        if (Time.unscaledTime - pendingSince >= delay)
        {
            hasPendingState = false;
            SetState(desired);
        }
    }

    private void SetState(ARCanvasController.BoardState newState)
    {
        Debug.Log($"[BoardWorldAnchor] Estado del tablero: {state} -> {newState} (ARSession.state = {ARSession.state})");
        state = newState;

        if (board != null)
        {
            bool visible = newState == ARCanvasController.BoardState.Tracking
                        || newState == ARCanvasController.BoardState.Limited;

            if (board.gameObject.activeSelf != visible)
            {
                Debug.Log($"[BoardWorldAnchor] Tablero {(visible ? "mostrado" : "ocultado")}.");
                board.gameObject.SetActive(visible);
            }

            if (visible) ShowBoardNow();
        }

        ARCanvasController.PublishState(newState);
    }
}