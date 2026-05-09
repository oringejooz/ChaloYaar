using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Attach to any UI Button (OnClick) or GameObject with a Collider to quit the game.
/// Works in both the Editor and standalone builds.
/// </summary>
public class ExitGame : MonoBehaviour
{
    /// <summary>
    /// Call this from a UI Button's OnClick() event in the Inspector,
    /// or invoke it from any other script.
    /// </summary>
    public void Quit()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;   // Stop Play Mode in the Editor
#else
        Application.Quit();                    // Quit the built application
#endif
        Debug.Log("[ExitGame] Quit called.");
    }
}
