using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour, IDamageable
{
    // =========================================================
    // DEATH
    // =========================================================

    [Header("Death")]
    [SerializeField]
    private float destroyDelay = 2f;

    [SerializeField]
    private GameObject deathEffectPrefab;

    [SerializeField]
    private Vector3 deathEffectSpawnOffset =
        Vector3.zero;

    [SerializeField]
    private float deathEffectLifetime = 3f;

    // =========================================================
    // PICKUP DROP
    // =========================================================

    [Header("Pickup Drop")]
    [SerializeField]
    private PickupType pickupToDrop =
        PickupType.Health;

    [SerializeField]
    private GameObject healthPickupPrefab;

    [SerializeField]
    private GameObject ammoPickupPrefab;

    [SerializeField]
    private GameObject specialPickupPrefab;

    [SerializeField]
    private float pickupSpawnHeight = 0.5f;

    // =========================================================
    // PATROL
    // =========================================================

    [Header("Patrol")]
    [SerializeField]
    private float patrolRadius = 10f;

    [SerializeField]
    private float patrolWaitTime = 2f;

    [SerializeField]
    private float destinationTolerance = 0.25f;

    [SerializeField]
    private float navMeshSampleDistance = 3f;

    [SerializeField]
    private int patrolSearchAttempts = 10;

    // =========================================================
    // DETECTION
    // =========================================================

    [Header("Detection")]
    [SerializeField]
    private float detectionRange = 20f;

    [SerializeField]
    private float loseTargetRange = 25f;

    // =========================================================
    // MOVEMENT
    // =========================================================

    [Header("Movement")]
    [SerializeField]
    private float patrolSpeed = 2f;

    [SerializeField]
    private float returnSpeed = 3f;

    [SerializeField]
    private float engagedRotationSpeed = 8f;

    // =========================================================
    // REFERENCES
    // =========================================================

    private Health health;
    private NavMeshAgent agent;
    private Animator animator;
    private CapsuleCollider capsuleCollider;

    private Transform player;
    private Health playerHealth;

    // =========================================================
    // RUNTIME STATE
    // =========================================================

    private bool deathHandled;

    private bool isEntangled;
    private Coroutine entangleRoutine;
    private GameObject activeEntangleEffect;

    private Vector3 homePosition;

    private float patrolWaitTimer;

    private bool isWaitingAtPatrolPoint;
    private bool hasPatrolDestination;

    // =========================================================
    // EVENTS
    // =========================================================

    public event Action OnDamaged;
    public event Action OnDied;
    public event Action OnEntangled;
    public event Action OnEntangleEnded;

    // =========================================================
    // PICKUP TYPES
    // =========================================================

    public enum PickupType
    {
        None,
        Health,
        Ammo,
        Special
    }

    // =========================================================
    // ANIMATOR PARAMETERS
    // =========================================================

    private static readonly int HitTrigger =
        Animator.StringToHash("Hit");

    private static readonly int DeathTrigger =
        Animator.StringToHash("Death");

    private static readonly int EntangleTrigger =
        Animator.StringToHash("Entangle");

    // =========================================================
    // PUBLIC PROPERTIES
    // =========================================================

    public Health Health =>
        health;

    public NavMeshAgent Agent =>
        agent;

    public Animator Animator =>
        animator;

    public Transform Player =>
        player;

    public Health PlayerHealth =>
        playerHealth;

    public Vector3 HomePosition =>
        homePosition;

    public int CurrentHealth =>
        health != null
            ? health.CurrentHealth
            : 0;

    public int MaxHealth =>
        health != null
            ? health.MaxHealth
            : 0;

    public bool IsDead =>
        health != null &&
        health.IsDead;

    public bool IsEntangled =>
        isEntangled;

    public bool IsPlayerDead =>
        playerHealth != null &&
        playerHealth.IsDead;

    public bool IsWaitingAtPatrolPoint =>
        isWaitingAtPatrolPoint;

    public bool HasPatrolDestination =>
        hasPatrolDestination;

    public bool IsOnNavMesh =>
        agent != null &&
        agent.enabled &&
        agent.isOnNavMesh;

    public float DetectionRange =>
        detectionRange;

    public float LoseTargetRange =>
        loseTargetRange;

    public float PatrolSpeed =>
        patrolSpeed;

    public float ReturnSpeed =>
        returnSpeed;

    public float EngagedRotationSpeed =>
        engagedRotationSpeed;

    public float DistanceToPlayer
    {
        get
        {
            if (player == null)
            {
                return Mathf.Infinity;
            }

            return GetFlatDistance(
                transform.position,
                player.position
            );
        }
    }

    public Vector3 LockOnPoint
    {
        get
        {
            if (capsuleCollider == null)
            {
                return transform.position;
            }

            return
                capsuleCollider.transform.TransformPoint(
                    capsuleCollider.center
                );
        }
    }

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        health =
            GetComponent<Health>();

        agent =
            GetComponent<NavMeshAgent>();

        animator =
            GetComponentInChildren<Animator>();

        capsuleCollider =
            GetComponentInChildren<CapsuleCollider>();

        FindPlayerReferences();

        deathHandled =
            false;

        if (health == null)
        {
            Debug.LogError(
                $"{name}: EnemyController requires a Health component.",
                this
            );

            enabled =
                false;

            return;
        }

        if (agent == null)
        {
            Debug.LogError(
                $"{name}: EnemyController requires a NavMeshAgent.",
                this
            );

            enabled =
                false;

            return;
        }

        if (animator == null)
        {
            Debug.LogError(
                $"{name}: No Animator component was found.",
                this
            );
        }

        if (capsuleCollider == null)
        {
            Debug.LogWarning(
                $"{name}: No CapsuleCollider was found. " +
                "Transform position will be used for lock-on.",
                this
            );
        }

        health.OnDied +=
            HandleHealthDepleted;
    }

    private void Start()
    {
        homePosition =
            transform.position;

        if (!IsOnNavMesh)
        {
            Debug.LogError(
                $"{name}: EnemyController is not positioned " +
                "on a baked NavMesh.",
                this
            );

            enabled =
                false;
        }
    }

    // =========================================================
    // PLAYER REFERENCES
    // =========================================================

    private void FindPlayerReferences()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag(
                "Player"
            );

        if (playerObject == null)
        {
            Debug.LogError(
                $"{name}: EnemyController could not find " +
                "a GameObject with the Player tag.",
                this
            );

            return;
        }

        player =
            playerObject.transform;

        playerHealth =
            playerObject.GetComponent<Health>();

        if (playerHealth == null)
        {
            playerHealth =
                playerObject.GetComponentInParent<Health>();
        }

        if (playerHealth == null)
        {
            playerHealth =
                playerObject.GetComponentInChildren<Health>();
        }

        if (playerHealth == null)
        {
            Debug.LogWarning(
                $"{name}: EnemyController could not find " +
                "the player's Health component.",
                this
            );
        }
    }

    // =========================================================
    // DAMAGE
    // =========================================================

    public void TakeDamage(
        int damage
    )
    {
        if (
            health == null ||
            IsDead
        )
        {
            return;
        }

        bool damageApplied =
            health.TakeDamage(
                damage
            );

        if (!damageApplied)
        {
            return;
        }

        /*
         * EnemyController owns the actual damage handling.
         *
         * Enemy-specific logic can optionally listen for this
         * event when it needs to react to a successful hit.
         */
        OnDamaged?.Invoke();

        if (IsDead)
        {
            return;
        }

        if (animator != null)
        {
            animator.SetTrigger(
                HitTrigger
            );
        }
    }

    private void HandleHealthDepleted()
    {
        Die();
    }

    // =========================================================
    // ENTANGLE
    // =========================================================

    public void ApplyEntangle(
        float duration,
        GameObject entangleEffectPrefab,
        Vector3 localOffset
    )
    {
        if (IsDead)
        {
            return;
        }

        duration =
            Mathf.Max(
                0.1f,
                duration
            );

        if (entangleRoutine != null)
        {
            StopCoroutine(
                entangleRoutine
            );

            entangleRoutine =
                null;
        }

        DestroyEntangleEffect();

        isEntangled =
            true;

        StopAgent();

        SpawnEntangleEffect(
            entangleEffectPrefab,
            localOffset
        );

        if (animator != null)
        {
            animator.ResetTrigger(
                HitTrigger
            );

            animator.ResetTrigger(
                EntangleTrigger
            );

            animator.SetTrigger(
                EntangleTrigger
            );
        }

        OnEntangled?.Invoke();

        entangleRoutine =
            StartCoroutine(
                EntangleRoutine(
                    duration
                )
            );

        Debug.Log(
            $"{name}: Entangled for {duration:0.0} seconds.",
            this
        );
    }

    private IEnumerator EntangleRoutine(
        float duration
    )
    {
        yield return new WaitForSeconds(
            duration
        );

        entangleRoutine =
            null;

        EndEntangle();
    }

    private void SpawnEntangleEffect(
        GameObject entangleEffectPrefab,
        Vector3 localOffset
    )
    {
        if (entangleEffectPrefab == null)
        {
            return;
        }

        activeEntangleEffect =
            Instantiate(
                entangleEffectPrefab,
                transform
            );

        activeEntangleEffect.transform.localPosition =
            localOffset;

        activeEntangleEffect.transform.localRotation =
            entangleEffectPrefab.transform.localRotation;
    }

    public void EndEntangle()
    {
        if (!isEntangled)
        {
            return;
        }

        isEntangled =
            false;

        if (entangleRoutine != null)
        {
            StopCoroutine(
                entangleRoutine
            );

            entangleRoutine =
                null;
        }

        DestroyEntangleEffect();

        OnEntangleEnded?.Invoke();

        Debug.Log(
            $"{name}: Entangle ended.",
            this
        );
    }

    private void DestroyEntangleEffect()
    {
        if (activeEntangleEffect == null)
        {
            return;
        }

        Destroy(
            activeEntangleEffect
        );

        activeEntangleEffect =
            null;
    }

    // =========================================================
    // DEATH
    // =========================================================

    private void Die()
    {
        if (deathHandled)
        {
            return;
        }

        deathHandled =
            true;

        if (entangleRoutine != null)
        {
            StopCoroutine(
                entangleRoutine
            );

            entangleRoutine =
                null;
        }

        isEntangled =
            false;

        DestroyEntangleEffect();

        StopAgent();

        OnDied?.Invoke();

        if (animator != null)
        {
            animator.ResetTrigger(
                EntangleTrigger
            );

            animator.SetTrigger(
                DeathTrigger
            );
        }

        DropPickup();

        StartCoroutine(
            DestroyAfterDelay()
        );
    }

    // =========================================================
    // DEATH EFFECT
    // =========================================================

    private Vector3 GetVisualBodyCentre()
    {
        Renderer[] renderers =
            GetComponentsInChildren<Renderer>();

        if (
            renderers == null ||
            renderers.Length == 0
        )
        {
            return LockOnPoint;
        }

        bool foundRenderer =
            false;

        Bounds combinedBounds =
            new Bounds(
                transform.position,
                Vector3.zero
            );

        foreach (
            Renderer enemyRenderer
            in renderers
        )
        {
            if (
                enemyRenderer == null ||
                !enemyRenderer.enabled ||
                !enemyRenderer.gameObject.activeInHierarchy
            )
            {
                continue;
            }

            if (
                activeEntangleEffect != null &&
                enemyRenderer.transform.IsChildOf(
                    activeEntangleEffect.transform
                )
            )
            {
                continue;
            }

            if (!foundRenderer)
            {
                combinedBounds =
                    enemyRenderer.bounds;

                foundRenderer =
                    true;

                continue;
            }

            combinedBounds.Encapsulate(
                enemyRenderer.bounds
            );
        }

        if (!foundRenderer)
        {
            return LockOnPoint;
        }

        return combinedBounds.center;
    }

    private void DisableEnemyColliders()
    {
        Collider[] enemyColliders =
            GetComponentsInChildren<Collider>();

        foreach (
            Collider enemyCollider
            in enemyColliders
        )
        {
            if (enemyCollider != null)
            {
                enemyCollider.enabled =
                    false;
            }
        }
    }

    private void SpawnDeathEffect(
        Vector3 spawnPosition
    )
    {
        if (deathEffectPrefab == null)
        {
            return;
        }

        GameObject deathEffect =
            Instantiate(
                deathEffectPrefab,
                spawnPosition,
                deathEffectPrefab.transform.rotation
            );

        ParticleSystem[] particleSystems =
            deathEffect.GetComponentsInChildren
                <ParticleSystem>();

        foreach (
            ParticleSystem particleSystem
            in particleSystems
        )
        {
            if (particleSystem != null)
            {
                particleSystem.Play();
            }
        }

        Destroy(
            deathEffect,
            deathEffectLifetime
        );
    }

    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(
            destroyDelay
        );

        Vector3 deathEffectPosition =
            GetVisualBodyCentre() +
            deathEffectSpawnOffset;

        DisableEnemyColliders();

        SpawnDeathEffect(
            deathEffectPosition
        );

        Destroy(
            gameObject
        );
    }

    // =========================================================
    // PICKUPS
    // =========================================================

    private void DropPickup()
    {
        GameObject pickupPrefab =
            GetPickupPrefab();

        if (pickupPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition =
            transform.position +
            Vector3.up *
            pickupSpawnHeight;

        Instantiate(
            pickupPrefab,
            spawnPosition,
            Quaternion.identity
        );
    }

    private GameObject GetPickupPrefab()
    {
        switch (pickupToDrop)
        {
            case PickupType.Health:
                return healthPickupPrefab;

            case PickupType.Ammo:
                return ammoPickupPrefab;

            case PickupType.Special:
                return specialPickupPrefab;

            default:
                return null;
        }
    }

    // =========================================================
    // PATROL
    // =========================================================

    public void BeginPatrol()
    {
        if (!CanNavigate())
        {
            return;
        }

        agent.speed =
            patrolSpeed;

        agent.stoppingDistance =
            0f;

        agent.isStopped =
            false;

        isWaitingAtPatrolPoint =
            false;

        ChooseRandomPatrolDestination();
    }

    public void UpdatePatrol()
    {
        if (!CanNavigate())
        {
            return;
        }

        agent.speed =
            patrolSpeed;

        agent.stoppingDistance =
            0f;

        if (isWaitingAtPatrolPoint)
        {
            agent.isStopped =
                true;

            patrolWaitTimer -=
                Time.deltaTime;

            if (patrolWaitTimer <= 0f)
            {
                isWaitingAtPatrolPoint =
                    false;

                ChooseRandomPatrolDestination();
            }

            return;
        }

        if (!hasPatrolDestination)
        {
            ChooseRandomPatrolDestination();

            return;
        }

        if (!HasReachedDestination())
        {
            return;
        }

        agent.isStopped =
            true;

        hasPatrolDestination =
            false;

        isWaitingAtPatrolPoint =
            true;

        patrolWaitTimer =
            patrolWaitTime;
    }

    public bool ChooseRandomPatrolDestination()
    {
        if (!CanNavigate())
        {
            return false;
        }

        for (
            int attempt = 0;
            attempt < patrolSearchAttempts;
            attempt++
        )
        {
            Vector2 randomCircle =
                UnityEngine.Random.insideUnitCircle *
                patrolRadius;

            Vector3 randomPosition =
                homePosition +
                new Vector3(
                    randomCircle.x,
                    0f,
                    randomCircle.y
                );

            if (
                !NavMesh.SamplePosition(
                    randomPosition,
                    out NavMeshHit hit,
                    navMeshSampleDistance,
                    agent.areaMask
                )
            )
            {
                continue;
            }

            if (
                GetFlatDistance(
                    homePosition,
                    hit.position
                ) >
                patrolRadius
            )
            {
                continue;
            }

            agent.speed =
                patrolSpeed;

            agent.stoppingDistance =
                0f;

            agent.isStopped =
                false;

            hasPatrolDestination =
                agent.SetDestination(
                    hit.position
                );

            return
                hasPatrolDestination;
        }

        hasPatrolDestination =
            false;

        isWaitingAtPatrolPoint =
            true;

        patrolWaitTimer =
            patrolWaitTime;

        return false;
    }

    public void BeginWaitingAtPatrolPoint()
    {
        StopAgent();

        hasPatrolDestination =
            false;

        isWaitingAtPatrolPoint =
            true;

        patrolWaitTimer =
            patrolWaitTime;
    }

    public void ClearPatrolState()
    {
        isWaitingAtPatrolPoint =
            false;

        hasPatrolDestination =
            false;

        patrolWaitTimer =
            0f;
    }

    // =========================================================
    // RETURN HOME
    // =========================================================

    public bool SetHomeDestination()
    {
        if (!CanNavigate())
        {
            return false;
        }

        if (
            !NavMesh.SamplePosition(
                homePosition,
                out NavMeshHit hit,
                navMeshSampleDistance,
                agent.areaMask
            )
        )
        {
            return false;
        }

        agent.speed =
            returnSpeed;

        agent.stoppingDistance =
            0f;

        agent.isStopped =
            false;

        return
            agent.SetDestination(
                hit.position
            );
    }

    public bool UpdateReturnHome()
    {
        if (!CanNavigate())
        {
            return false;
        }

        agent.speed =
            returnSpeed;

        agent.stoppingDistance =
            0f;

        agent.isStopped =
            false;

        if (
            !agent.hasPath &&
            !agent.pathPending
        )
        {
            SetHomeDestination();
        }

        if (!HasReachedDestination())
        {
            return false;
        }

        StopAgent();

        return true;
    }

    // =========================================================
    // NAVIGATION
    // =========================================================

    public void SetDestination(
        Vector3 destination,
        float speed,
        float stoppingDistance
    )
    {
        if (!CanNavigate())
        {
            return;
        }

        agent.speed =
            Mathf.Max(
                0f,
                speed
            );

        agent.stoppingDistance =
            Mathf.Max(
                0f,
                stoppingDistance
            );

        agent.isStopped =
            false;

        agent.SetDestination(
            destination
        );
    }

    public void StopAgent()
    {
        if (!IsOnNavMesh)
        {
            return;
        }

        agent.isStopped =
            true;

        agent.ResetPath();
    }

    public bool HasReachedDestination()
    {
        if (
            !IsOnNavMesh ||
            agent.pathPending
        )
        {
            return false;
        }

        if (
            agent.remainingDistance >
            agent.stoppingDistance +
            destinationTolerance
        )
        {
            return false;
        }

        if (
            agent.hasPath &&
            agent.velocity.sqrMagnitude >
            0.01f
        )
        {
            return false;
        }

        return true;
    }

    private bool CanNavigate()
    {
        return
            IsOnNavMesh &&
            !IsDead &&
            !IsEntangled;
    }

    // =========================================================
    // PLAYER HELPERS
    // =========================================================

    public bool IsPlayerWithinRange(
        float range
    )
    {
        return
            DistanceToPlayer <=
            Mathf.Max(
                0f,
                range
            );
    }

    public bool IsPlayerDetected()
    {
        return
            !IsPlayerDead &&
            IsPlayerWithinRange(
                detectionRange
            );
    }

    public bool HasLostPlayer()
    {
        return
            IsPlayerDead ||
            DistanceToPlayer >
            loseTargetRange;
    }

    public void FacePlayer(
        float rotationSpeed
    )
    {
        if (
            player == null ||
            IsDead ||
            IsEntangled
        )
        {
            return;
        }

        Vector3 directionToPlayer =
            player.position -
            transform.position;

        directionToPlayer.y =
            0f;

        if (
            directionToPlayer.sqrMagnitude <=
            0.001f
        )
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                directionToPlayer.normalized,
                Vector3.up
            );

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                Mathf.Max(
                    0f,
                    rotationSpeed
                ) *
                Time.deltaTime
            );
    }

    public void FacePlayer()
    {
        FacePlayer(
            engagedRotationSpeed
        );
    }

    // =========================================================
    // DISTANCE
    // =========================================================

    public static float GetFlatDistance(
        Vector3 firstPosition,
        Vector3 secondPosition
    )
    {
        firstPosition.y =
            0f;

        secondPosition.y =
            0f;

        return Vector3.Distance(
            firstPosition,
            secondPosition
        );
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -=
                HandleHealthDepleted;
        }

        if (entangleRoutine != null)
        {
            StopCoroutine(
                entangleRoutine
            );

            entangleRoutine =
                null;
        }

        DestroyEntangleEffect();
    }

    // =========================================================
    // VALIDATION
    // =========================================================

    private void OnValidate()
    {
        destroyDelay =
            Mathf.Max(
                0f,
                destroyDelay
            );

        deathEffectLifetime =
            Mathf.Max(
                0.1f,
                deathEffectLifetime
            );

        pickupSpawnHeight =
            Mathf.Max(
                0f,
                pickupSpawnHeight
            );

        patrolRadius =
            Mathf.Max(
                0f,
                patrolRadius
            );

        patrolWaitTime =
            Mathf.Max(
                0f,
                patrolWaitTime
            );

        destinationTolerance =
            Mathf.Max(
                0f,
                destinationTolerance
            );

        navMeshSampleDistance =
            Mathf.Max(
                0.1f,
                navMeshSampleDistance
            );

        patrolSearchAttempts =
            Mathf.Max(
                1,
                patrolSearchAttempts
            );

        detectionRange =
            Mathf.Max(
                0f,
                detectionRange
            );

        loseTargetRange =
            Mathf.Max(
                detectionRange,
                loseTargetRange
            );

        patrolSpeed =
            Mathf.Max(
                0f,
                patrolSpeed
            );

        returnSpeed =
            Mathf.Max(
                0f,
                returnSpeed
            );

        engagedRotationSpeed =
            Mathf.Max(
                0f,
                engagedRotationSpeed
            );
    }
}