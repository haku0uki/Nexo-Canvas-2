using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using System;

public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }

    // Eventos para que UIManager y FirestoreService reaccionen al instante
    public event Action OnFirebaseInitialized;
    public event Action<FirebaseUser> OnUserLoggedIn;
    public event Action OnUserLoggedOut;

    private FirebaseAuth auth;
    public FirebaseUser CurrentUser => auth != null ? auth.CurrentUser : null;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    void Start()
    {
        InicializarFirebase();
    }

    private void InicializarFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
                auth.StateChanged += AuthStateChanged;

                OnFirebaseInitialized?.Invoke();
                Debug.Log("<color=green>¡Firebase Auth inicializado con éxito!</color>");

                VerificarSesionPersistente();
            }
            else
            {
                Debug.LogError($"Dependencias de Firebase no disponibles: {dependencyStatus}");
            }
        });
    }

    private void VerificarSesionPersistente()
    {
        // Solo autologuear si el correo es de la universidad y ya está confirmado
        if (CurrentUser != null && ValidarCorreoInstitucional(CurrentUser.Email))
        {
            if (CurrentUser.IsEmailVerified)
            {
                Debug.Log($"Sesión persistente recuperada: {CurrentUser.Email}");
                OnUserLoggedIn?.Invoke(CurrentUser);
            }
            else
            {
                SignOut();
            }
        }
    }

    // 1. REGISTRAR ESTUDIANTE (Con dominio institucional obligatorio)
    public void RegistrarEstudiante(string email, string password)
    {
        if (!ValidarCorreoInstitucional(email))
        {
            Debug.LogError("Registro rechazado: Debe usar su correo institucional @uandresbello.edu");
            return;
        }

        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task => {
            if (task.IsFaulted)
            {
                Debug.LogError("Error al registrar: " + task.Exception);
                return;
            }

            FirebaseUser user = task.Result.User;
            Debug.Log($"<color=green>¡Cuenta creada! Enviando verificación a {user.Email}</color>");

            // Enviar correo de verificación automático al Outlook del alumno
            user.SendEmailVerificationAsync().ContinueWithOnMainThread(verifyTask => {
                Debug.Log("Correo de verificación enviado. Revisa tu bandeja de entrada de la universidad.");
                SignOut(); // Forzar cierre de sesión hasta que haga clic en el link
            });
        });
    }

    // 2. INICIAR SESIÓN (Y verificar que ya haya confirmado su correo)
    public void IniciarSesion(string email, string password)
    {
        if (!ValidarCorreoInstitucional(email))
        {
            Debug.LogError("Login rechazado: El correo no pertenece a la universidad.");
            return;
        }

        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task => {
            if (task.IsFaulted)
            {
                Debug.LogError("Error al iniciar sesión: " + task.Exception);
                return;
            }

            FirebaseUser user = task.Result.User;

            // Bloquear el paso si no ha verificado su cuenta desde su Outlook
            if (user.IsEmailVerified)
            {
                Debug.Log($"<color=green>Sesión iniciada con éxito: {user.Email}</color>");
                OnUserLoggedIn?.Invoke(user);
            }
            else
            {
                Debug.LogWarning("Por favor, verifica tu correo en tu Outlook antes de ingresar.");
                SignOut();
            }
        });
    }

    public void SignOut()
    {
        if (auth != null)
        {
            auth.SignOut();
            OnUserLoggedOut?.Invoke();
        }
    }

    private bool ValidarCorreoInstitucional(string email)
    {
        return !string.IsNullOrEmpty(email) && email.EndsWith("@uandresbello.edu", StringComparison.OrdinalIgnoreCase);
    }

    private void AuthStateChanged(object sender, EventArgs eventArgs)
    {
        if (auth.CurrentUser == null)
        {
            OnUserLoggedOut?.Invoke();
        }
    }

    private void OnDestroy()
    {
        if (auth != null) auth.StateChanged -= AuthStateChanged;
    }
}
