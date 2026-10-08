using SheepyCreations.UIControl.Runtime;
using UnityEngine;

public class QuickLoginBridgeConnector : MonoBehaviour
{
    [Header("Bridges de la UI")]
    [SerializeField] private TextFieldBridge emailBridge;
    [SerializeField] private TextFieldBridge passwordBridge;

    public void TriggerLoginFromBridges()
    {
        if (emailBridge == null || passwordBridge == null) return;
        string email = emailBridge.Value.Trim();
        string password = passwordBridge.Value;
        AuthManager.Instance.IniciarSesion(email, password);
    }

    public void TriggerRegisterFromBridges()
    {
        if (emailBridge == null || passwordBridge == null) return;
        string email = emailBridge.Value.Trim();
        string password = passwordBridge.Value;
        AuthManager.Instance.RegistrarEstudiante(email, password);
    }

    public void TriggerResendVerificationFromBridges()
    {
        if (emailBridge == null || passwordBridge == null) return;
        string email = emailBridge.Value.Trim();
        string password = passwordBridge.Value;
        AuthManager.Instance.ReenviarCorreoVerificacion(email, password);
    }

    // Método para asignar al botón de volver y vaciar los campos de texto
    public void ClearTextFields()
    {
        if (emailBridge != null) emailBridge.SetValue(string.Empty, false);
        if (passwordBridge != null) passwordBridge.SetValue(string.Empty, false);
    }
}