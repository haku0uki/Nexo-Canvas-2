using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using System;
using UnityEngine.SceneManagement;
using SheepyCreations.UIControl.Runtime;

public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }

    [Header("Scene Settings")]
    [SerializeField] private string mainScene = "NexoCanvasScene";

    // Eventos para que UIManager y FirestoreService reaccionen al instante
    public event Action OnFirebaseInitialized;
    public event Action<FirebaseUser> OnUserLoggedIn;
    public event Action OnUserLoggedOut;

    private FirebaseAuth auth;
    private NotificationBehaviour notification;


    public FirebaseUser CurrentUser => auth != null ? auth.CurrentUser : null;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    void Start()
    {
        notification = FindFirstObjectByType<NotificationBehaviour>();

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

                //VerificarSesionPersistente();
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

            if (notification != null)
            {
                notification.Enqueue("Debes usar tu correo @uandresbello.edu");
            }

            return;
        }

        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task => {
            if (task.IsFaulted)
            {
                // Interceptar el error específico de Firebase para evitar la alerta roja en consola
                var firebaseEx = task.Exception?.Flatten().InnerException as FirebaseException;
                if (firebaseEx != null)
                {
                    AuthError errorCode = (AuthError)firebaseEx.ErrorCode;
                    if (errorCode == AuthError.EmailAlreadyInUse)
                    {
                        Debug.LogWarning("<b>[Aviso]</b> Este correo ya está registrado. Por favor, selecciona la opción de iniciar sesión.");

                        if (notification != null)
                        {
                            notification.Enqueue("Este correo ya está registrado. Inicia sesión");
                        }

                        return;
                    }
                }

                Debug.LogError("Error inesperado al registrar: " + task.Exception);

                if (notification != null)
                {
                    notification.Enqueue("Error inesperado al registrar: " + task.Exception);
                }

                return;
            }

            FirebaseUser user = task.Result.User;
            Debug.Log($"<color=green>¡Cuenta creada! Enviando verificación a {user.Email}</color>");

            user.SendEmailVerificationAsync().ContinueWithOnMainThread(verifyTask => {
                Debug.Log("Correo de verificación enviado. Revisa tu bandeja de entrada de la universidad.");

                if (notification != null)
                {
                    notification.Enqueue("Correo de verificación enviado a " + user.Email + ". Revisa tu bandeja de entrada.");
                }


                SignOut();
            });
        });
    }

    // 2. INICIAR SESIÓN (Y verificar que ya haya confirmado su correo)
    public void IniciarSesion(string email, string password)
    {
        if (!ValidarCorreoInstitucional(email))
        {
            Debug.LogError("Login rechazado: El correo no pertenece a la universidad.");

            if (notification != null)
            {
                notification.Enqueue("Usa tu correo @uandresbello.edu");
            }

            return;
        }

        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task => {
            if (task.IsFaulted)
            {
                Debug.LogError("Error al iniciar sesión: " + task.Exception);

                if (notification != null)
                {
                    notification.Enqueue("Error al iniciar sesión: " + task.Exception);
                }

                return;
            }

            FirebaseUser user = task.Result.User;

            // Bloquear el paso si no ha verificado su cuenta desde su Outlook
            if (user.IsEmailVerified)
            {
                Debug.Log($"<color=green>Sesión iniciada con éxito: {user.Email}</color>");

                if (notification != null)
                {
                    notification.Enqueue("¡Sesión iniciada con éxito!");
                }

                OnUserLoggedIn?.Invoke(user);

                LoadMainScene();
            }
            else
            {
                Debug.LogWarning("Por favor, verifica tu correo en tu Outlook antes de ingresar.");

                if (notification != null)
                {
                    notification.Enqueue("Por favor, verifica tu correo antes de ingresar.");
                }

                SignOut();
            }
        });
    }

    public void ReenviarCorreoVerificacion(string email, string password)
    {
        if (!ValidarCorreoInstitucional(email))
        {
            Debug.LogError("Operación rechazada: El correo no pertenece a la universidad.");

            if (notification != null)
            {
                notification.Enqueue("Por favor, verifica tu correo antes de ingresar.");
            }

            return;
        }

        Debug.Log("Iniciando sesión silenciosa para reenviar correo de verificación...");
        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task => {
            if (task.IsFaulted)
            {
                Debug.LogError("No se pudo iniciar sesión para reenviar el correo: " + task.Exception);
                return;
            }

            FirebaseUser user = task.Result.User;

            // Enviar el correo de verificación nuevamente
            user.SendEmailVerificationAsync().ContinueWithOnMainThread(verifyTask => {
                if (verifyTask.IsCompletedSuccessfully)
                {
                    Debug.Log("<color=yellow><b>[Auth]</b> Enlace de verificación reenviado con éxito a tu Outlook.</color>");
                }
                else
                {
                    Debug.LogError("Error al reenviar el correo de verificación: " + verifyTask.Exception);
                }
                SignOut(); // Cerrar sesión inmediatamente por seguridad
            });
        });
    }

    private void LoadMainScene()
    {
        if (!string.IsNullOrEmpty(mainScene))
        {
            SceneManager.LoadScene(mainScene);
        }
        else
        {
            Debug.LogError("El nombre de la escena principal está vacío en el AuthManager.");
        }
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
