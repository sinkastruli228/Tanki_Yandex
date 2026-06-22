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
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float minRotationSpeed = 30f;
    [SerializeField] private float slowdownAngle = 35f;
    [SerializeField] private float fireAngle = 10f;
    [SerializeField] private float fireRange = 85f;
    [SerializeField] private float fireCooldown = 1.35f;
    [SerializeField] private float projectileSpeed = 108f;
    [SerializeField] private int damage = 25;

    private float lastShotTime = -999f;

    public void Configure(
        TankHealth playerTarget,
        Transform turretTransform,
        Transform muzzleTransform,
        GameObject projectilePrefabOverride,
        float newProjectileSpeed,
        int newDamage,
        float newFireRange,
        Vector3 forwardAxis)
    {
        target = playerTarget;
        turret = turretTransform;
        muzzlePoint = muzzleTransform;
        projectilePrefab = projectilePrefabOverride;
        projectileSpeed = Mathf.Max(0f, newProjectileSpeed);
        damage = Mathf.Max(0, newDamage);
        fireRange = Mathf.Max(0f, newFireRange);
        localForwardAxis = TankPlaneMath.SafeLocalForwardAxis(forwardAxis);
        projectileForwardAxis = TankPlaneMath.SafeLocalForwardAxis(forwardAxis);
    }

    private void Update()
    {
        if (target == null || !target.IsAlive || projectilePrefab == null)
        {
            return;
        }

        Transform aimTurret = turret != null ? turret : transform;
        Vector3 desiredDirection = target.transform.position - aimTurret.position;
        desiredDirection.y = 0f;

        if (desiredDirection.sqrMagnitude < 0.001f || desiredDirection.sqrMagnitude > fireRange * fireRange)
        {
            return;
        }

        float angleAfterTurn = RotateTurret(aimTurret, desiredDirection);
        if (angleAfterTurn <= fireAngle && Time.time >= lastShotTime + fireCooldown)
        {
            Fire(aimTurret, desiredDirection.normalized);
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
        fireCooldown = Mathf.Max(0.01f, fireCooldown);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        damage = Mathf.Max(0, damage);
    }
}
