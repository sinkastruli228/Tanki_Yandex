using UnityEngine;

[DisallowMultipleComponent]
public sealed class SpecialSpiralProjectile : MonoBehaviour
{
    [SerializeField] private float flightDuration = 1.15f;
    [SerializeField] private float arcHeight = 5.5f;
    [SerializeField] private float spiralRadius = 1.5f;
    [SerializeField] private float spiralTurns = 2.25f;

    private TankHealth target;
    private TankCombatRewards rewards;
    private int damage;
    private Vector3 startPosition;
    private Vector3 lastTargetPoint;
    private float launchTime;
    private TrailRenderer trail;

    public void Launch(TankHealth lockedTarget, TankCombatRewards combatRewards, int damageAmount)
    {
        target = lockedTarget;
        rewards = combatRewards;
        damage = Mathf.Max(0, damageAmount);
        startPosition = transform.position;
        lastTargetPoint = TankSpecialWeapon.GetTargetPoint(target);
        launchTime = Time.time;
        ConfigureTrail();
    }

    private void Update()
    {
        if (target != null && target.IsAlive)
        {
            lastTargetPoint = TankSpecialWeapon.GetTargetPoint(target);
        }

        float t = Mathf.Clamp01((Time.time - launchTime) / Mathf.Max(0.05f, flightDuration));
        Vector3 direct = lastTargetPoint - startPosition;
        Vector3 forward = direct.sqrMagnitude > 0.001f ? direct.normalized : transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
        if (side.sqrMagnitude < 0.001f)
        {
            side = Vector3.right;
        }

        Vector3 upAroundPath = Vector3.Cross(forward, side).normalized;
        float angle = t * spiralTurns * Mathf.PI * 2f;
        float radius = spiralRadius * Mathf.Sin(Mathf.PI * t) * (1f - t * 0.35f);
        Vector3 spiral = (side * Mathf.Cos(angle) + upAroundPath * Mathf.Sin(angle)) * radius;
        Vector3 arc = Vector3.up * (Mathf.Sin(Mathf.PI * t) * arcHeight);
        Vector3 nextPosition = Vector3.Lerp(startPosition, lastTargetPoint, t) + arc + spiral;

        Vector3 movement = nextPosition - transform.position;
        transform.position = nextPosition;
        if (movement.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(movement.normalized, Vector3.up);
        }

        if (t >= 1f)
        {
            HitTarget();
        }
    }

    private void HitTarget()
    {
        bool fatal = false;
        if (target != null && target.IsAlive)
        {
            target.TakeDamage(damage);
            fatal = !target.IsAlive;
            ProjectileMovement.NotifyTankDamaged(lastTargetPoint, fatal);
            if (fatal && rewards != null)
            {
                rewards.RegisterKill();
            }
        }

        ImpactExplosion.Spawn(lastTargetPoint);
        if (trail != null)
        {
            trail.transform.SetParent(null, true);
            trail.autodestruct = true;
            trail.emitting = false;
        }

        Destroy(gameObject);
    }

    private void ConfigureTrail()
    {
        trail = GetComponentInChildren<TrailRenderer>(true);
        if (trail == null)
        {
            trail = gameObject.AddComponent<TrailRenderer>();
        }

        trail.enabled = true;
        trail.emitting = true;
        trail.time = 0.45f;
        trail.startWidth = 0.5f;
        trail.endWidth = 0.04f;
        trail.startColor = new Color(0.2f, 0.9f, 1f, 1f);
        trail.endColor = new Color(0.1f, 0.35f, 1f, 0f);
        trail.Clear();
    }
}
