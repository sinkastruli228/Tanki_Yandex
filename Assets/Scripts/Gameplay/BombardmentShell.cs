using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class BombardmentShell : MonoBehaviour
{
    private static Material fireMaterial;
    private static Material smokeMaterial;
    private static Texture2D softParticleTexture;

    private TankBombardmentUltimate bombardment;
    private Vector3 startPosition;
    private Vector3 impactPosition;
    private Vector3 curveSide;
    private float launchTime;
    private float flightDuration;
    private Transform effectsRoot;
    private TrailRenderer trail;
    private ParticleSystem fire;
    private ParticleSystem smoke;
    private bool counted;
    private bool resolved;

    public static int ActiveCount { get; private set; }
    public static float LastLaunchedVisualScale { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        fireMaterial = null;
        smokeMaterial = null;
        softParticleTexture = null;
        ActiveCount = 0;
        LastLaunchedVisualScale = 0f;
    }

    public void Launch(TankBombardmentUltimate owner, Vector3 target, float duration)
    {
        bombardment = owner;
        startPosition = transform.position;
        impactPosition = target;
        flightDuration = Mathf.Max(0.35f, duration);
        launchTime = Time.time;
        Vector3 direct = impactPosition - startPosition;
        curveSide = Vector3.Cross(direct.normalized, Vector3.up).normalized * Random.Range(-5f, 5f);
        LastLaunchedVisualScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        ActiveCount++;
        counted = true;

        DisableOrdinaryProjectileBehaviour();
        CreateFlightEffects();
    }

    private void Update()
    {
        if (resolved)
        {
            return;
        }

        float t = Mathf.Clamp01((Time.time - launchTime) / flightDuration);
        float eased = t * t * (3f - 2f * t);
        Vector3 arc = curveSide * Mathf.Sin(t * Mathf.PI);
        Vector3 next = Vector3.Lerp(startPosition, impactPosition, eased) + arc;
        Vector3 movement = next - transform.position;
        transform.position = next;
        if (movement.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(movement.normalized, Vector3.up);
        }

        if (t >= 1f)
        {
            ResolveImpact();
        }
    }

    private void DisableOrdinaryProjectileBehaviour()
    {
        ProjectileMovement movement = GetComponent<ProjectileMovement>();
        if (movement != null) movement.enabled = false;
        SpecialSpiralProjectile spiral = GetComponent<SpecialSpiralProjectile>();
        if (spiral != null) spiral.enabled = false;

        foreach (Collider projectileCollider in GetComponentsInChildren<Collider>(true))
        {
            projectileCollider.enabled = false;
        }

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
    }

    private void CreateFlightEffects()
    {
        effectsRoot = new GameObject("Bombardment Shell Trail").transform;
        effectsRoot.SetParent(transform, false);

        trail = effectsRoot.gameObject.AddComponent<TrailRenderer>();
        trail.time = 1.35f;
        trail.minVertexDistance = 0.2f;
        trail.startWidth = 5.2f;
        trail.endWidth = 0.55f;
        trail.numCornerVertices = 3;
        trail.numCapVertices = 3;
        trail.material = GetFireMaterial();
        trail.startColor = new Color(1f, .83f, .24f, .96f);
        trail.endColor = new Color(.95f, .2f, .03f, 0f);
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.Clear();

        fire = CreateParticles(
            effectsRoot,
            "Bombardment Fire Trail",
            GetFireMaterial(),
            new Color(1f, .78f, .14f, .98f),
            new Color(1f, .18f, .02f, 0f),
            0.28f,
            0.68f,
            1.6f,
            4.2f,
            82f);

        smoke = CreateParticles(
            effectsRoot,
            "Bombardment Smoke Trail",
            GetSmokeMaterial(),
            new Color(.24f, .2f, .16f, .82f),
            new Color(.12f, .11f, .10f, 0f),
            1.05f,
            2.15f,
            2.8f,
            7.2f,
            64f);
    }

    private static ParticleSystem CreateParticles(
        Transform parent,
        string objectName,
        Material material,
        Color startColor,
        Color endColor,
        float minLifetime,
        float maxLifetime,
        float minSize,
        float maxSize,
        float emissionRate)
    {
        GameObject particleObject = new GameObject(objectName);
        particleObject.transform.SetParent(parent, false);
        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(minLifetime, maxLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.1f, .55f);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = startColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.025f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = emissionRate;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Clamp(maxSize * .12f, .35f, .9f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 1f) },
            new[] { new GradientAlphaKey(startColor.a, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, .45f),
            new Keyframe(.25f, 1f),
            new Keyframe(1f, 1.6f)));

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        particles.Play();
        return particles;
    }

    private void ResolveImpact()
    {
        resolved = true;
        StopAndDetachEffects();
        bombardment?.ResolveImpact(impactPosition);
        Destroy(gameObject);
    }

    private void StopAndDetachEffects()
    {
        if (effectsRoot == null)
        {
            return;
        }

        effectsRoot.SetParent(null, true);
        if (trail != null)
        {
            trail.emitting = false;
            trail.autodestruct = true;
        }
        if (fire != null) fire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(effectsRoot.gameObject, 3.2f);
        effectsRoot = null;
    }

    private void OnDestroy()
    {
        if (counted)
        {
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
            counted = false;
        }
    }

    private static Material GetFireMaterial()
    {
        if (fireMaterial == null)
        {
            fireMaterial = CreateParticleMaterial("Bombardment Fire Material", new Color(1f, .62f, .08f));
        }
        return fireMaterial;
    }

    private static Material GetSmokeMaterial()
    {
        if (smokeMaterial == null)
        {
            smokeMaterial = CreateParticleMaterial("Bombardment Smoke Material", new Color(.34f, .29f, .23f));
        }
        return smokeMaterial;
    }

    private static Material CreateParticleMaterial(string materialName, Color tint)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = (int)RenderQueue.Transparent
        };
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", GetSoftParticleTexture());
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", GetSoftParticleTexture());
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
        if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return material;
    }

    private static Texture2D GetSoftParticleTexture()
    {
        if (softParticleTexture != null) return softParticleTexture;
        const int size = 32;
        softParticleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Bombardment Soft Particle",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = (x + .5f) / size * 2f - 1f;
            float ny = (y + .5f) / size * 2f - 1f;
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny)), 1.35f);
            softParticleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        softParticleTexture.Apply(false, true);
        return softParticleTexture;
    }
}
