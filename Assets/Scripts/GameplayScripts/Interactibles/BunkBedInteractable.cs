using UnityEngine;

public class BunkBedInteractable : Interactable
{
    [Header("Bunk Bed")]
    public float drowsinessReduceRate = 5f;
    public float minSleepSeconds = 5f;
    public float maxSleepSeconds = 30f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip layDownClip;       // plays when getting into bed
    //public AudioClip sleepingLoopClip;  // looping ambient sleep sound
    public AudioClip wakeUpClip;        // plays on wake

    private bool _playerSleeping;
    private PlayerController _sleeper;

    public override string InteractPrompt =>
        _playerSleeping ? "Press E to wake up" : "Press E to rest";

    public override bool CanInteract(PlayerController player)
        => !_playerSleeping || _sleeper == player;

    public override void Interact(PlayerController player)
    {
        if (!_playerSleeping) StartSleep(player);
        else WakeUp();
    }

    void StartSleep(PlayerController player)
    {
        _playerSleeping = true;
        _sleeper = player;
        player.Movement.enabled = false;
        audioSource?.PlayOneShot(layDownClip);
        StartCoroutine(SleepRoutine(player));
    }

    void WakeUp()
    {
        StopAllCoroutines();

        if (audioSource != null)
        {
            audioSource.loop = false;
            audioSource.Stop();
            audioSource.PlayOneShot(wakeUpClip);
        }

        _sleeper.Movement.enabled = true;
        _playerSleeping = false;
        _sleeper = null;
    }

    System.Collections.IEnumerator SleepRoutine(PlayerController player)
    {
        //// Start looping sleep ambient after lay-down clip finishes
        //if (audioSource != null && sleepingLoopClip != null)
        //{
        //    yield return new WaitForSeconds(layDownClip != null ? layDownClip.length : 0f);
        //    audioSource.clip = sleepingLoopClip;
        //    audioSource.loop = true;
        //    audioSource.Play();
        //}

        float elapsed = 0f;
        while (elapsed < maxSleepSeconds)
        {
            elapsed += Time.deltaTime;
            player.Stats.ApplyStat(StatType.Drowsiness, -drowsinessReduceRate * Time.deltaTime);
            if (player.Stats.Drowsiness <= 0f) break;
            yield return null;
        }

        WakeUp();
    }
}