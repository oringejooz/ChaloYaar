using UnityEngine;

public class StoveInteractable : Interactable
{
    [Header("Stove")]
    public VanSystemsHub vanHub;
    public float cookTime = 10f;
    public ItemData cookedItem;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip cookingLoopClip;   // looping sizzle sound
    //public AudioClip cookDoneClip;      // ding when meal is ready

    private bool _isCooking;
    private float _cookTimer;

    public override string InteractPrompt =>
        _isCooking ? $"Cooking… ({Mathf.CeilToInt(_cookTimer)}s)" : "Press E to Cook";

    public override bool CanInteract(PlayerController player)
        => !_isCooking && vanHub != null && vanHub.gas.current > 0;

    public override void Interact(PlayerController player)
    {
        if (_isCooking) return;
        _isCooking = true;
        _cookTimer = cookTime;
        vanHub.UseStove();
        StartCoroutine(CookRoutine(player));
    }

    System.Collections.IEnumerator CookRoutine(PlayerController player)
    {
        // Start looping cooking sound
        if (audioSource != null && cookingLoopClip != null)
        {
            audioSource.clip = cookingLoopClip;
            //audioSource.loop = true;
            audioSource.Play();
        }

        while (_cookTimer > 0f)
        {
            _cookTimer -= Time.deltaTime;
            yield return null;
        }

        //// Stop loop, play done sound
        //if (audioSource != null)
        //{
        //    audioSource.loop = false;
        //    audioSource.Stop();
        //    audioSource.PlayOneShot(cookDoneClip);
        //}

        _isCooking = false;
        if (cookedItem != null)
        {
            player.Inventory.AddItem(cookedItem, 1);
            Debug.Log($"[Stove] Cooked: {cookedItem.itemName}");
        }
    }
}