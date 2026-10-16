using UnityEngine;

public class FirebaseSimpleTester : MonoBehaviour
{
    [Header("Datos de Prueba (Configura en el Inspector)")]
    public string emailPrueba = "tu_correo@uandresbello.edu";
    public string passwordPrueba = "nexocanvas123";

    void Start()
    {
        // Suscribirse a los eventos de AuthManager para rastrear el flujo en consola
        AuthManager.Instance.OnFirebaseInitialized += OnFirebaseInitialized;
        AuthManager.Instance.OnUserLoggedIn += OnUserLoggedIn;
        AuthManager.Instance.OnUserLoggedOut += OnUserLoggedOut;
    }

    private void OnFirebaseInitialized()
    {
        Debug.Log("<color=cyan><b>[Tester]</b> Firebase está listo. Haz clic derecho sobre este componente para ver las opciones de pruebas.</color>");
    }

    private void OnUserLoggedIn(Firebase.Auth.FirebaseUser user)
    {
        Debug.Log($"<color=green><b>[Tester]</b> ¡Éxito de Login! El usuario {user.Email} ha iniciado sesión. UID: {user.UserId}</color>");
    }

    private void OnUserLoggedOut()
    {
        Debug.Log("<color=orange><b>[Tester]</b> El usuario ha cerrado sesión de forma segura.</color>");
    }

    // --- MENÚ DE CLIC DERECHO EN EL INSPECTOR ---

    [ContextMenu("1. Registrar Estudiante")]
    public void RegistrarPrueba()
    {
        Debug.Log($"<b>[Tester]</b> Intentando registrar a: {emailPrueba}");
        AuthManager.Instance.RegistrarEstudiante(emailPrueba, passwordPrueba);
    }

    [ContextMenu("2. Iniciar Sesión")]
    public void IniciarSesionPrueba()
    {
        Debug.Log($"<b>[Tester]</b> Intentando iniciar sesión con: {emailPrueba}");
        AuthManager.Instance.IniciarSesion(emailPrueba, passwordPrueba);
    }

    [ContextMenu("3. Cerrar Sesión")]
    public void CerrarSesionPrueba()
    {
        Debug.Log("<b>[Tester]</b> Cerrando sesión...");
        AuthManager.Instance.SignOut();
    }
}
