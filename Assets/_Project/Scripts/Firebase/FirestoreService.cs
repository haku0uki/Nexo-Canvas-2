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

    // Un listener de comentarios por post-it abierto, para poder detenerlo al cambiar o cerrar el detalle.
    private readonly Dictionary<string, ListenerRegistration> comentarioListeners = new Dictionary<string, ListenerRegistration>();

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    void Start()
    {
        // Esperar a que AuthManager inicialice Firebase antes de usar la base de datos
        AuthManager.Instance.OnFirebaseInitialized += InicializarFirestore;
    }

    private void InicializarFirestore()
    {
        db = FirebaseFirestore.DefaultInstance;
        postitsCollection = db.Collection("postits"); // Tu coleccion definitiva de produccion
        Debug.Log("<color=green>¡FirestoreService inicializado con éxito!</color>");
    }

    // 1. Escuchar los post-its en tiempo real (Suscripcion)
    public void EscucharPostIts(Action<List<PostItData>> OnPostItsUpdated)
    {
        if (postitsCollection == null) return;
        if (realtimeListener != null) return; // Ya suscripto; BoardCanvas puede llamar esto mas de una vez.

        // Abrir el canal de escucha en tiempo real
        realtimeListener = postitsCollection.Listen(snapshot =>
        {
            List<PostItData> listaPostIts = new List<PostItData>();
            foreach (DocumentSnapshot doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    PostItData data = new PostItData();
                    data.id = doc.Id;
                    data.authorId = doc.GetValue<string>("authorId");
                    data.tipo = doc.GetValue<string>("tipo");
                    data.contenido = doc.GetValue<string>("contenido");

                    // Extraer las coordenadas 3D locales
                    Dictionary<string, object> pos = doc.GetValue<Dictionary<string, object>>("posicionRelativa");
                    data.posX = Convert.ToSingle(pos["x"]);
                    data.posY = Convert.ToSingle(pos["y"]);
                    data.posZ = Convert.ToSingle(pos["z"]);

                    listaPostIts.Add(data);
                }
            }
            OnPostItsUpdated?.Invoke(listaPostIts); // Enviar datos al renderizador
        });
    }

    // 2. Publicar un nuevo post-it (Texto o Dibujo) con su posicion local
    public bool PublicarPostIt(string autorId, string tipo, string contenido, Vector3 posRelativa)
    {
        if (postitsCollection == null)
        {
            Debug.LogWarning("FirestoreService: Firestore todavía no está inicializado, no se publicó el post-it.");
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

    // 6. Obtener un post-it puntual por el id de su documento (una sola lectura, sin listener)
    public void ObtenerPostIt(string postItId, Action<PostItData> onResult)
    {
        if (postitsCollection == null)
        {
            Debug.LogWarning("FirestoreService: Firestore todavía no está inicializado.");
            onResult?.Invoke(null);
            return;
        }

        if (string.IsNullOrWhiteSpace(postItId))
        {
            Debug.LogWarning("FirestoreService: el id del post-it está vacío.");
            onResult?.Invoke(null);
            return;
        }

        postitsCollection.Document(postItId.Trim()).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("FirestoreService: error al leer el post-it: " + task.Exception);
                onResult?.Invoke(null);
                return;
            }

            DocumentSnapshot doc = task.Result;

            if (!doc.Exists)
            {
                Debug.LogWarning("FirestoreService: no existe un post-it con id '" + postItId + "'.");
                onResult?.Invoke(null);
                return;
            }

            PostItData data = new PostItData();
            data.id = doc.Id;

            string authorId, tipo, contenido;
            doc.TryGetValue("authorId", out authorId);
            doc.TryGetValue("tipo", out tipo);
            doc.TryGetValue("contenido", out contenido);

            data.authorId = authorId;
            data.tipo = tipo;
            data.contenido = contenido;

            Dictionary<string, object> pos;
            if (doc.TryGetValue("posicionRelativa", out pos) && pos != null)
            {
                if (pos.ContainsKey("x")) data.posX = Convert.ToSingle(pos["x"]);
                if (pos.ContainsKey("y")) data.posY = Convert.ToSingle(pos["y"]);
                if (pos.ContainsKey("z")) data.posZ = Convert.ToSingle(pos["z"]);
            }

            onResult?.Invoke(data);
        });
    }

    private void OnDestroy()
    {
        // Muy importante: detener la escucha al cerrar para ahorrar ancho de banda de tu plan Spark (sin costo)
        if (realtimeListener != null) realtimeListener.Stop();

        foreach (ListenerRegistration listener in comentarioListeners.Values) listener.Stop();
        comentarioListeners.Clear();
    }
}