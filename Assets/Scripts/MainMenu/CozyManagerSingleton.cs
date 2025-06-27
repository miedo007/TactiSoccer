using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ① Keeps exactly one CozyManager alive across scenes.
/// ② Shows the menu Canvas only in scenes you list (e.g. "MainMenu").
/// Attach this to the root object of the CozyManager prefab.
/// </summary>
[DefaultExecutionOrder(-1000)]   // run before most other scripts
public class CozyManagerSingleton : MonoBehaviour
{
    // Drag the visible menu Canvas here in the Inspector
    [SerializeField] private Canvas menuCanvas;

    // Add the scene names where the Canvas SHOULD be visible
    [SerializeField] private string[] scenesThatShowMenu = { "MainMenu" };

    private static CozyManagerSingleton _instance;

    private void Awake()
    {
        // ------- Singleton logic -------
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);        // duplicate → discard
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);  // persist for the entire session

        // ------- Scene-change callback -------
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
    }

    private void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        bool show = System.Array.Exists(
            scenesThatShowMenu,
            s => s.Equals(newScene.name, System.StringComparison.OrdinalIgnoreCase));

        if (menuCanvas != null)
        {
            menuCanvas.enabled = show;

            // If the Canvas has a CanvasGroup, also block/unblock raycasts
            var cg = menuCanvas.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = show;
        }
    }
}
