using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class TankSpecialWeapon : MonoBehaviour
{
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private TankCombatRewards rewards;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private float targetSelectionRadius = 90f;

    private TankHealth currentTarget;
    private TankShieldUltimate shieldUltimate;
    private TankBombardmentUltimate bombardmentUltimate;

    public TankHealth CurrentTarget => currentTarget;
    public bool IsShieldActive => shieldUltimate != null && shieldUltimate.IsActive;
    public bool IsBombardmentPlanning => bombardmentUltimate != null && bombardmentUltimate.IsPlanning;
    public bool IsBombardmentActive => bombardmentUltimate != null && bombardmentUltimate.IsActive;

    public void Configure(Transform muzzle, GameObject projectile, TankCombatRewards combatRewards, Camera camera)
    {
        if (rewards != null)
        {
            rewards.SpecialActivationRequested -= HandleSpecialActivationRequested;
        }

        muzzlePoint = muzzle;
        projectilePrefab = projectile;
        rewards = combatRewards;
        aimCamera = camera;
        if (rewards != null)
        {
            rewards.SpecialActivationRequested += HandleSpecialActivationRequested;
        }

        shieldUltimate = GetComponent<TankShieldUltimate>();
        if (shieldUltimate == null)
        {
            shieldUltimate = gameObject.AddComponent<TankShieldUltimate>();
        }

        bombardmentUltimate = GetComponent<TankBombardmentUltimate>();
        if (bombardmentUltimate == null)
        {
            bombardmentUltimate = gameObject.AddComponent<TankBombardmentUltimate>();
        }
        bombardmentUltimate.Configure(rewards, projectilePrefab, aimCamera);
    }

    private void Update()
    {
        if (rewards == null || !rewards.IsSpecialArmed)
        {
            currentTarget = null;
            return;
        }

        if (TankUltimateLoadout.Selected == TankUltimateLoadout.ShieldSlot
            || TankUltimateLoadout.Selected == TankUltimateLoadout.BombardmentSlot)
        {
            currentTarget = null;
            return;
        }

        currentTarget = FindTargetUnderCursor();
    }

    public bool TryHandleFire()
    {
        if (rewards == null || !rewards.IsSpecialArmed)
        {
            return false;
        }

        if (TankUltimateLoadout.Selected == TankUltimateLoadout.ShieldSlot)
        {
            return false;
        }

        if (TankUltimateLoadout.Selected == TankUltimateLoadout.BombardmentSlot)
        {
            return true;
        }

        // Once the special shell is armed, ordinary fire is held until a target is acquired.
        if (currentTarget == null || !currentTarget.IsAlive || muzzlePoint == null)
        {
            return true;
        }

        GameObject projectile = projectilePrefab != null
            ? Instantiate(projectilePrefab, muzzlePoint.position, muzzlePoint.rotation)
            : GameObject.CreatePrimitive(PrimitiveType.Sphere);

        projectile.name = "Special Spiral Projectile";
        projectile.transform.position = muzzlePoint.position;
        projectile.transform.localScale *= 1.35f;

        ProjectileMovement ordinaryMovement = projectile.GetComponent<ProjectileMovement>();
        if (ordinaryMovement != null)
        {
            ordinaryMovement.enabled = false;
        }

        foreach (Collider projectileCollider in projectile.GetComponentsInChildren<Collider>(true))
        {
            projectileCollider.enabled = false;
        }

        Rigidbody body = projectile.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        SpecialSpiralProjectile spiralProjectile = projectile.GetComponent<SpecialSpiralProjectile>();
        if (spiralProjectile == null)
        {
            spiralProjectile = projectile.AddComponent<SpecialSpiralProjectile>();
        }

        TankHealth lockedTarget = currentTarget;
        if (!rewards.ConsumeArmedShot())
        {
            Destroy(projectile);
            return true;
        }

        spiralProjectile.Launch(lockedTarget, rewards, 100);
        TankShooter shooter = GetComponent<TankShooter>();
        if (shooter != null)
        {
            shooter.NotifySpecialShotFired();
        }

        currentTarget = null;
        return true;
    }

    private void ActivateShield()
    {
        shieldUltimate = shieldUltimate != null ? shieldUltimate : GetComponent<TankShieldUltimate>();
        if (shieldUltimate == null || shieldUltimate.IsActive)
        {
            return;
        }

        if (shieldUltimate.Activate())
        {
            rewards.ConsumeArmedShot();
        }
    }

    private void HandleSpecialActivationRequested()
    {
        if (TankUltimateLoadout.Selected == TankUltimateLoadout.ShieldSlot)
        {
            currentTarget = null;
            ActivateShield();
            return;
        }

        if (TankUltimateLoadout.Selected == TankUltimateLoadout.BombardmentSlot)
        {
            currentTarget = null;
            bombardmentUltimate = bombardmentUltimate != null ? bombardmentUltimate : GetComponent<TankBombardmentUltimate>();
            if (bombardmentUltimate == null || !bombardmentUltimate.OpenPlanner())
            {
                rewards?.CancelSpecialActivation();
            }
        }
    }

    private void OnDestroy()
    {
        if (rewards != null)
        {
            rewards.SpecialActivationRequested -= HandleSpecialActivationRequested;
        }
    }

    private TankHealth FindTargetUnderCursor()
    {
        Camera camera = aimCamera != null ? aimCamera : Camera.main;
        Mouse mouse = Mouse.current;
        if (camera == null || mouse == null)
        {
            return null;
        }

        Vector2 cursorPosition = mouse.position.ReadValue();
        float bestDistanceSqr = targetSelectionRadius * targetSelectionRadius;
        TankHealth bestTarget = null;
        TankHealth[] tanks = FindObjectsByType<TankHealth>(FindObjectsInactive.Exclude);

        foreach (TankHealth tank in tanks)
        {
            if (tank == null || !tank.IsAlive || tank.Team != TankTeam.Enemy)
            {
                continue;
            }

            Vector3 targetPoint = GetTargetPoint(tank);
            Vector3 screenPoint = camera.WorldToScreenPoint(targetPoint);
            if (screenPoint.z <= 0f)
            {
                continue;
            }

            float distanceSqr = ((Vector2)screenPoint - cursorPosition).sqrMagnitude;
            if (distanceSqr <= bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestTarget = tank;
            }
        }

        return bestTarget;
    }

    public static Vector3 GetTargetPoint(TankHealth target)
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        Collider targetCollider = target.GetComponentInChildren<Collider>();
        return targetCollider != null
            ? targetCollider.bounds.center
            : target.transform.position + Vector3.up * 1.5f;
    }
}
