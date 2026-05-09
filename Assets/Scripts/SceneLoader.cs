using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Attach to a UI Button's OnClick() event to load a specific scene.
/// Make sure the target scene is added to File > Build Settings > Scenes In Build.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    [Tooltip("Exact name of the scene to load (must be in Build Settings).")]
    public string sceneName;

    public void LoadScene()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[SceneLoader] Scene name is empty!");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
