using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class EscapeToHome : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private string homeSceneName = "Home";
    [SerializeField] private bool ignoreIfAlreadyInHome = true;

    private static EscapeToHome instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (ignoreIfAlreadyInHome && SceneManager.GetActiveScene().name == homeSceneName)
        {
            return;
        }

        SceneManager.LoadScene(homeSceneName);
    }
}
