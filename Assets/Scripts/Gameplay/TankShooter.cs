using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class TankShooter : MonoBehaviour
{
    [SerializeField] private Transform turret;
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Vector3 launchForwardAxis = Vector3.forward;
    [SerializeField] private Vector3 projectileForwardAxis = Vector3.forward;
    [SerializeField] private float fallbackMuzzleDistance = 2.8f;
    [SerializeField] private float projectileSpeed = 18f;
    [SerializeField] private float shotCooldown = 0.25f;
    [SerializeField] private TankTeam ownerTeam = TankTeam.Player;
    [SerializeField] private int damage = 25;

    public event Action Shot;

    private float lastShotTime = -999f;

    public void Configure(Transform turretTransform, GameObject projectilePrefabOverride, Transform muzzleTransform)
    {
        turret = turretTransform;
        projectilePrefab = projectilePrefabOverride;
        muzzlePoint = muzzleTransform;
        launchForwardAxis = Vector3.forward;
        projectileForwardAxis = Vector3.forward;
    }

    public void ConfigureProjectileSpeed(float newProjectileSpeed)
    {
        projectileSpeed = Mathf.Max(0f, newProjectileSpeed);
    }

    public void ConfigureDamage(TankTeam team, int damageAmount)
    {
        ownerTeam = team;
        damage = Mathf.Max(0, damageAmount);
    }

    private void Reset()
    {
        launchForwardAxis = Vector3.forward;
        projectileForwardAxis = Vector3.forward;
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Fire();
        }
    }

    public void Fire()
    {
        if (projectilePrefab == null || Time.time < lastShotTime + shotCooldown)
        {
            return;
        }

        Transform launchTransform = turret != null ? turret : transform;
        Vector3 direction = TankPlaneMath.Flatten(launchTransform.TransformDirection(launchForwardAxis));
        Vector3 spawnPosition = muzzlePoint != null
            ? muzzlePoint.position
            : launchTransform.position + direction * fallbackMuzzleDistance;

        Quaternion spawnRotation = TankPlaneMath.RotationLookingAlong(direction, projectileForwardAxis);
        GameObject projectile = Instantiate(projectilePrefab, spawnPosition, spawnRotation);

        ProjectileMovement projectileMovement = projectile.GetComponent<ProjectileMovement>();
        if (projectileMovement == null)
        {
            projectileMovement = projectile.AddComponent<ProjectileMovement>();
        }

        projectileMovement.ConfigureDamage(ownerTeam, damage, gameObject);
        projectileMovement.Launch(direction, projectileSpeed, projectileForwardAxis);
        IgnoreTankCollisions(projectile);
        lastShotTime = Time.time;
        Shot?.Invoke();
    }

    private void OnValidate()
    {
        launchForwardAxis = TankPlaneMath.SafeLocalForwardAxis(launchForwardAxis);
        projectileForwardAxis = TankPlaneMath.SafeLocalForwardAxis(projectileForwardAxis);
        fallbackMuzzleDistance = Mathf.Max(0f, fallbackMuzzleDistance);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        shotCooldown = Mathf.Max(0f, shotCooldown);
        damage = Mathf.Max(0, damage);
    }

    private void IgnoreTankCollisions(GameObject projectile)
    {
        Collider[] tankColliders = GetComponentsInChildren<Collider>();
        Collider[] projectileColliders = projectile.GetComponentsInChildren<Collider>();

        foreach (Collider tankCollider in tankColliders)
        {
            foreach (Collider projectileCollider in projectileColliders)
            {
                Physics.IgnoreCollision(tankCollider, projectileCollider, true);
            }
        }
    }
}
