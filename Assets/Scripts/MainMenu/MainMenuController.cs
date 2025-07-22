using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Tooltip("Name of the Scene to load when Play is clicked")]
    public string gameplaySceneName = "SoccerGameplay";

    // Hook this up to your Button onClick
    public void OnPlayButtonClicked()
    {
        if (!string.IsNullOrEmpty(gameplaySceneName))
            SceneManager.LoadScene(gameplaySceneName);
        else
            Debug.LogError("Gameplay Scene Name not set in MainMenuController!");
    }
}