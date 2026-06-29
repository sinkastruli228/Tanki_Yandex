using UnityEngine;

[DisallowMultipleComponent]
public sealed class TankDeathEffect : MonoBehaviour
{
    private const float ConfiguredTurretUpForce = 20.25f;
    private const float ConfiguredTurretSideForce = ConfiguredTurretUpForce * 0.25f;
    private const float ConfiguredTurretTorque = 4.5f;
    private const float ConfiguredDeathShakeIntensity = 1.7f;

    [SerializeField] private TankHealth health;
    [SerializeField] private Transform turret;
    [SerializeField] private bool destroyTankAfterDeath = true;
    [SerializeField] private float destroyDelay = 7f;
    [SerializeField] private float turretUpForce = 0.05f;
    [SerializeField] private float turretSideForce = 0.03f;
    [SerializeField] private float turretTorque = 0.5f;
    [SerializeField] private float deathShakeIntensity = 1.7f;
    [SerializeField] private float smokeFadeTime = 1.8f;

    private bool hasDied;

    public void Configure(TankHealth tankHealth, Transform turretTransform, bool shouldDestroyTankAfterDeath)
    {
        if (health != tankHealth)
        {
            Unsubscribe();
            health = tankHealth;
            Subscribe();
        }

        turret = turretTransform;
        destroyTankAfterDeath = shouldDestroyTankAfterDeath;
        turretUpForce = ConfiguredTurretUpForce;
        turretSideForce = ConfiguredTurretSideForce;
        turretTorque = ConfiguredTurretTorque;
        deathShakeIntensity = ConfiguredDeathShakeIntensity;
        hasDied = health != null && !health.IsAlive;
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (health != null)
        {
            health.Died -= HandleDied;
            health.Died += HandleDied;
        }
    }

    private void Unsubscribe()
    {
        if (health != null)
        {
            health.Died -= HandleDied;
        }
    }

    private void HandleDied(TankHealth deadHealth)
    {
        if (hasDied)
        {
            return;
        }

        hasDied = true;
        DisableTankSystems();
        Vector3 explosionPosition = GetExplosionPosition();
        PrepareTurretForExplosion();
        ImpactExplosion.SpawnTankDeath(explosionPosition);
        AddDeathCameraShake();
        SpawnPersistentBodySmoke(explosionPosition);
        LaunchTurret(explosionPosition);

        if (destroyTankAfterDeath)
        {
            StartCoroutine(DestroyAfterSmokeFade());
        }
    }

    private System.Collections.IEnumerator DestroyAfterSmokeFade()
    {
        float waitBeforeFade = Mathf.Max(0f, destroyDelay - smokeFadeTime);
        if (waitBeforeFade > 0f)
        {
            yield return new WaitForSeconds(waitBeforeFade);
        }

        TankDeathSmoke[] smokes = GetComponentsInChildren<TankDeathSmoke>(true);
        foreach (TankDeathSmoke smoke in smokes)
        {
            smoke.BeginFadeOut(smokeFadeTime);
        }

        if (smokeFadeTime > 0f)
        {
            yield return new WaitForSeconds(smokeFadeTime);
        }

        if (this != null)
        {
            Destroy(gameObject);
        }
    }

    private void AddDeathCameraShake()
    {
        TopDownCameraFollow.ShakeAllExplosions(deathShakeIntensity);
    }

    private void SpawnPersistentBodySmoke(Vector3 center)
    {
        GameObject smokeObject = new GameObject("Tank Body Persistent Smoke");
        smokeObject.transform.SetParent(transform, true);
        smokeObject.transform.position = center + Vector3.up * 0.3f;

        TankDeathSmoke smoke = smokeObject.AddComponent<TankDeathSmoke>();
        smoke.Configure();
    }

    private void PrepareTurretForExplosion()
    {
        if (turret == null)
        {
            return;
        }

        Rigidbody[] turretBodies = turret.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody turretBody in turretBodies)
        {
            turretBody.linearVelocity = Vector3.zero;
            turretBody.angularVelocity = Vector3.zero;
            turretBody.isKinematic = true;
        }
    }

    private void DisableTankSystems()
    {
        TankController controller = GetComponent<TankController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        TankShooter shooter = GetComponent<TankShooter>();
        if (shooter != null)
        {
            shooter.enabled = false;
        }

        TankTurretAim turretAim = GetComponent<TankTurretAim>();
        if (turretAim != null)
        {
            turretAim.enabled = false;
        }

        StaticEnemyTank enemyTank = GetComponent<StaticEnemyTank>();
        if (enemyTank != null)
        {
            enemyTank.enabled = false;
        }

        TankAudioController audioController = GetComponent<TankAudioController>();
        if (audioController != null)
        {
            audioController.enabled = false;
        }

        TankTrackDust dust = GetComponent<TankTrackDust>();
        if (dust != null)
        {
            dust.enabled = false;
        }
    }

    private Vector3 GetExplosionPosition()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return transform.position + Vector3.up * 1.2f;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds.center;
    }

    private void LaunchTurret(Vector3 explosionPosition)
    {
        if (turret == null || turret == transform)
        {
            return;
        }

        Transform detachedTurret = turret;
        Collider[] tankColliders = GetComponentsInChildren<Collider>(true);
        detachedTurret.SetParent(null, true);
        detachedTurret.position += Vector3.up * 0.45f;
        EnsureTurretCollider(detachedTurret.gameObject);
        IgnoreTankBodyCollisions(detachedTurret, tankColliders);
        if (detachedTurret.GetComponent<TurretDebris>() == null)
        {
            detachedTurret.gameObject.AddComponent<TurretDebris>();
        }

        Rigidbody turretBody = detachedTurret.GetComponent<Rigidbody>();
        if (turretBody == null)
        {
            turretBody = detachedTurret.gameObject.AddComponent<Rigidbody>();
        }

        turretBody.useGravity = true;
        turretBody.isKinematic = false;
        turretBody.mass = 24f;
        turretBody.linearDamping = 0.25f;
        turretBody.angularDamping = 0.35f;
        turretBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
        turretBody.linearVelocity = Vector3.zero;
        turretBody.angularVelocity = Vector3.zero;

        Vector3 sideDirection = TankPlaneMath.Flatten(transform.right);
        turretBody.linearVelocity = Vector3.up * turretUpForce + sideDirection * turretSideForce;
        turretBody.angularVelocity = Random.onUnitSphere * turretTorque;
        if (destroyTankAfterDeath)
        {
            Destroy(detachedTurret.gameObject, destroyDelay);
        }
    }

    private static void IgnoreTankBodyCollisions(Transform detachedTurret, Collider[] tankColliders)
    {
        Collider[] turretColliders = detachedTurret.GetComponentsInChildren<Collider>(true);
        foreach (Collider turretCollider in turretColliders)
        {
            if (turretCollider == null)
            {
                continue;
            }

            foreach (Collider tankCollider in tankColliders)
            {
                if (tankCollider == null || tankCollider == turretCollider || tankCollider.transform.IsChildOf(detachedTurret))
                {
                    continue;
                }

                Physics.IgnoreCollision(turretCollider, tankCollider, true);
            }
        }
    }

    private static void EnsureTurretCollider(GameObject turretObject)
    {
        Collider existingCollider = turretObject.GetComponentInChildren<Collider>();
        if (existingCollider != null && !existingCollider.isTrigger)
        {
            return;
        }

        if (!TryGetLocalRendererBounds(turretObject, out Bounds localBounds))
        {
            return;
        }

        BoxCollider boxCollider = turretObject.GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = turretObject.AddComponent<BoxCollider>();
        }

        boxCollider.center = localBounds.center;
        boxCollider.size = localBounds.size;
        boxCollider.isTrigger = false;
    }

    private static bool TryGetLocalRendererBounds(GameObject root, out Bounds localBounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        localBounds = default;
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            Bounds bounds = renderer.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 corner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        Vector3 localCorner = root.transform.InverseTransformPoint(corner);

                        if (!hasBounds)
                        {
                            localBounds = new Bounds(localCorner, Vector3.zero);
                            hasBounds = true;
                            continue;
                        }

                        localBounds.Encapsulate(localCorner);
                    }
                }
            }
        }

        return hasBounds;
    }

    private void OnValidate()
    {
        destroyDelay = Mathf.Max(0f, destroyDelay);
        turretUpForce = Mathf.Max(0f, turretUpForce);
        turretSideForce = Mathf.Max(0f, turretSideForce);
        turretTorque = Mathf.Max(0f, turretTorque);
        deathShakeIntensity = Mathf.Max(0f, deathShakeIntensity);
        smokeFadeTime = Mathf.Max(0f, smokeFadeTime);
    }
}

internal sealed class TankDeathSmoke : MonoBehaviour
{
    private static Texture2D smokeTexture;
    private static Mesh smokeMesh;
    private ParticleSystem smoke;

    public void Configure()
    {
        smoke = gameObject.AddComponent<ParticleSystem>();
        smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = smoke.main;
        main.playOnAwake = false;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 1.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(2.4f, 4.8f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.19f, 0.17f, 0.15f, 0.48f),
            new Color(0.06f, 0.055f, 0.05f, 0.64f));
        main.gravityModifier = -0.03f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = smoke.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(7f, 12f);

        ParticleSystem.ShapeModule shape = smoke.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.75f;
        shape.position = Vector3.up * 0.35f;

        ParticleSystem.VelocityOverLifetimeModule velocity = smoke.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = new ParticleSystem.MinMaxCurve(0.65f, 1.8f);
        velocity.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = smoke.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.24f, 0.21f, 0.18f), 0f),
                new GradientColorKey(new Color(0.08f, 0.075f, 0.07f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.58f, 0.18f),
                new GradientAlphaKey(0.42f, 0.68f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = smoke.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(0.4f, 1.1f),
            new Keyframe(1f, 1.65f)));

        ParticleSystemRenderer renderer = smoke.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = GetSmokeMesh();
        renderer.material = CreateSmokeMaterial();
        renderer.sortingFudge = -3f;

        smoke.Play();
    }

    public void BeginFadeOut(float fadeTime)
    {
        if (smoke == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = smoke.emission;
        emission.enabled = false;
        Destroy(gameObject, Mathf.Max(0.1f, fadeTime) + 3.5f);
    }

    private static Material CreateSmokeMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader)
        {
            name = "Tank Death Persistent Smoke Material",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = 3000
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", new Color(0.16f, 0.14f, 0.12f, 0.72f));
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", new Color(0.16f, 0.14f, 0.12f, 0.72f));
        }

        Texture2D texture = GetSmokeTexture();
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 0f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        }

        material.SetOverrideTag("RenderType", "Transparent");
        material.SetOverrideTag("Queue", "Transparent");
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        return material;
    }

    private static Mesh GetSmokeMesh()
    {
        if (smokeMesh != null)
        {
            return smokeMesh;
        }

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        smokeMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
        smokeMesh.hideFlags = HideFlags.HideAndDontSave;
        Destroy(sphere);
        return smokeMesh;
    }

    private static Texture2D GetSmokeTexture()
    {
        if (smokeTexture != null)
        {
            return smokeTexture;
        }

        const int size = 96;
        smokeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Tank Death Soft Smoke Texture",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.46f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(distance));
                alpha *= alpha;
                smokeTexture.SetPixel(x, y, new Color(0.18f, 0.16f, 0.14f, alpha));
            }
        }

        smokeTexture.Apply(false, true);
        return smokeTexture;
    }
}
