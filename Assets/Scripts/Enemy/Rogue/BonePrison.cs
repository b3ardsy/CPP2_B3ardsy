using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BonePrison : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float activationDelay = 0.1f;
    [SerializeField] private float lifetime = 3f;

    [Header("Damage")]
    [SerializeField] private bool dealDamageOnCapture;
    [SerializeField] private int captureDamage = 1;

    [Header("References")]
    [Tooltip("Trigger collider covering the inside of the prison.")]
    [SerializeField] private Collider captureTrigger;

    private readonly HashSet<PlayerMovement3D> trappedPlayers =
        new HashSet<PlayerMovement3D>();

    private readonly HashSet<PlayerStats> damagedPlayers =
        new HashSet<PlayerStats>();

    private bool isActive;
    private bool isBeingDestroyed;

    private void Awake()
    {
        if (captureTrigger == null)
        {
            captureTrigger = GetComponent<Collider>();
        }

        if (captureTrigger == null)
        {
            Debug.LogError(
                $"{name}: Bone Prison needs a capture trigger collider.",
                this
            );

            enabled = false;
            return;
        }

        captureTrigger.isTrigger = true;
        captureTrigger.enabled = false;
    }

    private void Start()
    {
        StartCoroutine(PrisonRoutine());
    }

    private IEnumerator PrisonRoutine()
    {
        if (activationDelay > 0f)
        {
            yield return new WaitForSeconds(activationDelay);
        }

        isActive = true;
        captureTrigger.enabled = true;

        float remainingLifetime =
            Mathf.Max(0f, lifetime - activationDelay);

        yield return new WaitForSeconds(remainingLifetime);

        ReleaseAndDestroy();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCapturePlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryCapturePlayer(other);
    }

    private void TryCapturePlayer(Collider other)
    {
        if (!isActive)
        {
            return;
        }

        PlayerMovement3D playerMovement =
            other.GetComponentInParent<PlayerMovement3D>();

        if (playerMovement == null)
        {
            return;
        }

        if (trappedPlayers.Add(playerMovement))
        {
            playerMovement.AddMovementLock(this);

            Debug.Log(
                $"{name}: Player captured by Bone Prison.",
                this
            );
        }

        if (!dealDamageOnCapture)
        {
            return;
        }

        PlayerStats playerStats =
            other.GetComponentInParent<PlayerStats>();

        if (playerStats != null &&
            damagedPlayers.Add(playerStats))
        {
            playerStats.TakeDamage(captureDamage);
        }
    }

    private void ReleaseAndDestroy()
    {
        if (isBeingDestroyed)
        {
            return;
        }

        isBeingDestroyed = true;
        isActive = false;

        if (captureTrigger != null)
        {
            captureTrigger.enabled = false;
        }

        ReleasePlayers();
        Destroy(gameObject);
    }

    private void ReleasePlayers()
    {
        foreach (PlayerMovement3D trappedPlayer in trappedPlayers)
        {
            if (trappedPlayer != null)
            {
                trappedPlayer.RemoveMovementLock(this);
            }
        }

        trappedPlayers.Clear();
    }

    private void OnDestroy()
    {
        ReleasePlayers();
    }

    private void OnValidate()
    {
        activationDelay = Mathf.Max(0f, activationDelay);
        lifetime = Mathf.Max(activationDelay, lifetime);
        captureDamage = Mathf.Max(1, captureDamage);
    }
}