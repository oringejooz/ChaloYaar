// ── Bunk Bed ──────────────────────────────────────────────────────────────────
using UnityEngine;

public class BunkBedInteractable : Interactable
{
    [Header("Bunk Bed")]
    [Tooltip("Drowsiness removed per second while sleeping")]
    public float drowsinessReduceRate = 5f;

    [Tooltip("Minimum sleep duration in seconds")]
    public float minSleepSeconds = 5f;

    [Tooltip("Maximum sleep (full rest) in seconds")]
    public float maxSleepSeconds = 30f;

    private bool _playerSleeping;
    private PlayerController _sleeper;

    public override string InteractPrompt =>
        _playerSleeping ? "Press E to wake up" : "Press E to rest";

    public override bool CanInteract(PlayerController player)
        => !_playerSleeping || _sleeper == player;

    public override void Interact(PlayerController player)
    {
        if (!_playerSleeping)
            StartSleep(player);
        else
            WakeUp();
    }

    void StartSleep(PlayerController player)
    {
        _playerSleeping = true;
        _sleeper = player;
        player.Movement.enabled = false;   // freeze movement
        // TODO: play lay-down animation, fade screen
        StartCoroutine(SleepRoutine(player));
    }

    void WakeUp()
    {
        StopAllCoroutines();
        _sleeper.Movement.enabled = true;
        _playerSleeping = false;
        _sleeper = null;
    }

    System.Collections.IEnumerator SleepRoutine(PlayerController player)
    {
        float elapsed = 0f;
        while (elapsed < maxSleepSeconds)
        {
            elapsed += Time.deltaTime;
            player.Stats.ApplyStat(StatType.Drowsiness,
                -drowsinessReduceRate * Time.deltaTime);

            // Fully rested — auto-wake
            if (player.Stats.Drowsiness <= 0f) break;

            yield return null;
        }
        WakeUp();
    }
}