using UnityEngine;
using UnityEngine.UIElements;

public class MainMenuSessionController : MonoBehaviour
{
    [SerializeField] private string enterButtonName = "EnterButton";
    [SerializeField] private string loginButtonName = "LoginButton";
    [SerializeField] private string registerButtonName = "RegisterButton";
    [SerializeField] private string logoffButtonName = "LogoffButton";

    private UIDocument document;
    private Button enterButton;
    private Button loginButton;
    private Button registerButton;
    private Button logoffButton;

    private void Awake()
    {
        document = GetComponent<UIDocument>();
    }

    private void Start()
    {
        AuthManager auth = AuthManager.Instance;
        if (auth == null)
        {
            Debug.LogError("MainMenuSessionController: no existe AuthManager.");
            return;
        }

        if (document == null || document.rootVisualElement == null)
        {
            Debug.LogError("MainMenuSessionController: no se pudo obtener el documento del menu.");
            return;
        }

        enterButton = document.rootVisualElement.Q<Button>(enterButtonName);
        if (enterButton == null)
        {
            Debug.LogError("MainMenuSessionController: no existe el boton '" + enterButtonName + "'.");
            return;
        }

        loginButton = document.rootVisualElement.Q<Button>(loginButtonName);
        registerButton = document.rootVisualElement.Q<Button>(registerButtonName);
        logoffButton = document.rootVisualElement.Q<Button>(logoffButtonName);
        if (registerButton == null || logoffButton == null)
        {
            Debug.LogError("MainMenuSessionController: faltan botones del menu de sesion.");
            return;
        }

        enterButton.clicked += EnterWithCurrentSession;
        logoffButton.clicked += SignOut;
        auth.OnFirebaseInitialized += RefreshButton;
        auth.OnUserLoggedIn += HandleUserLoggedIn;
        auth.OnUserLoggedOut += HandleUserLoggedOut;
        RefreshButton();
    }

    private void RefreshButton()
    {
        bool authenticated = AuthManager.Instance != null &&
                             AuthManager.Instance.HasAuthenticatedUser;

        if (enterButton != null)
            enterButton.style.display = authenticated ? DisplayStyle.Flex : DisplayStyle.None;
        if (logoffButton != null)
            logoffButton.style.display = authenticated ? DisplayStyle.Flex : DisplayStyle.None;
        if (loginButton != null)
            loginButton.style.display = authenticated ? DisplayStyle.None : DisplayStyle.Flex;
        if (registerButton != null)
            registerButton.style.display = authenticated ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void HandleUserLoggedIn(Firebase.Auth.FirebaseUser user)
    {
        RefreshButton();
    }

    private void HandleUserLoggedOut()
    {
        RefreshButton();
    }

    private void EnterWithCurrentSession()
    {
        AuthManager.Instance?.EntrarConSesionActual();
    }

    private void SignOut()
    {
        AuthManager.Instance?.SignOut();
    }

    private void OnDestroy()
    {
        if (enterButton != null)
            enterButton.clicked -= EnterWithCurrentSession;
        if (logoffButton != null)
            logoffButton.clicked -= SignOut;

        AuthManager auth = AuthManager.Instance;
        if (auth == null) return;

        auth.OnFirebaseInitialized -= RefreshButton;
        auth.OnUserLoggedIn -= HandleUserLoggedIn;
        auth.OnUserLoggedOut -= HandleUserLoggedOut;
    }
}
