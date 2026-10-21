using SheepyCreations.UIControl.Runtime;
using UnityEngine;

public class QuickLoginBridgeConnector : MonoBehaviour
{
    [Header("Bridges de la UI")]
    [SerializeField] private TextFieldBridge emailBridge;
    [SerializeField] private TextFieldBridge passwordBridge;
    [SerializeField] private TextFieldBridge confirmPasswordBridge;

    [Header("Sistema de Notificaciones")]
    [SerializeField] private NotificationBehaviour notificationPanel;

    void Start()
    {
        // Firebase conserva la sesion; solo se recuerda el correo para facilitar el formulario.
        AuthManager.Instance.OnUserLoggedIn += GuardarCredencialesAlLoguear;

        // Intentar autocompletar el correo al arrancar la pantalla.
        AutocompletarCampos();
    }

    public void TriggerLoginFromBridges()
    {
        if (AuthManager.Instance != null && AuthManager.Instance.HasAuthenticatedUser)
        {
            AuthManager.Instance.EntrarConSesionActual();
            return;
        }

        if (emailBridge == null || passwordBridge == null) return;
        string email = emailBridge.Value.Trim();
        string password = passwordBridge.Value;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowNotification("Por favor completa todos los campos.");
            return;
        }

        AuthManager.Instance.IniciarSesion(email, password);
    }

    public void TriggerRegisterFromBridges()
    {
        if (emailBridge == null || passwordBridge == null || confirmPasswordBridge == null) return;

        string email = emailBridge.Value.Trim();
        string password = passwordBridge.Value;
        string confirmPassword = confirmPasswordBridge.Value;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
        {
            ShowNotification("Por favor completa todos los campos.");
            return;
        }

        if (password != confirmPassword)
        {
            ShowNotification("Las contrase�as no coinciden.");
            return;
        }

        if (password.Length < 6)
        {
            ShowNotification("La contrase�a debe tener al menos 6 caracteres.");
            return;
        }

        AuthManager.Instance.RegistrarEstudiante(email, password);
    }

    public void TriggerResendVerificationFromBridges()
    {
        if (emailBridge == null || passwordBridge == null) return;
        string email = emailBridge.Value.Trim();
        string password = passwordBridge.Value;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            ShowNotification("Ingresa tu correo y contrase�a para reenviar la verificaci�n.");
            return;
        }

        AuthManager.Instance.ReenviarCorreoVerificacion(email, password);
    }

    // Busca los datos guardados en el PlayerPrefs y llena las cajas de texto
    private void AutocompletarCampos()
    {
        if (PlayerPrefs.HasKey("Credencial_Email") && emailBridge != null)
        {
            emailBridge.SetValue(PlayerPrefs.GetString("Credencial_Email"), false);
        }

    }

    // Se ejecuta de forma autom�tica solo si el inicio de sesi�n fue exitoso y verificado
    private void GuardarCredencialesAlLoguear(Firebase.Auth.FirebaseUser user)
    {
        if (emailBridge == null) return;

        string email = emailBridge.Value.Trim();

        // Guardar localmente en el almacenamiento del celular
        PlayerPrefs.SetString("Credencial_Email", email);
        PlayerPrefs.DeleteKey("Credencial_Password");
        PlayerPrefs.Save();

        Debug.Log("Correo verificado guardado localmente. Firebase conserva la sesion.");
    }

    // M�todo para llamar desde tu UIDocumentPanel al mostrar el panel de Login
    public void IntentarAutologueoSiHayCuenta()
    {
        AuthManager.Instance.EntrarConSesionActual();
    }

    private void ShowNotification(string mensaje)
    {
        Debug.LogWarning(mensaje);
        if (notificationPanel != null)
        {
            notificationPanel.Enqueue(mensaje);
        }
    }

    public void ClearTextFields()
    {
        if (emailBridge != null) emailBridge.SetValue(string.Empty, false);
        if (passwordBridge != null) passwordBridge.SetValue(string.Empty, false);
        if (confirmPasswordBridge != null) confirmPasswordBridge.SetValue(string.Empty, false);
    }

    private void OnDestroy()
    {
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.OnUserLoggedIn -= GuardarCredencialesAlLoguear;
        }
    }
}
