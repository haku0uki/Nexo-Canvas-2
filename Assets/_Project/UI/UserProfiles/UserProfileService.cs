using System;
using System.Collections.Generic;
using Firebase.Firestore;
using Firebase.Extensions;
using UnityEngine;

/// <summary>
/// Gestiona el documento users/{uid} y recupera las publicaciones del usuario.
/// No modifica AuthManager ni FirestoreService.
/// </summary>
public class UserProfileService : MonoBehaviour
{
    public static UserProfileService Instance { get; private set; }
    public event Action<string> ProfileUpdated;

    private FirebaseFirestore db;
    private CollectionReference users;
    private CollectionReference postits;
    private bool subscribedToAuth;

    // Nombre a mostrar por correo (nickname, o la parte local del correo si no hay).
    // Los post-its y comentarios guardan el correo como autor; sin cache habria una
    // consulta por cada post-it cada vez que Firestore reporta un cambio.
    private readonly Dictionary<string, string> displayNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // Dibujo de perfil (JSON) por correo, para mostrarlo junto al autor. Cadena vacia = sin dibujo.
    private readonly Dictionary<string, string> avatarDrawings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, List<Action<string>>> pendingAvatars =
        new Dictionary<string, List<Action<string>>>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, List<Action<string>>> pendingDisplayNames =
        new Dictionary<string, List<Action<string>>>(StringComparer.OrdinalIgnoreCase);

    [Serializable]
    public class UserProfileData
    {
        public string uid;
        public string email;
        public string nickname;
        public string pronouns;
        public string drawingJson;
    }

    public bool IsReady => db != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        AuthManager auth = AuthManager.Instance;

        if (auth == null)
        {
            Debug.LogError(
                "UserProfileService: no se encontró AuthManager."
            );
            return;
        }

        if (auth.IsFirebaseInitialized)
        {
            InitializeFirestore();
        }
        else
        {
            auth.OnFirebaseInitialized += InitializeFirestore;
            subscribedToAuth = true;
        }

        auth.OnUserLoggedIn += HandleUserLoggedIn;

        // Si el servicio se activa con una sesión ya iniciada.
        if (auth.CurrentUser != null &&
            auth.CurrentUser.IsEmailVerified)
        {
            EnsureProfileExists(auth.CurrentUser, null);
        }
    }

    private void InitializeFirestore()
    {
        if (db != null)
            return;

        AuthManager auth = AuthManager.Instance;

        if (subscribedToAuth && auth != null)
        {
            auth.OnFirebaseInitialized -= InitializeFirestore;
            subscribedToAuth = false;
        }

        db = FirebaseFirestore.DefaultInstance;
        users = db.Collection("users");
        postits = db.Collection("postits");
    }

    private void HandleUserLoggedIn(
        Firebase.Auth.FirebaseUser user)
    {
        if (user != null && user.IsEmailVerified)
            EnsureProfileExists(user, null);
    }

    /// <summary>
    /// Crea users/{uid} solo si todavía no existe.
    /// No sobrescribe perfiles existentes.
    /// </summary>
    public void EnsureProfileExists(
        Firebase.Auth.FirebaseUser user,
        Action<bool> onComplete)
    {
        if (user == null || !user.IsEmailVerified)
        {
            onComplete?.Invoke(false);
            return;
        }

        if (users == null)
            InitializeFirestore();

        if (users == null)
        {
            Debug.LogError(
                "UserProfileService: Firestore todavía no está listo."
            );
            onComplete?.Invoke(false);
            return;
        }

        DocumentReference profileRef = users.Document(user.UserId);

        profileRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError(
                    "UserProfileService: no se pudo consultar el perfil: "
                    + task.Exception
                );

                onComplete?.Invoke(false);
                return;
            }

            if (task.Result.Exists)
            {
                onComplete?.Invoke(true);
                return;
            }

            var initialProfile = new Dictionary<string, object>
            {
                { "uid", user.UserId },
                { "email", user.Email ?? string.Empty },
                { "nickname", string.Empty },
                { "pronouns", string.Empty },
                { "drawingJson", string.Empty },
                { "createdAt", FieldValue.ServerTimestamp },
                { "updatedAt", FieldValue.ServerTimestamp }
            };

            profileRef.SetAsync(initialProfile)
                .ContinueWithOnMainThread(saveTask =>
                {
                    bool success =
                        !saveTask.IsFaulted && !saveTask.IsCanceled;

                    if (!success)
                    {
                        Debug.LogError(
                            "UserProfileService: no se pudo crear el perfil: "
                            + saveTask.Exception
                        );
                    }

                    onComplete?.Invoke(success);
                });
        });
    }

    /// <summary>
    /// Lee un perfil por UID, independientemente de quién inició sesión.
    /// </summary>
    public void LoadProfileByUid(
        string uid,
        Action<UserProfileData> onComplete)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            onComplete?.Invoke(null);
            return;
        }

        if (users == null)
            InitializeFirestore();

        if (users == null)
        {
            onComplete?.Invoke(null);
            return;
        }

        users.Document(uid).GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted ||
                    task.IsCanceled ||
                    !task.Result.Exists)
                {
                    if (task.IsFaulted)
                    {
                        Debug.LogError(
                            "UserProfileService: error al cargar perfil por UID: "
                            + task.Exception
                        );
                    }

                    onComplete?.Invoke(null);
                    return;
                }

                DocumentSnapshot doc = task.Result;

                onComplete?.Invoke(new UserProfileData
                {
                    uid = uid,
                    email = ReadString(
                        doc, "email", string.Empty
                    ),
                    nickname = ReadString(
                        doc, "nickname", string.Empty
                    ),
                    pronouns = ReadString(
                        doc, "pronouns", string.Empty
                    ),
                    drawingJson = ReadString(
                        doc, "drawingJson", string.Empty
                    )
                });
            });
    }

    /// <summary>Nombre de respaldo cuando no hay nickname: la parte del correo antes de la arroba.</summary>
    public static string FallbackName(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;

        int at = email.IndexOf('@');
        return at > 0 ? email.Substring(0, at) : email;
    }

    /// <summary>Devuelve el nombre si ya esta en cache, sin consultar Firestore.</summary>
    public bool TryGetDisplayName(string email, out string name)
    {
        name = null;
        return !string.IsNullOrWhiteSpace(email) && displayNames.TryGetValue(email, out name);
    }

    /// <summary>
    /// Entrega el nickname del usuario con ese correo (o el nombre de respaldo).
    /// Si ya esta en cache, responde en el mismo instante; si no, consulta una sola vez
    /// aunque varios pidan el mismo correo a la vez.
    /// </summary>
    public void ResolveDisplayName(string email, Action<string> onResolved)
    {
        if (onResolved == null) return;

        if (string.IsNullOrWhiteSpace(email))
        {
            onResolved(string.Empty);
            return;
        }

        if (displayNames.TryGetValue(email, out string cached))
        {
            onResolved(cached);
            return;
        }

        if (pendingDisplayNames.TryGetValue(email, out List<Action<string>> waiting))
        {
            waiting.Add(onResolved);
            return;
        }

        pendingDisplayNames[email] = new List<Action<string>> { onResolved };

        LoadProfileByEmail(email, profile =>
        {
            string name = profile != null && !string.IsNullOrWhiteSpace(profile.nickname)
                ? profile.nickname.Trim()
                : FallbackName(email);

            displayNames[email] = name;
            avatarDrawings[email] = profile?.drawingJson ?? string.Empty;

            List<Action<string>> callbacks = pendingDisplayNames[email];
            pendingDisplayNames.Remove(email);

            foreach (Action<string> callback in callbacks)
                callback(name);
        });
    }

    /// <summary>Devuelve el dibujo de perfil si ya esta en cache (cadena vacia = sin dibujo).</summary>
    public bool TryGetAvatarDrawing(string email, out string drawingJson)
    {
        drawingJson = null;
        return !string.IsNullOrWhiteSpace(email) && avatarDrawings.TryGetValue(email, out drawingJson);
    }

    /// <summary>
    /// Entrega el JSON del dibujo de perfil del usuario con ese correo. Mismo esquema que
    /// ResolveDisplayName: cache, y una sola consulta aunque muchos lo pidan a la vez.
    /// </summary>
    public void ResolveAvatarDrawing(string email, Action<string> onResolved)
    {
        if (onResolved == null) return;

        if (string.IsNullOrWhiteSpace(email))
        {
            onResolved(string.Empty);
            return;
        }

        if (avatarDrawings.TryGetValue(email, out string cached))
        {
            onResolved(cached);
            return;
        }

        if (pendingAvatars.TryGetValue(email, out List<Action<string>> waiting))
        {
            waiting.Add(onResolved);
            return;
        }

        pendingAvatars[email] = new List<Action<string>> { onResolved };

        LoadProfileByEmail(email, profile =>
        {
            string json = profile?.drawingJson ?? string.Empty;

            avatarDrawings[email] = json;

            if (profile != null && !displayNames.ContainsKey(email))
            {
                displayNames[email] = !string.IsNullOrWhiteSpace(profile.nickname)
                    ? profile.nickname.Trim()
                    : FallbackName(email);
            }

            List<Action<string>> callbacks = pendingAvatars[email];
            pendingAvatars.Remove(email);

            foreach (Action<string> callback in callbacks)
                callback(json);
        });
    }

    /// <summary>
    /// Lee un perfil por correo. Las publicaciones guardan authorId como correo,
    /// asi que es la forma de llegar al perfil del autor de un post-it.
    /// Entrega null si no existe un documento users con ese correo.
    /// </summary>
    public void LoadProfileByEmail(
        string email,
        Action<UserProfileData> onComplete)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            onComplete?.Invoke(null);
            return;
        }

        if (users == null)
            InitializeFirestore();

        if (users == null)
        {
            onComplete?.Invoke(null);
            return;
        }

        users.WhereEqualTo("email", email).Limit(1).GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError(
                        "UserProfileService: error al cargar perfil por correo: "
                        + task.Exception
                    );

                    onComplete?.Invoke(null);
                    return;
                }

                DocumentSnapshot doc = null;

                foreach (DocumentSnapshot candidate in task.Result.Documents)
                {
                    if (candidate.Exists)
                    {
                        doc = candidate;
                        break;
                    }
                }

                if (doc == null)
                {
                    onComplete?.Invoke(null);
                    return;
                }

                onComplete?.Invoke(new UserProfileData
                {
                    uid = doc.Id,
                    email = ReadString(doc, "email", email),
                    nickname = ReadString(doc, "nickname", string.Empty),
                    pronouns = ReadString(doc, "pronouns", string.Empty),
                    drawingJson = ReadString(doc, "drawingJson", string.Empty)
                });
            });
    }

    /// <summary>Lee el perfil del usuario autenticado.</summary>
    public void LoadProfile(
        Firebase.Auth.FirebaseUser user,
        Action<UserProfileData> onComplete)
    {
        if (user == null)
        {
            onComplete?.Invoke(null);
            return;
        }

        if (users == null)
            InitializeFirestore();

        if (users == null)
        {
            onComplete?.Invoke(null);
            return;
        }

        users.Document(user.UserId).GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted ||
                    task.IsCanceled ||
                    !task.Result.Exists)
                {
                    if (task.IsFaulted)
                    {
                        Debug.LogError(
                            "UserProfileService: error al cargar perfil: "
                            + task.Exception
                        );
                    }

                    onComplete?.Invoke(null);
                    return;
                }

                DocumentSnapshot doc = task.Result;

                onComplete?.Invoke(new UserProfileData
                {
                    uid = user.UserId,
                    email = ReadString(
                        doc, "email", user.Email
                    ),
                    nickname = ReadString(
                        doc, "nickname", string.Empty
                    ),
                    pronouns = ReadString(
                        doc, "pronouns", string.Empty
                    ),
                    drawingJson = ReadString(
                        doc, "drawingJson", string.Empty
                    )
                });
            });
    }

    /// <summary>
    /// Guarda nickname y pronombres del usuario autenticado.
    /// Un valor null significa "no cambiar ese campo": no se escribe en Firestore.
    /// </summary>
    public void SaveProfile(
        Firebase.Auth.FirebaseUser user,
        string nickname,
        string pronouns,
        Action<bool> onComplete)
    {
        if (user == null || !user.IsEmailVerified)
        {
            onComplete?.Invoke(false);
            return;
        }

        if (users == null)
            InitializeFirestore();

        if (users == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        var fields = new Dictionary<string, object>
        {
            { "email", user.Email ?? string.Empty },
            { "updatedAt", FieldValue.ServerTimestamp }
        };

        if (nickname != null)
            fields["nickname"] = nickname.Trim();

        if (pronouns != null)
            fields["pronouns"] = pronouns.Trim();

        users.Document(user.UserId)
            .SetAsync(fields, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                bool success =
                    !task.IsFaulted && !task.IsCanceled;

                if (!success)
                {
                    Debug.LogError(
                        "UserProfileService: error al guardar perfil: "
                        + task.Exception
                    );
                }
                else if (nickname != null)
                {
                    // Los post-its nuevos ya muestran el nickname recien guardado.
                    string trimmed = nickname.Trim();
                    displayNames[user.Email ?? string.Empty] =
                        trimmed.Length > 0 ? trimmed : FallbackName(user.Email);
                }

                if (success && !string.IsNullOrWhiteSpace(user.Email))
                    ProfileUpdated?.Invoke(user.Email);

                onComplete?.Invoke(success);
            });
    }

    /// <summary>
    /// Guarda el JSON del dibujo del perfil.
    /// Un JSON vacío representa un lienzo sin dibujo.
    /// </summary>
    public void SaveProfileDrawing(
        Firebase.Auth.FirebaseUser user,
        string drawingJson,
        Action<bool> onComplete)
    {
        if (user == null || !user.IsEmailVerified)
        {
            onComplete?.Invoke(false);
            return;
        }

        if (users == null)
            InitializeFirestore();

        if (users == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        var fields = new Dictionary<string, object>
        {
            { "drawingJson", drawingJson ?? string.Empty },
            { "updatedAt", FieldValue.ServerTimestamp }
        };

        users.Document(user.UserId)
            .SetAsync(fields, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                bool success =
                    !task.IsFaulted && !task.IsCanceled;

                if (!success)
                {
                    Debug.LogError(
                        "UserProfileService: error al guardar dibujo de perfil: "
                        + task.Exception
                    );
                }

                if (success && !string.IsNullOrEmpty(user.Email))
                {
                    avatarDrawings[user.Email] = drawingJson ?? string.Empty;
                    ProfileUpdated?.Invoke(user.Email);
                }

                onComplete?.Invoke(success);
            });
    }

    /// <summary>
    /// Las publicaciones actuales guardan authorId como correo.
    /// Por eso se filtran usando el correo de Firebase Authentication.
    /// </summary>
    public void LoadUserPosts(
        string email,
        Action<List<PostItData>> onComplete)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            onComplete?.Invoke(new List<PostItData>());
            return;
        }

        if (postits == null)
            InitializeFirestore();

        if (postits == null)
        {
            onComplete?.Invoke(new List<PostItData>());
            return;
        }

        postits.WhereEqualTo("authorId", email)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError(
                        "UserProfileService: error al cargar publicaciones: "
                        + task.Exception
                    );

                    onComplete?.Invoke(new List<PostItData>());
                    return;
                }

                List<PostItData> posts =
                    new List<PostItData>();

                foreach (DocumentSnapshot doc in task.Result.Documents)
                {
                    if (!doc.Exists)
                        continue;

                    string tipo = doc.ContainsField("tipo")
                        ? doc.GetValue<string>("tipo")
                        : "texto";

                    string contenido = doc.ContainsField("contenido")
                        ? doc.GetValue<string>("contenido")
                        : string.Empty;

                    string authorId = doc.ContainsField("authorId")
                        ? doc.GetValue<string>("authorId")
                        : string.Empty;

                    float x = 0f;
                    float y = 0f;
                    float z = 0f;

                    if (doc.ContainsField("posicionRelativa"))
                    {
                        Dictionary<string, object> pos =
                            doc.GetValue<Dictionary<string, object>>(
                                "posicionRelativa"
                            );

                        if (pos != null)
                        {
                            x = ReadFloat(pos, "x");
                            y = ReadFloat(pos, "y");
                            z = ReadFloat(pos, "z");
                        }
                    }

                    posts.Add(new PostItData
                    {
                        id = doc.Id,
                        authorId = authorId,
                        tipo = tipo,
                        contenido = contenido,
                        posX = x,
                        posY = y,
                        posZ = z
                    });
                }

                onComplete?.Invoke(posts);
            });
    }

    private static string ReadString(
        DocumentSnapshot doc,
        string key,
        string fallback)
    {
        return doc.ContainsField(key) &&
               doc.GetValue<object>(key) != null
            ? doc.GetValue<string>(key)
            : fallback;
    }

    private static float ReadFloat(
        Dictionary<string, object> data,
        string key)
    {
        if (!data.TryGetValue(key, out object value) ||
            value == null)
        {
            return 0f;
        }

        try
        {
            return Convert.ToSingle(value);
        }
        catch
        {
            return 0f;
        }
    }

    private void OnDestroy()
    {
        AuthManager auth = AuthManager.Instance;

        if (auth != null)
        {
            auth.OnUserLoggedIn -= HandleUserLoggedIn;

            if (subscribedToAuth)
                auth.OnFirebaseInitialized -= InitializeFirestore;
        }

        if (Instance == this)
            Instance = null;
    }
}