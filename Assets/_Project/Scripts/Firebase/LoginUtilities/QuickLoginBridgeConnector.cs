using SheepyCreations.UIControl.Runtime;
using UnityEngine;

public class QuickLoginBridgeConnector : MonoBehaviour
{
    [Header("Bridges de la UI")]
    [SerializeField] private TextFieldBridge emailBridge;
    [SerializeField] private TextFieldBridge passwordBridge;
    [SerializeField] private TextFieldBridge confirmPasswordBridge;

    void Start()
    {
        // 1. Suscribirse al evento de login exitoso para guardar credenciales
        AuthManager.Instance.OnUserLoggedIn += GuardarCredencialesAlLoguear;

        // 2. Intentar autocompletar los campos al arrancar la pantalla
        AutocompletarCampos();
    }

    public void TriggerLoginFromBridges()
    {
        if (emailBridge == null || passwordBridge == null) return;
        string email = emailBridge.Value.Trim();
        string password = passwordBridge.Value;
        AuthManager.Instance.IniciarSesion(email, password);
    }

    public void TriggerRegisterFromBridges()
    {
        if (emailBridge == null || passwordBridge == null || confirmPasswordBridge == null) return;

        string email = emailBridge.Value.Trim();
        string password = passwordBridge.Value;
        string confirmPassword = confirmPasswordBridge.Value;

        // Validar que las contraseñas coincidan antes de enviar a Firebase
        if (password != confirmPassword)
        {
            Debug.LogError("Error de registro: Las contraseñas no coinciden.");
            // Opcional: Mostrar un mensaje en pantalla si tienes un label de feedback
            return;
        }

        // Validar longitud mínima que exige Firebase (por lo general 6 caracteres)
        if (password.Length < 6)
        {
            Debug.LogError("Error de registro: La contraseña debe tener al menos 6 caracteres.");
            return;
        }

        AuthManager.Instance.RegistrarEstudiante(email, password);
    }

    public void TriggerResendVerificationFromBridges()
    {
        if (emailBridge == null || passwordBridge == null) return;
        string email = emailBridge.Value.Trim();
        string password = passwordBridge.Value;
        AuthManager.Instance.ReenviarCorreoVerificacion(email, password);
    }

    // Busca los datos guardados en el PlayerPrefs y llena las cajas de texto
    private void AutocompletarCampos()
    {
        if (PlayerPrefs.HasKey("Credencial_Email") && emailBridge != null)
        {
            emailBridge.SetValue(PlayerPrefs.GetString("Credencial_Email"), false);
        }

        if (PlayerPrefs.HasKey("Credencial_Password") && passwordBridge != null)
        {
            passwordBridge.SetValue(PlayerPrefs.GetString("Credencial_Password"), false);
        }
    }

    // Se ejecuta de forma automática solo si el inicio de sesión fue exitoso y verificado
    private void GuardarCredencialesAlLoguear(Firebase.Auth.FirebaseUser user)
    {
        if (emailBridge == null || passwordBridge == null) return;

        string email = emailBridge.Value.Trim();
        string password = passwordBridge.Value;

        // Guardar localmente en el almacenamiento del celular
        PlayerPrefs.SetString("Credencial_Email", email);
        PlayerPrefs.SetString("Credencial_Password", password);
        PlayerPrefs.Save();

        Debug.Log("Credenciales verificadas y guardadas localmente en el dispositivo.");
    }

    // Método para llamar desde tu UIDocumentPanel al mostrar el panel de Login
    public void IntentarAutologueoSiHayCuenta()
    {
        Debug.Log("Intentando autologueo si hay credenciales guardadas...");

        // Verificar si existen credenciales guardadas en el almacenamiento del dispositivo
        if (PlayerPrefs.HasKey("Credencial_Email") && PlayerPrefs.HasKey("Credencial_Password"))
        {
            string email = PlayerPrefs.GetString("Credencial_Email");
            string password = PlayerPrefs.GetString("Credencial_Password");

            Debug.Log("Credenciales locales encontradas. Iniciando sesión automática...");

            // Ejecutar el login. Al tener éxito, AuthManager disparará el evento OnUserLoggedIn
            AuthManager.Instance.IniciarSesion(email, password);
        }
        else
        {
            Debug.Log("No se encontraron credenciales locales para inicio automático.");
        }
    }


    public void ClearTextFields()
    {
        if (emailBridge != null) emailBridge.SetValue(string.Empty, false);
        if (passwordBridge != null) passwordBridge.SetValue(string.Empty, false);
    }

    private void OnDestroy()
    {
        if (AuthManager.Instance != null)
        {
            AuthManager.Instance.OnUserLoggedIn -= GuardarCredencialesAlLoguear;
        }
    }
}
