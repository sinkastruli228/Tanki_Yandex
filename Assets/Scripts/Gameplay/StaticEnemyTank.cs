using UnityEngine;

[DisallowMultipleComponent]
public sealed class StaticEnemyTank : MonoBehaviour
{
    [SerializeField] private TankHealth target;
    [SerializeField] private Transform turret;
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Vector3 localForwardAxis = Vector3.forward;
    [SerializeField] private Vector3 projectileForwardAxis = Vector3.forward;
    [SerializeField] private TankController movementController;
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float minRotationSpeed = 30f;
    [SerializeField] private float slowdownAngle = 35f;
    [SerializeField] private float fireAngle = 10f;
    [SerializeField] private float fireRange = 85f;
    [SerializeField] private float detectionRange = 135f;
    [SerializeField] private float hitPursuitRange = 230f;
    [SerializeField] private float preferredDistance = 55f;
    [SerializeField] private float stopDistance = 38f;
    [SerializeField] private float hullTurnAngle = 55f;
    [SerializeField] private float obstacleCheckDistance = 22f;
    [SerializeField] private float obstacleProbeRadius = 1.25f;
    [SerializeField] private float obstacleProbeAngle = 48f;
    [SerializeField] private float obstacleAvoidanceStrength = 2.25f;
    [SerializeField] private float obstacleSlowdown = 0.18f;
    [SerializeField] private float blockedThrottle = 0.12f;
    [SerializeField] private float lineOfFireRadius = 0.35f;
    [SerializeField] private float fireCooldown = 2.5f;
    [SerializeField] private float projectileSpeed = 108f;
    [SerializeField] private int damage = 25;
    [SerializeField] private AudioClip shotClip;
    [SerializeField] private AudioSource shotSource;
    [SerializeField] private float shotVolume = 1f;
    [SerializeField] private MuzzleShotEffect shotEffect;

    private float lastShotTime = -999f;
    private float lastAvoidanceSide = 1f;
    private TankHealth ownHealth;
    private bool pursueBecauseDamaged;

    public void Configure(
        TankHealth playerTarget,
        Transform turretTransform,
        Transform muzzleTransform,
        GameObject projectilePrefabOverride,
        float newProjectileSpeed,
        int newDamage,
        float newFireRange,
        float newDetectionRange,
        float newFireCooldown,
        Vector3 forwardAxis)
    {
        target = playerTarget;
        turret = turretTransform;
        muzzlePoint = muzzleTransform;
        projectilePrefab = projectilePrefabOverride;
        projectileSpeed = Mathf.Max(0f, newProjectileSpeed);
        damage = Mathf.Max(0, newDamage);
        fireRange = Mathf.Max(0f, newFireRange);
        detectionRange = Mathf.Max(fireRange, newDetectionRange);
        fireCooldown = Mathf.Max(0.01f, newFireCooldown);
        preferredDistance = Mathf.Clamp(preferredDistance, stopDistance, detectionRange);
        localForwardAxis = TankPlaneMath.SafeLocalForwardAxis(forwardAxis);
        projectileForwardAxis = TankPlaneMath.SafeLocalForwardAxis(forwardAxis);
        movementController = GetComponent<TankController>();
        ConfigureDamageAggro();
        EnsureShotSource();
    }

    public void ConfigureShotAudio(AudioClip clip)
    {
        shotClip = clip;
        EnsureShotSource();
    }

    public void ConfigureShotEffect(MuzzleShotEffect effect)
    {
        shotEffect = effect;
    }

    private void Update()
    {
        if (target == null || !target.IsAlive || projectilePrefab == null)
        {
            StopMoving();
            return;
        }

        Transform aimTurret = turret != null ? turret : transform;
        Vector3 desiredDirection = target.transform.position - aimTurret.position;
        desiredDirection.y = 0f;
        float sqrDistance = desiredDirection.sqrMagnitude;
        float activeDetectionRange = pursueBecauseDamaged ? hitPursuitRange : detectionRange;

        if (sqrDistance < 0.001f || sqrDistance > activeDetectionRange * activeDetectionRange)
        {
            pursueBecauseDamaged = false;
            StopMoving();
            return;
        }

        DriveTowardTarget(desiredDirection, Mathf.Sqrt(sqrDistance));
        float angleAfterTurn = RotateTurret(aimTurret, desiredDirection);
        if (sqrDistance <= fireRange * fireRange
            && angleAfterTurn <= fireAngle
            && Time.time >= lastShotTime + fireCooldown
            && HasClearLineOfFire(aimTurret, desiredDirection))
        {
            Fire(aimTurret, desiredDirection.normalized);
        }
    }

    private void DriveTowardTarget(Vector3 desiredDirection, float distance)
    {
        if (movementController == null)
        {
            movementController = GetComponent<TankController>();
        }

        if (movementController == null)
        {
            return;
        }

        Vector3 hullForward = TankPlaneMath.Flatten(transform.TransformDirection(localForwardAxis));
        float signedAngle = Vector3.SignedAngle(hullForward, TankPlaneMath.Flatten(desiredDirection), Vector3.up);
        float turn = Mathf.Clamp(signedAngle / Mathf.Max(1f, hullTurnAngle), -1f, 1f);
        float facing = Mathf.InverseLerp(100f, 18f, Mathf.Abs(signedAngle));
        float throttle = distance > preferredDistance ? facing : 0f;

        if (distance < stopDistance)
        {
            throttle = -0.35f;
        }
        else if (throttle > 0.01f)
        {
            float avoidanceTurn = GetObstacleAvoidanceTurn(hullForward, out float slowdownMultiplier, out bool centerBlocked);
            turn = Mathf.Clamp(turn + avoidanceTurn, -1f, 1f);
            throttle *= slowdownMultiplier;
            if (centerBlocked)
            {
                throttle = Mathf.Min(throttle, blockedThrottle);
            }
        }

        movementController.SetExternalInput(throttle, turn);
    }

    private float GetObstacleAvoidanceTurn(Vector3 hullForward, out float slowdownMultiplier, out bool centerBlocked)
    {
        slowdownMultiplier = 1f;

        centerBlocked = TryProbeObstacle(hullForward, out float centerDistance);
        Quaternion leftProbeRotation = Quaternion.AngleAxis(-obstacleProbeAngle, Vector3.up);
        Quaternion rightProbeRotation = Quaternion.AngleAxis(obstacleProbeAngle, Vector3.up);
        bool leftBlocked = TryProbeObstacle(leftProbeRotation * hullForward, out float leftDistance);
        bool rightBlocked = TryProbeObstacle(rightProbeRotation * hullForward, out float rightDistance);

        if (!centerBlocked && !leftBlocked && !rightBlocked)
        {
            return 0f;
        }

        float desiredSide = lastAvoidanceSide;
        if (centerBlocked)
        {
            desiredSide = rightDistance >= leftDistance ? 1f : -1f;
        }
        else if (leftBlocked && !rightBlocked)
        {
            desiredSide = 1f;
        }
        else if (rightBlocked && !leftBlocked)
        {
            desiredSide = -1f;
        }

        lastAvoidanceSide = desiredSide;
        float nearestHit = Mathf.Min(centerDistance, Mathf.Min(leftDistance, rightDistance));
        float danger = 1f - Mathf.Clamp01(nearestHit / Mathf.Max(0.01f, obstacleCheckDistance));
        slowdownMultiplier = Mathf.Lerp(1f, obstacleSlowdown, danger);
        float baseTurn = centerBlocked ? 0.85f : 0.45f;
        return desiredSide * Mathf.Lerp(baseTurn, obstacleAvoidanceStrength, danger);
    }

    private bool TryProbeObstacle(Vector3 direction, out float hitDistance)
    {
        hitDistance = obstacleCheckDistance;
        Vector3 origin = transform.position + Vector3.up * 1.1f;
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            obstacleProbeRadius,
            TankPlaneMath.Flatten(direction),
            obstacleCheckDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        bool hasHit = false;
        foreach (RaycastHit hit in hits)
        {
            if (!IsAvoidanceObstacle(hit.collider))
            {
                continue;
            }

            if (!hasHit || hit.distance < hitDistance)
            {
                hitDistance = hit.distance;
                hasHit = true;
            }
        }

        return hasHit;
    }

    private bool HasClearLineOfFire(Transform launchTransform, Vector3 desiredDirection)
    {
        if (target == null)
        {
            return false;
        }

        Vector3 origin = muzzlePoint != null ? muzzlePoint.position : launchTransform.position;
        Vector3 targetPoint = target.transform.position + Vector3.up * 1.1f;
        Vector3 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
        {
            return true;
        }

        Vector3 direction = toTarget / distance;
        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            lineOfFireRadius,
            direction,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float nearestBlockingDistance = distance;
        bool targetWasHit = false;
        float nearestTargetDistance = distance;

        foreach (RaycastHit hit in hits)
        {
            Collider hitCollider = hit.collider;
            if (hitCollider == null || hitCollider.isTrigger)
            {
                continue;
            }

            if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hitCollider.GetComponentInParent<ProjectileMovement>() != null)
            {
                continue;
            }

            if (hitCollider.transform == target.transform || hitCollider.transform.IsChildOf(target.transform))
            {
                targetWasHit = true;
                nearestTargetDistance = Mathf.Min(nearestTargetDistance, hit.distance);
                continue;
            }

            nearestBlockingDistance = Mathf.Min(nearestBlockingDistance, hit.distance);
        }

        return targetWasHit && nearestTargetDistance <= nearestBlockingDistance + 0.05f;
    }

    private bool IsAvoidanceObstacle(Collider hitCollider)
    {
        if (hitCollider == null || hitCollider.isTrigger)
        {
            return false;
        }

        if (hitCollider is TerrainCollider)
        {
            return false;
        }

        if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
        {
            return false;
        }

        if (target != null && (hitCollider.transform == target.transform || hitCollider.transform.IsChildOf(target.transform)))
        {
            return false;
        }

        if (hitCollider.GetComponentInParent<ProjectileMovement>() != null)
        {
            return false;
        }

        return true;
    }

    private void StopMoving()
    {
        if (movementController == null)
        {
            movementController = GetComponent<TankController>();
        }

        if (movementController != null)
        {
            movementController.SetExternalInput(0f, 0f);
        }
    }

    private void ConfigureDamageAggro()
    {
        TankHealth health = GetComponent<TankHealth>();
        if (ownHealth == health)
        {
            return;
        }

        if (ownHealth != null)
        {
            ownHealth.Damaged -= HandleDamaged;
        }

        ownHealth = health;
        if (ownHealth != null)
        {
            ownHealth.Damaged += HandleDamaged;
        }
    }

    private void HandleDamaged(TankHealth damagedHealth, int damageAmount)
    {
        pursueBecauseDamaged = true;
    }

    private void OnDestroy()
    {
        if (ownHealth != null)
        {
            ownHealth.Damaged -= HandleDamaged;
        }
    }

    private float RotateTurret(Transform aimTurret, Vector3 desiredDirection)
    {
        Quaternion targetRotation = TankPlaneMath.RotationLookingAlong(desiredDirection, localForwardAxis);
        float angle = Quaternion.Angle(aimTurret.rotation, targetRotation);
        float slowdownT = Mathf.Clamp01(angle / slowdownAngle);
        float easedT = 1f - (1f - slowdownT) * (1f - slowdownT);
        float currentRotationSpeed = Mathf.Lerp(minRotationSpeed, rotationSpeed, easedT);
        aimTurret.rotation = Quaternion.RotateTowards(aimTurret.rotation, targetRotation, currentRotationSpeed * Time.deltaTime);
        return Quaternion.Angle(aimTurret.rotation, targetRotation);
    }

    private void Fire(Transform launchTransform, Vector3 direction)
    {
        Vector3 spawnPosition = muzzlePoint != null ? muzzlePoint.position : launchTransform.position + direction * 2.8f;
        Quaternion spawnRotation = TankPlaneMath.RotationLookingAlong(direction, projectileForwardAxis);
        GameObject projectile = Instantiate(projectilePrefab, spawnPosition, spawnRotation);

        ProjectileMovement projectileMovement = projectile.GetComponent<ProjectileMovement>();
        if (projectileMovement == null)
        {
            projectileMovement = projectile.AddComponent<ProjectileMovement>();
        }

        projectileMovement.ConfigureDamage(TankTeam.Enemy, damage, gameObject);
        projectileMovement.Launch(direction, projectileSpeed, projectileForwardAxis);
        IgnoreOwnCollisions(projectile);
        lastShotTime = Time.time;
        PlayShotAudio(spawnPosition);
        if (shotEffect != null)
        {
            shotEffect.Play();
        }
    }

    private void PlayShotAudio(Vector3 position)
    {
        if (shotSource == null || shotClip == null)
        {
            return;
        }

        shotSource.transform.position = position;
        shotSource.PlayOneShot(shotClip, shotVolume);
    }

    private void EnsureShotSource()
    {
        Transform parent = muzzlePoint != null ? muzzlePoint : transform;
        if (shotSource == null || shotSource.transform.parent != parent)
        {
            Transform existing = parent.Find("Shot Audio");
            GameObject sourceObject = existing != null ? existing.gameObject : new GameObject("Shot Audio");
            sourceObject.transform.SetParent(parent, false);
            sourceObject.transform.localPosition = Vector3.zero;
            sourceObject.transform.localRotation = Quaternion.identity;
            shotSource = sourceObject.GetComponent<AudioSource>();
            if (shotSource == null)
            {
                shotSource = sourceObject.AddComponent<AudioSource>();
            }
        }

        shotSource.playOnAwake = false;
        shotSource.loop = false;
        shotSource.spatialBlend = 1f;
        shotSource.rolloffMode = AudioRolloffMode.Linear;
        shotSource.minDistance = 28f;
        shotSource.maxDistance = 260f;
        shotSource.dopplerLevel = 0.1f;
    }

    private void IgnoreOwnCollisions(GameObject projectile)
    {
        Collider[] ownColliders = GetComponentsInChildren<Collider>();
        Collider[] projectileColliders = projectile.GetComponentsInChildren<Collider>();

        foreach (Collider ownCollider in ownColliders)
        {
            foreach (Collider projectileCollider in projectileColliders)
            {
                Physics.IgnoreCollision(ownCollider, projectileCollider, true);
            }
        }
    }

    private void OnValidate()
    {
        localForwardAxis = TankPlaneMath.SafeLocalForwardAxis(localForwardAxis);
        projectileForwardAxis = TankPlaneMath.SafeLocalForwardAxis(projectileForwardAxis);
        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        minRotationSpeed = Mathf.Clamp(minRotationSpeed, 0f, rotationSpeed);
        slowdownAngle = Mathf.Max(0.01f, slowdownAngle);
        fireAngle = Mathf.Max(0f, fireAngle);
        fireRange = Mathf.Max(0f, fireRange);
        detectionRange = Mathf.Max(fireRange, detectionRange);
        hitPursuitRange = Mathf.Max(detectionRange, hitPursuitRange);
        stopDistance = Mathf.Max(0f, stopDistance);
        preferredDistance = Mathf.Clamp(preferredDistance, stopDistance, detectionRange);
        hullTurnAngle = Mathf.Max(1f, hullTurnAngle);
        obstacleCheckDistance = Mathf.Max(1f, obstacleCheckDistance);
        obstacleProbeRadius = Mathf.Max(0.05f, obstacleProbeRadius);
        obstacleProbeAngle = Mathf.Clamp(obstacleProbeAngle, 1f, 89f);
        obstacleAvoidanceStrength = Mathf.Max(0f, obstacleAvoidanceStrength);
        obstacleSlowdown = Mathf.Clamp01(obstacleSlowdown);
        blockedThrottle = Mathf.Clamp01(blockedThrottle);
        lineOfFireRadius = Mathf.Max(0.01f, lineOfFireRadius);
        fireCooldown = Mathf.Max(0.01f, fireCooldown);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        damage = Mathf.Max(0, damage);
        shotVolume = Mathf.Clamp01(shotVolume);
    }
}
