using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Pure UI script — lives in the Menu scene only.
/// Connection approval and player cap are handled by GameNetworkManager (persistent).
/// </summary>
public class NetworkManagerUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button hostBtn;
    [SerializeField] private Button clientBtn;
    [SerializeField] private Button serverBtn;       // reserved — greyed out
    [SerializeField] private Button disconnectBtn;

    [Header("Status")]
    [SerializeField] private TextMeshProUGUI statusText;

    private const string GameScene = "Terrain";

    // ── lifecycle ───────────────────────────────────────────────────────────

    private void Awake()
    {
        if (hostBtn != null) hostBtn.onClick.AddListener(OnHostClicked);
        if (clientBtn != null) clientBtn.onClick.AddListener(OnClientClicked);
        if (disconnectBtn != null) disconnectBtn.onClick.AddListener(OnDisconnectClicked);

        if (serverBtn != null)
        {
            serverBtn.onClick.AddListener(OnServerClicked);
            serverBtn.interactable = false;
        }

        if (disconnectBtn != null) disconnectBtn.gameObject.SetActive(false);
        SetStatus("Select Host or Join");
    }

    private void Start()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetworkManagerUI] NetworkManager.Singleton is null.");
            return;
        }

        // Only UI-relevant callbacks here — approval is in GameNetworkManager
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    // ── button handlers ─────────────────────────────────────────────────────

    private void OnHostClicked()
    {
        // GameNetworkManager.Singleton has already registered the approval callback
        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.LoadScene(GameScene, LoadSceneMode.Single);

        SetConnectedUI();
        SetStatus("Hosting | Players: 1 / 4");
    }

    private void OnClientClicked()
    {
        NetworkManager.Singleton.StartClient();
        SetConnectedUI();
        SetStatus("Connecting...");
    }

    private void OnServerClicked()
    {
        Debug.Log("[NetworkManagerUI] Dedicated server mode not configured yet.");
    }

    private void OnDisconnectClicked()
    {
        NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ── NGO callbacks (UI refresh only) ─────────────────────────────────────

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return;
        int count = NetworkManager.Singleton.ConnectedClientsList.Count;
        SetStatus($"Host | Players: {count} / 4");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        bool weLeft = !NetworkManager.Singleton.IsConnectedClient
                   && !NetworkManager.Singleton.IsHost;
        if (weLeft)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        int count = NetworkManager.Singleton.ConnectedClientsList.Count;
        SetStatus($"Host | Players: {count} / 4");
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private void SetConnectedUI()
    {
        if (hostBtn != null) hostBtn.gameObject.SetActive(false);
        if (clientBtn != null) clientBtn.gameObject.SetActive(false);
        if (serverBtn != null) serverBtn.gameObject.SetActive(false);
        if (disconnectBtn != null) disconnectBtn.gameObject.SetActive(true);
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }
}