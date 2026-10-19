using UnityEngine;
using Firebase.Firestore;
using Firebase.Extensions;
using System;
using System.Collections.Generic;

// Estructura limpia para organizar tus post-its en Unity
[System.Serializable]
public class PostItData
{
    public string id;
    public string authorId;
    public string tipo; // "texto" o "dibujo"
    public string contenido; // El texto o los vectores del dibujo
    public float posX, posY, posZ; // Coordenadas locales relativas
}

public class FirestoreService : MonoBehaviour
{
    public static FirestoreService Instance { get; private set; }

    private FirebaseFirestore db;
    private CollectionReference postitsCollection;
    private ListenerRegistration realtimeListener;

    // True cuando Firestore ya está inicializado y se puede leer/escribir.
    public bool IsReady => postitsCollection != null;

    // Si BoardCanvas pide escuchar antes de que Firestore esté listo, se guarda
    // el callback y se abre el listener apenas termine la inicialización.
    private Action<List<PostItData>> pendingPostItsCallback;
    private bool subscribedToAuth;

    // Un listener de comentarios por post-it abierto, para poder detenerlo al cambiar o cerrar el detalle.
    private readonly Dictionary<string, ListenerRegistration> comentarioListeners = new Dictionary<string, ListenerRegistration>();

    private void Awake()
    {
        Debug.Log("[FirestoreService] Awake");
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    void Start()
    {
        AuthManager auth = AuthManager.Instance;
        Debug.Log("[FirestoreService] Start: authManager=" + (auth != null) + ", firebaseListo=" + (auth != null && auth.IsFirebaseInitialized));
        if (auth == null)
        {
            Debug.LogError("FirestoreService: no existe AuthManager. Inicia desde la escena de login (AuthManager persiste entre escenas).");
            return;
        }

        // Si Firebase ya se inicializó (AuthManager vive en la escena de login), el evento
        // OnFirebaseInitialized ya pasó y no volverá a dispararse: se inicializa directamente.
        if (auth.IsFirebaseInitialized)
        {
            InicializarFirestore();
            return;
        }

        auth.OnFirebaseInitialized += InicializarFirestore;
        subscribedToAuth = true;
    }

    private void InicializarFirestore()
    {
        if (postitsCollection != null) return; // Ya inicializado.

        if (subscribedToAuth && AuthManager.Instance != null)
        {
            AuthManager.Instance.OnFirebaseInitialized -= InicializarFirestore;
            subscribedToAuth = false;
        }

        db = FirebaseFirestore.DefaultInstance;
        postitsCollection = db.Collection("postits"); // Tu coleccion definitiva de produccion
        Debug.Log("<color=green>¡FirestoreService inicializado con éxito!</color>");

        if (pendingPostItsCallback != null)
        {
            Action<List<PostItData>> callback = pendingPostItsCallback;
            pendingPostItsCallback = null;
            EscucharPostIts(callback);
        }
    }

    // 1. Escuchar los post-its en tiempo real (Suscripcion)
    public void EscucharPostIts(Action<List<PostItData>> OnPostItsUpdated)
    {
        if (realtimeListener != null) return; // Ya suscripto; BoardCanvas puede llamar esto mas de una vez.

        if (postitsCollection == null)
        {
            // Firestore aun no esta listo: se recuerda el pedido y se abre el listener al inicializar.
            Debug.LogWarning("FirestoreService: Firestore aun no esta listo; la escucha de post-its comenzara al inicializar.");
            pendingPostItsCallback = OnPostItsUpdated;
            return;
        }

        // Abrir el canal de escucha en tiempo real
        realtimeListener = postitsCollection.Listen(snapshot =>
        {
            List<PostItData> listaPostIts = new List<PostItData>();
            foreach (DocumentSnapshot doc in snapshot.Documents)
            {
                if (!doc.Exists) continue;

                // Un documento mal formado no debe impedir que se vean los demas.
                try { listaPostIts.Add(LeerPostIt(doc)); }
                catch (Exception e) { Debug.LogWarning("FirestoreService: post-it '" + doc.Id + "' ignorado: " + e.Message); }
            }

            Debug.Log("FirestoreService: " + listaPostIts.Count + " post-its recibidos.");
            OnPostItsUpdated?.Invoke(listaPostIts); // Enviar datos al renderizador
        });
    }

    // 2. Publicar un nuevo post-it (Texto o Dibujo) con su posicion local
    // Devuelve false si Firestore aun no esta listo, para que quien llama conserve lo que iba a publicar.
    public bool PublicarPostIt(string autorId, string tipo, string contenido, Vector3 posRelativa)
    {
        if (postitsCollection == null)
        {
            Debug.LogWarning("FirestoreService: Firestore aun no esta inicializado; el post-it no se publico.");
            return false;
        }

        // Ningun post-it (texto o dibujo) se publica sin una sesion iniciada.
        if (AuthManager.Instance == null || AuthManager.Instance.CurrentUser == null)
        {
            Debug.LogWarning("FirestoreService: no hay sesion iniciada; el post-it no se publico.");
            return false;
        }

        var datosPostit = new Dictionary<string, object> {
            { "authorId", autorId },
            { "tipo", tipo },
            { "contenido", contenido },
            { "posicionRelativa", new Dictionary<string, object> {
                { "x", posRelativa.x },
                { "y", posRelativa.y },
                { "z", posRelativa.z }
            }},
            { "timestamp", FieldValue.ServerTimestamp }
        };

        postitsCollection.AddAsync(datosPostit).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
                Debug.LogError("FirestoreService: error al publicar el post-it: " + task.Exception);
        });

        return true;
    }

    // 2b. Leer un post-it puntual por id, una sola vez y sin listener. Entrega null si no existe o falla.
    public void ObtenerPostIt(string postItId, Action<PostItData> onResultado)
    {
        if (postitsCollection == null || string.IsNullOrEmpty(postItId))
        {
            onResultado?.Invoke(null);
            return;
        }

        postitsCollection.Document(postItId).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
            {
                onResultado?.Invoke(null);
                return;
            }

            onResultado?.Invoke(LeerPostIt(task.Result));
        });
    }

    private static PostItData LeerPostIt(DocumentSnapshot doc)
    {
        PostItData data = new PostItData();
        data.id = doc.Id;

        string valor;
        data.authorId = doc.TryGetValue("authorId", out valor) ? valor : string.Empty;
        data.tipo = doc.TryGetValue("tipo", out valor) ? valor : "texto";
        data.contenido = doc.TryGetValue("contenido", out valor) ? valor : string.Empty;

        // Extraer las coordenadas 3D locales (0 si faltan)
        Dictionary<string, object> pos;
        if (doc.TryGetValue("posicionRelativa", out pos) && pos != null)
        {
            data.posX = LeerFloat(pos, "x");
            data.posY = LeerFloat(pos, "y");
            data.posZ = LeerFloat(pos, "z");
        }

        return data;
    }

    private static float LeerFloat(Dictionary<string, object> dic, string clave)
    {
        return dic.TryGetValue(clave, out object v) && v != null ? Convert.ToSingle(v) : 0f;
    }

    // 3. Mover un post-it existente (Solo actualiza el campo de coordenadas en la nube)
    public void ActualizarPosicion(string postitId, Vector3 nuevaPosRelativa)
    {
        if (postitsCollection == null) return;

        DocumentReference docRef = postitsCollection.Document(postitId);
        var datosActualizar = new Dictionary<string, object> {
            { "posicionRelativa", new Dictionary<string, object> {
                { "x", nuevaPosRelativa.x },
                { "y", nuevaPosRelativa.y },
                { "z", nuevaPosRelativa.z }
            }}
        };

        docRef.UpdateAsync(datosActualizar);
    }

    // 4. Escuchar los comentarios de un post-it puntual (subcoleccion postits/{id}/comentarios)
    public void EscucharComentarios(string postItId, Action<List<CommentData>> onComentariosActualizados)
    {
        if (postitsCollection == null) return;

        DejarDeEscucharComentarios(postItId); // Evita apilar listeners si se vuelve a abrir el mismo post-it.

        CollectionReference coleccionComentarios = postitsCollection.Document(postItId).Collection("comentarios");

        ListenerRegistration listener = coleccionComentarios.Listen(snapshot =>
        {
            List<CommentData> lista = new List<CommentData>();
            foreach (DocumentSnapshot doc in snapshot.Documents)
            {
                if (!doc.Exists) continue;

                lista.Add(new CommentData
                {
                    id = doc.Id,
                    postItId = postItId,
                    authorId = doc.GetValue<string>("authorId"),
                    contenido = doc.GetValue<string>("contenido")
                });
            }
            onComentariosActualizados?.Invoke(lista);
        });

        comentarioListeners[postItId] = listener;
    }

    // Llamar al cerrar PostItDetailPanel (o al abrir otro post-it), para no dejar listeners de Firestore huerfanos.
    public void DejarDeEscucharComentarios(string postItId)
    {
        if (comentarioListeners.TryGetValue(postItId, out ListenerRegistration listener))
        {
            listener.Stop();
            comentarioListeners.Remove(postItId);
        }
    }

    // 5. Publicar un comentario nuevo dentro de un post-it puntual
    public void PublicarComentario(string postItId, string autorId, string contenido)
    {
        if (postitsCollection == null) return;

        CollectionReference coleccionComentarios = postitsCollection.Document(postItId).Collection("comentarios");

        var datosComentario = new Dictionary<string, object> {
            { "authorId", autorId },
            { "contenido", contenido },
            { "timestamp", FieldValue.ServerTimestamp }
        };

        coleccionComentarios.AddAsync(datosComentario);
    }

    private void OnDestroy()
    {
        if (subscribedToAuth && AuthManager.Instance != null)
            AuthManager.Instance.OnFirebaseInitialized -= InicializarFirestore;

        // Muy importante: detener la escucha al cerrar para ahorrar ancho de banda de tu plan Spark (sin costo)
        if (realtimeListener != null) realtimeListener.Stop();

        foreach (ListenerRegistration listener in comentarioListeners.Values) listener.Stop();
        comentarioListeners.Clear();
    }
}