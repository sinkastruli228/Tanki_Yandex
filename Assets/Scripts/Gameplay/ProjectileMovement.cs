using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class ProjectileMovement : MonoBehaviour
{
    public static event System.Action<Vector3> Impacted;
    public static event System.Action<Vector3> DamagedTank;

    [SerializeField] private Vector3 localForwardAxis = Vector3.forward;
    [SerializeField] private float speed = 18f;
    [SerializeField] private float lifetime = 4f;
    [SerializeField] private bool destroyOnCollision = true;
    [SerializeField] private float collisionRadius = 0.8f;
    [SerializeField] private TankTeam ownerTeam = TankTeam.Player;
    [SerializeField] private int damage = 25;
    [SerializeField] private float trailLifetime = 0.18f;
    [SerializeField] private float trailStartWidth = 0.34f;
    [SerializeField] private float trailEndWidth = 0.04f;

    private Vector3 direction;
    private float planeY;
    private float despawnTime;
    private bool launched;
    private GameObject owner;
    private TrailRenderer trail;
    private Transform trailAnchor;
    private static Material trailMaterial;

    private void Awake()
    {
        planeY = transform.position.y;
        direction = TankPlaneMath.Flatten(transform.TransformDirection(localForwardAxis));
        EnsureCollision();
    }

    private void Update()
    {
        if (!launched)
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        Vector3 nextPosition = currentPosition + direction * (speed * Time.deltaTime);
        nextPosition.y = planeY;

        if (TrySweepHit(currentPosition, nextPosition, out Vector3 hitPoint))
        {
            ExplodeAndDestroy(hitPoint);
            return;
        }

        transform.position = nextPosition;

        if (Time.time >= despawnTime)
        {
            ReleaseTrail();
            Destroy(gameObject);
        }
    }

    public void Launch(Vector3 launchDirection, float launchSpeed, Vector3 visualForwardAxis)
    {
        EnsureCollision();
        EnsureTrail();
        direction = TankPlaneMath.Flatten(launchDirection);
        speed = launchSpeed;
        localForwardAxis = TankPlaneMath.SafeLocalForwardAxis(visualForwardAxis);
        planeY = transform.position.y;
        despawnTime = Time.time + lifetime;
        launched = true;
        transform.rotation = TankPlaneMath.RotationLookingAlong(direction, localForwardAxis);
        trail.Clear();
        trail.emitting = true;
    }

    public void ConfigureDamage(TankTeam team, int damageAmount, GameObject ownerObject)
    {
        ownerTeam = team;
        damage = Mathf.Max(0, damageAmount);
        owner = ownerObject;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!launched)
        {
            return;
        }

        if (TryApplyDamage(collision.collider, collision.GetContact(0).point) || destroyOnCollision)
        {
            ExplodeAndDestroy(collision.GetContact(0).point);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!launched)
        {
            return;
        }

        if (TryApplyDamage(other, transform.position) || destroyOnCollision)
        {
            ExplodeAndDestroy(transform.position);
        }
    }

    private bool TryApplyDamage(Collider other, Vector3 hitPoint)
    {
        TankHealth health = other.GetComponentInParent<TankHealth>();
        if (health == null || !health.IsAlive)
        {
            return false;
        }

        if (owner != null && health.gameObject == owner)
        {
            return false;
        }

        if (health.Team == ownerTeam)
        {
            return false;
        }

        health.TakeDamage(damage);
        DamagedTank?.Invoke(hitPoint);
        return true;
    }

    private bool TrySweepHit(Vector3 start, Vector3 end, out Vector3 hitPoint)
    {
        Vector3 travel = end - start;
        float distance = travel.magnitude;
        if (distance <= 0.001f)
        {
            hitPoint = end;
            return false;
        }

        RaycastHit[] hits = Physics.SphereCastAll(
            start,
            collisionRadius,
            travel / distance,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (RaycastHit hit in hits)
        {
            Collider hitCollider = hit.collider;
            if (hitCollider == null || ShouldIgnoreCollider(hitCollider))
            {
                continue;
            }

            TankHealth health = hitCollider.GetComponentInParent<TankHealth>();
            if (health != null)
            {
                if (!health.IsAlive || health.Team == ownerTeam)
                {
                    continue;
                }

                health.TakeDamage(damage);
                DamagedTank?.Invoke(hit.point.sqrMagnitude > 0.001f ? hit.point : end);
            }

            hitPoint = hit.point.sqrMagnitude > 0.001f ? hit.point : end;
            return true;
        }

        if (TryOverlapCapsuleHit(start, end, out hitPoint))
        {
            return true;
        }

        hitPoint = end;
        return false;
    }

    private bool TryOverlapCapsuleHit(Vector3 start, Vector3 end, out Vector3 hitPoint)
    {
        Collider[] overlaps = Physics.OverlapCapsule(
            start,
            end,
            collisionRadius,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(overlaps, (left, right) =>
            DistanceToSegmentSqr(left.bounds.center, start, end).CompareTo(DistanceToSegmentSqr(right.bounds.center, start, end)));

        foreach (Collider hitCollider in overlaps)
        {
            if (hitCollider == null || ShouldIgnoreCollider(hitCollider))
            {
                continue;
            }

            TankHealth health = hitCollider.GetComponentInParent<TankHealth>();
            if (health != null)
            {
                if (!health.IsAlive || health.Team == ownerTeam)
                {
                    continue;
                }

                health.TakeDamage(damage);
                DamagedTank?.Invoke(GetClosestHitPoint(hitCollider, start, end));
            }

            hitPoint = GetClosestHitPoint(hitCollider, start, end);
            return true;
        }

        hitPoint = end;
        return false;
    }

    private static float DistanceToSegmentSqr(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector3 segment = end - start;
        float lengthSqr = segment.sqrMagnitude;
        if (lengthSqr <= 0.0001f)
        {
            return (point - start).sqrMagnitude;
        }

        float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSqr);
        Vector3 closest = start + segment * t;
        return (point - closest).sqrMagnitude;
    }

    private static Vector3 GetClosestHitPoint(Collider hitCollider, Vector3 start, Vector3 fallback)
    {
        Vector3 point = hitCollider.ClosestPoint(start);
        return (point - start).sqrMagnitude > 0.0001f ? point : fallback;
    }

    private bool ShouldIgnoreCollider(Collider other)
    {
        if (other.GetComponentInParent<ProjectileMovement>() != null)
        {
            return true;
        }

        if (owner != null && other.transform.IsChildOf(owner.transform))
        {
            return true;
        }

        return false;
    }

    private void ExplodeAndDestroy(Vector3 position)
    {
        Impacted?.Invoke(position);
        ImpactExplosion.Spawn(position);
        ReleaseTrail();
        Destroy(gameObject);
    }

    private void EnsureCollision()
    {
        ConfigureShadows();
        EnsureTrail();

        Collider[] colliders = GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
        {
            SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = collisionRadius;
            sphereCollider.isTrigger = true;
        }
        else
        {
            foreach (Collider projectileCollider in colliders)
            {
                projectileCollider.isTrigger = true;
            }
        }

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void EnsureTrail()
    {
        if (trailAnchor == null)
        {
            Transform existingAnchor = transform.Find("Projectile Trail");
            if (existingAnchor != null)
            {
                trailAnchor = existingAnchor;
            }
            else
            {
                GameObject trailObject = new GameObject("Projectile Trail");
                trailAnchor = trailObject.transform;
                trailAnchor.SetParent(transform, false);
                trailAnchor.localPosition = Vector3.zero;
                trailAnchor.localRotation = Quaternion.identity;
                trailAnchor.localScale = Vector3.one;
            }
        }

        trail = trail != null ? trail : trailAnchor.GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = trailAnchor.gameObject.AddComponent<TrailRenderer>();
        }

        trail.time = trailLifetime;
        trail.startWidth = trailStartWidth;
        trail.endWidth = trailEndWidth;
        trail.minVertexDistance = 0.08f;
        trail.numCornerVertices = 4;
        trail.numCapVertices = 4;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.emitting = launched;
        trail.colorGradient = CreateTrailGradient();
        trail.sharedMaterial = GetTrailMaterial();
    }

    private void ReleaseTrail()
    {
        if (trail == null)
        {
            return;
        }

        trail.emitting = false;
        trail.transform.SetParent(null, true);
        Destroy(trail.gameObject, trail.time);
        trail = null;
        trailAnchor = null;
    }

    private static Gradient CreateTrailGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(4.5f, 2.0f, 0.45f, 1f), 0f),
                new GradientColorKey(new Color(2.4f, 0.7f, 0.1f, 1f), 0.45f),
                new GradientColorKey(new Color(0.65f, 0.12f, 0.02f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.42f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static Material GetTrailMaterial()
    {
        if (trailMaterial != null)
        {
            return trailMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        trailMaterial = new Material(shader)
        {
            name = "Projectile Glow Trail Material",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = (int)RenderQueue.Transparent
        };

        Color glowColor = new Color(4.5f, 1.6f, 0.25f, 1f);
        if (trailMaterial.HasProperty("_BaseColor"))
        {
            trailMaterial.SetColor("_BaseColor", glowColor);
        }

        if (trailMaterial.HasProperty("_Color"))
        {
            trailMaterial.SetColor("_Color", glowColor);
        }

        if (trailMaterial.HasProperty("_EmissionColor"))
        {
            trailMaterial.EnableKeyword("_EMISSION");
            trailMaterial.SetColor("_EmissionColor", glowColor);
        }

        return trailMaterial;
    }

    private void ConfigureShadows()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private void OnValidate()
    {
        localForwardAxis = TankPlaneMath.SafeLocalForwardAxis(localForwardAxis);
        speed = Mathf.Max(0f, speed);
        lifetime = Mathf.Max(0.01f, lifetime);
        collisionRadius = Mathf.Max(0.01f, collisionRadius);
        damage = Mathf.Max(0, damage);
        trailLifetime = Mathf.Max(0.01f, trailLifetime);
        trailStartWidth = Mathf.Max(0.01f, trailStartWidth);
        trailEndWidth = Mathf.Max(0f, trailEndWidth);
    }
}
