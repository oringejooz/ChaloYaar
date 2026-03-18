using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Persistent singleton that survives scene loads.
/// Owns connection approval and the 4-player cap so the callback is never
/// destroyed when the Menu scene is replaced by Terrain.
///
/// SETUP: Create an empty GameObject in your Menu scene, attach this script,
/// and make sure it is NOT the same object as NetworkManagerUI.
/// </summary>
public class GameNetworkManager : MonoBehaviour
{
    public static GameNetworkManager Singleton { get; private set; }

    private const int MaxPlayers = 4;

    private void Awake()
    {
        // Classic persistent singleton pattern
        if (Singleton != null && Singleton != this)
        {
            Destroy(gameObject);
            return;
        }

        Singleton = this;
        DontDestroyOnLoad(gameObject);  // <-- survives the Menu → Terrain scene load
    }

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[GameNetworkManager] NetworkManager.Singleton is null.");
            return;
        }

        // This callback must be registered before StartHost() is called,
        // and it must stay alive after the scene transition — both are
        // guaranteed because this object is DontDestroyOnLoad.
        NetworkManager.Singleton.ConnectionApprovalCallback += ApproveConnection;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.ConnectionApprovalCallback -= ApproveConnection;
    }

    // ── connection approval ──────────────────────────────────────────────────

    private void ApproveConnection(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        int currentPlayers = NetworkManager.Singleton.ConnectedClientsList.Count;

        if (currentPlayers >= MaxPlayers)
        {
            response.Approved = false;
            response.Reason = "Server is full (max 4 players).";
            Debug.Log($"[GameNetworkManager] Rejected client {request.ClientNetworkId} — lobby full.");
            return;
        }

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Position = GetSpawnPosition(currentPlayers);
        response.Rotation = Quaternion.identity;

        Debug.Log($"[GameNetworkManager] Approved client {request.ClientNetworkId}. " +
                  $"Players: {currentPlayers + 1} / {MaxPlayers}");
    }

    // ── spawn positions ──────────────────────────────────────────────────────

    /// <summary>
    /// Staggered spawn points — tweak these to match real spots in your Terrain scene.
    /// </summary>
    private Vector3 GetSpawnPosition(int playerIndex)
    {
        Vector3[] spawnPoints =
        {
            new Vector3(  0f, 1f,  0f),
            new Vector3(  3f, 1f,  0f),
            new Vector3( -3f, 1f,  0f),
            new Vector3(  0f, 1f,  3f),
        };

        return playerIndex < spawnPoints.Length ? spawnPoints[playerIndex] : Vector3.zero;
    }
}