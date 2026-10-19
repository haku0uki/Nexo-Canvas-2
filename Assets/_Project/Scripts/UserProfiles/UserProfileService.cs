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

    private FirebaseFirestore db;
    private CollectionReference users;
    private CollectionReference postits;
    private bool subscribedToAuth;

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
            Debug.LogError("UserProfileService: no se encontró AuthManager.");
            return;
        }

        if (auth.IsFirebaseInitialized)
            InitializeFirestore();
        else
        {
            auth.OnFirebaseInitialized += InitializeFirestore;
            subscribedToAuth = true;
        }

        // El evento de login se emite después de verificar el correo.
        auth.OnUserLoggedIn += HandleUserLoggedIn;

        // Cubre el caso en que este objeto se active cuando ya hay una sesión.
        if (auth.CurrentUser != null && auth.CurrentUser.IsEmailVerified)
            EnsureProfileExists(auth.CurrentUser, null);
    }

    private void InitializeFirestore()
    {
        if (db != null) return;

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

    private void HandleUserLoggedIn(Firebase.Auth.FirebaseUser user)
    {
        if (user != null && user.IsEmailVerified)
            EnsureProfileExists(user, null);
    }

    /// <summary>
    /// Crea el perfil solo si users/{uid} no existe. No sobrescribe perfiles existentes.
    /// </summary>
    public void EnsureProfileExists(Firebase.Auth.FirebaseUser user, Action<bool> onComplete)
    {
        if (user == null || !user.IsEmailVerified)
        {
            onComplete?.Invoke(false);
            return;
        }

        if (users == null) InitializeFirestore();
        if (users == null)
        {
            Debug.LogError("UserProfileService: Firestore todavía no está listo.");
            onComplete?.Invoke(false);
            return;
        }

        DocumentReference profileRef = users.Document(user.UserId);
        profileRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("UserProfileService: no se pudo consultar el perfil: " + task.Exception);
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

            profileRef.SetAsync(initialProfile).ContinueWithOnMainThread(saveTask =>
            {
                bool success = !saveTask.IsFaulted && !saveTask.IsCanceled;
                if (!success)
                    Debug.LogError("UserProfileService: no se pudo crear el perfil: " + saveTask.Exception);
                onComplete?.Invoke(success);
            });
        });
    }

    /// <summary>Lee un perfil por UID, independientemente de quién haya iniciado sesión.</summary>
    public void LoadProfileByUid(string uid, Action<UserProfileData> onComplete)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            onComplete?.Invoke(null);
            return;
        }

        if (users == null) InitializeFirestore();
        if (users == null)
        {
            onComplete?.Invoke(null);
            return;
        }

        users.Document(uid).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
            {
                if (task.IsFaulted)
                    Debug.LogError("UserProfileService: error al cargar perfil por UID: " + task.Exception);
                onComplete?.Invoke(null);
                return;
            }

            DocumentSnapshot doc = task.Result;
            onComplete?.Invoke(new UserProfileData
            {
                uid = uid,
                email = ReadString(doc, "email", string.Empty),
                nickname = ReadString(doc, "nickname", string.Empty),
                pronouns = ReadString(doc, "pronouns", string.Empty),
                drawingJson = ReadString(doc, "drawingJson", string.Empty)
            });
        });
    }

    public void LoadProfile(Firebase.Auth.FirebaseUser user, Action<UserProfileData> onComplete)
    {
        if (user == null)
        {
            onComplete?.Invoke(null);
            return;
        }

        if (users == null) InitializeFirestore();
        if (users == null)
        {
            onComplete?.Invoke(null);
            return;
        }

        users.Document(user.UserId).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
            {
                if (task.IsFaulted)
                    Debug.LogError("UserProfileService: error al cargar perfil: " + task.Exception);
                onComplete?.Invoke(null);
                return;
            }

            DocumentSnapshot doc = task.Result;
            onComplete?.Invoke(new UserProfileData
            {
                uid = user.UserId,
                email = ReadString(doc, "email", user.Email),
                nickname = ReadString(doc, "nickname", string.Empty),
                pronouns = ReadString(doc, "pronouns", string.Empty),
                drawingJson = ReadString(doc, "drawingJson", string.Empty)
            });
        });
    }

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

        if (users == null) InitializeFirestore();
        if (users == null)
        {
            onComplete?.Invoke(false);
            return;
        }

        var fields = new Dictionary<string, object>
        {
            { "email", user.Email ?? string.Empty },
            { "nickname", (nickname ?? string.Empty).Trim() },
            { "pronouns", (pronouns ?? string.Empty).Trim() },
            { "updatedAt", FieldValue.ServerTimestamp }
        };

        users.Document(user.UserId).SetAsync(fields, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                bool success = !task.IsFaulted && !task.IsCanceled;
                if (!success)
                    Debug.LogError("UserProfileService: error al guardar perfil: " + task.Exception);
                onComplete?.Invoke(success);
            });
    }

    /// <summary>
    /// Guarda el JSON de dibujo del perfil. Un JSON vacío representa un lienzo sin dibujo.
    /// </summary>
    public void SaveProfileDrawing(Firebase.Auth.FirebaseUser user, string drawingJson, Action<bool> onComplete)
    {
        if (user == null || !user.IsEmailVerified)
        {
            onComplete?.Invoke(false);
            return;
        }

        if (users == null) InitializeFirestore();
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

        users.Document(user.UserId).SetAsync(fields, SetOptions.MergeAll)
            .ContinueWithOnMainThread(task =>
            {
                bool success = !task.IsFaulted && !task.IsCanceled;
                if (!success)
                    Debug.LogError("UserProfileService: error al guardar dibujo de perfil: " + task.Exception);
                onComplete?.Invoke(success);
            });
    }

    /// <summary>
    /// Las publicaciones actuales guardan authorId como correo, no como UID.
    /// Por eso esta consulta filtra por el correo de Firebase Authentication.
    /// </summary>
    public void LoadUserPosts(string email, Action<List<PostItData>> onComplete)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            onComplete?.Invoke(new List<PostItData>());
            return;
        }

        if (postits == null) InitializeFirestore();
        if (postits == null)
        {
            onComplete?.Invoke(new List<PostItData>());
            return;
        }

        postits.WhereEqualTo("authorId", email).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError("UserProfileService: error al cargar publicaciones: " + task.Exception);
                onComplete?.Invoke(new List<PostItData>());
                return;
            }

            List<PostItData> posts = new List<PostItData>();
            foreach (DocumentSnapshot doc in task.Result.Documents)
            {
                if (!doc.Exists) continue;

                string tipo = doc.ContainsField("tipo") ? doc.GetValue<string>("tipo") : "texto";
                string contenido = doc.ContainsField("contenido") ? doc.GetValue<string>("contenido") : string.Empty;
                string authorId = doc.ContainsField("authorId") ? doc.GetValue<string>("authorId") : string.Empty;

                float x = 0f, y = 0f, z = 0f;
                if (doc.ContainsField("posicionRelativa"))
                {
                    Dictionary<string, object> pos = doc.GetValue<Dictionary<string, object>>("posicionRelativa");
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

    private static string ReadString(DocumentSnapshot doc, string key, string fallback)
    {
        return doc.ContainsField(key) && doc.GetValue<object>(key) != null
            ? doc.GetValue<string>(key)
            : fallback;
    }

    private static float ReadFloat(Dictionary<string, object> data, string key)
    {
        if (!data.TryGetValue(key, out object value) || value == null) return 0f;
        try { return Convert.ToSingle(value); }
        catch { return 0f; }
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

        if (Instance == this) Instance = null;
    }
}
