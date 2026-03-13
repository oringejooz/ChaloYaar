using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public Button loadButton;
    public string sceneName;

    void Start()
    {
        if (loadButton != null)
        {
            loadButton.onClick.AddListener(LoadScene);
        }
    }

    void LoadScene()
    {
        SceneManager.LoadScene(sceneName);
    }
}
