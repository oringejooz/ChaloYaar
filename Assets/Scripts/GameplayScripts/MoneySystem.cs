using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  MoneySystem.cs
//  Chalo Yaar! — Shared party budget. All players access the same wallet.
//
//  This is a singleton-style component placed on a persistent GameManager GO.
//  In multiplayer, the host will own authority over this value (Netcode ready).
//
//  Usage:
//    MoneySystem.Instance.Spend(amount)
//    MoneySystem.Instance.Earn(amount)
// ─────────────────────────────────────────────────────────────────────────────

public class MoneySystem : MonoBehaviour
{
    public static MoneySystem Instance { get; private set; }

    [Header("Starting Budget (₹)")]
    public int startingMoney = 5000;

    [Header("Current Balance — Runtime")]
    [SerializeField] private int _balance;

    // Events
    public System.Action<int, int> OnBalanceChanged; // (newBalance, delta)
    public System.Action           OnBroke;

    public int Balance => _balance;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        _balance = startingMoney;
    }

    // ── Transactions ──────────────────────────────────────────────────────────

    /// <summary>Returns true if purchase was successful.</summary>
    public bool Spend(int amount)
    {
        if (amount <= 0) return false;
        if (_balance < amount)
        {
            Debug.Log($"[MoneySystem] Not enough money. Need {amount}, have {_balance}.");
            return false;
        }

        _balance -= amount;
        OnBalanceChanged?.Invoke(_balance, -amount);
        if (_balance == 0) OnBroke?.Invoke();
        return true;
    }

    public void Earn(int amount)
    {
        if (amount <= 0) return;
        _balance += amount;
        OnBalanceChanged?.Invoke(_balance, amount);
    }

    public bool CanAfford(int amount) => _balance >= amount;

    // ── Save / Load hooks (plug into your SaveSystem later) ───────────────────
    public int GetSaveData()  => _balance;
    public void LoadSaveData(int saved) => _balance = saved;
}

// ─────────────────────────────────────────────────────────────────────────────
//  NOTE: PlayerMovementAdvanced.cs patch
//  Add this public property to PlayerMovementAdvanced so PlayerNeeds can read it.
//  (You cannot edit the file from here — add manually, or use a partial class.)
//
//  In PlayerMovementAdvanced, add inside the class body:
//
//      public bool IsSprinting => _isSprinting;
//
//  This exposes the private _isSprinting field from PlayerMovementAdvanced.
// ─────────────────────────────────────────────────────────────────────────────
