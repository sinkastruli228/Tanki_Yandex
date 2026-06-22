using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class MuzzleShotEffect : MonoBehaviour
{
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private TankShooter shooter;
    [SerializeField] private Vector3 localForwardAxis = Vector3.forward;

    private static Material tracerMaterial;
    private static Material smokeMaterial;
    private static Material flashMaterial;
    private static Texture2D softTexture;
    private TankShooter subscribedShooter;

    public void Configure(Transform muzzle, Vector3 forwardAxis, TankShooter sourceShooter = null)
    {
        muzzlePoint = muzzle;
        localForwardAxis = TankPlaneMath.SafeLocalForwardAxis(forwardAxis);
        shooter = sourceShooter;
        SubscribeToShooter(shooter);
    }

    public void Play()
    {
        Transform origin = muzzlePoint != null ? muzzlePoint : transform;
        Vector3 direction = TankPlaneMath.Flatten(origin.TransformDirection(localForwardAxis));
        if (direction.sqrMagnitude < 0.001f)
        {
            direction = origin.forward;
        }

        GameObject root = new GameObject("Muzzle Shot Effect");
        root.transform.position = origin.position;
        root.transform.rotation = TankPlaneMath.RotationLookingAlong(direction, localForwardAxis);

        CreateTracerCone(root.transform);
        CreateBrightSmoke(muzzlePoint != null ? muzzlePoint : root.transform);
        CreateFlash(root.transform);
        CreateFlashLight(root.transform);
        Destroy(root, 0.9f);
    }

    private void OnEnable()
    {
        SubscribeToShooter(shooter);
    }

    private void OnDisable()
    {
        SubscribeToShooter(null);
    }

    private void CreateTracerCone(Transform parent)
    {
        const int tracerCount = 16;
        for (int i = 0; i < tracerCount; i++)
        {
            Vector2 spread = Random.insideUnitCircle * Random.Range(0.16f, 0.42f);
            Vector3 velocity = new Vector3(spread.x, spread.y, 1f).normalized * Random.Range(42f, 68f);

            GameObject tracer = new GameObject("Muzzle Cone Tracer");
            tracer.transform.SetParent(parent, false);
            tracer.transform.localPosition = Vector3.forward * Random.Range(0f, 0.24f);

            TrailRenderer trail = tracer.AddComponent<TrailRenderer>();
            trail.time = Random.Range(0.34f, 0.5f);
            trail.startWidth = Random.Range(0.46f, 0.68f);
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.025f;
            trail.numCornerVertices = 4;
            trail.numCapVertices = 4;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.colorGradient = CreateTracerGradient();
            trail.sharedMaterial = GetTracerMaterial();

            ShotTracer motion = tracer.AddComponent<ShotTracer>();
            motion.Configure(velocity, Random.Range(0.26f, 0.38f), trail);
        }
    }

    private void CreateBrightSmoke(Transform parent)
    {
        ParticleSystem smoke = new GameObject("Muzzle Bright Smoke").AddComponent<ParticleSystem>();
        smoke.transform.SetParent(parent, false);
        smoke.transform.localPosition = Vector3.forward * 0.18f;
        smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = smoke.main;
        main.playOnAwake = false;
        main.duration = 0.12f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.52f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.4f, 5.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.65f, 1.55f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.86f, 0.34f, 0.85f),
            new Color(0.72f, 0.58f, 0.42f, 0.48f));
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = smoke.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = smoke.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.12f;
        shape.length = 0.8f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = smoke.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.36f), 0f),
                new GradientColorKey(new Color(0.68f, 0.58f, 0.46f), 0.42f),
                new GradientColorKey(new Color(0.32f, 0.29f, 0.25f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.88f, 0f),
                new GradientAlphaKey(0.42f, 0.38f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = smoke.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(0.24f, 1.2f),
            new Keyframe(1f, 1.7f)));

        ParticleSystemRenderer renderer = smoke.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = GetSmokeMaterial();
        renderer.sortingFudge = 4f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        smoke.Emit(14);
        smoke.Play();
    }

    private void CreateFlash(Transform parent)
    {
        ParticleSystem flash = new GameObject("Muzzle Hot Flash").AddComponent<ParticleSystem>();
        flash.transform.SetParent(parent, false);
        flash.transform.localPosition = Vector3.forward * 0.22f;
        flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = flash.main;
        main.playOnAwake = false;
        main.duration = 0.04f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.14f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startColor = new Color(1f, 0.96f, 0.28f, 0.96f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = flash.emission;
        emission.rateOverTime = 0f;

        ParticleSystemRenderer renderer = flash.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = GetFlashMaterial();
        renderer.sortingFudge = 8f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        flash.Emit(5);
        flash.Play();
    }

    private void CreateFlashLight(Transform parent)
    {
        GameObject lightObject = new GameObject("Muzzle Flash Light");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = Vector3.forward * 0.3f;

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.62f, 0.12f);
        light.range = 8f;
        light.intensity = 4.5f;
        light.shadows = LightShadows.None;

        ShotLightFade fade = lightObject.AddComponent<ShotLightFade>();
        fade.Configure(0.1f, 4.5f);
    }

    private void SubscribeToShooter(TankShooter targetShooter)
    {
        if (subscribedShooter == targetShooter)
        {
            return;
        }

        if (subscribedShooter != null)
        {
            subscribedShooter.Shot -= Play;
        }

        subscribedShooter = targetShooter;
        if (subscribedShooter != null)
        {
            subscribedShooter.Shot += Play;
        }
    }

    private static Gradient CreateTracerGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(5f, 2.2f, 0.4f), 0f),
                new GradientColorKey(new Color(2.3f, 0.56f, 0.08f), 0.55f),
                new GradientColorKey(new Color(0.7f, 0.08f, 0.02f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private static Material GetTracerMaterial()
    {
        if (tracerMaterial != null)
        {
            return tracerMaterial;
        }

        tracerMaterial = CreateParticleMaterial("Muzzle Cone Tracer Material", new Color(9f, 3.2f, 0.45f, 1f), true);
        return tracerMaterial;
    }

    private static Material GetSmokeMaterial()
    {
        if (smokeMaterial != null)
        {
            return smokeMaterial;
        }

        smokeMaterial = CreateParticleMaterial("Muzzle Glowing Smoke Material", new Color(1f, 0.72f, 0.26f, 0.75f), true);
        return smokeMaterial;
    }

    private static Material GetFlashMaterial()
    {
        if (flashMaterial != null)
        {
            return flashMaterial;
        }

        flashMaterial = CreateParticleMaterial("Muzzle Hot Flash Material", new Color(1f, 0.92f, 0.22f, 1f), true);
        return flashMaterial;
    }

    private static Material CreateParticleMaterial(string materialName, Color color, bool additive)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = (int)RenderQueue.Transparent
        };

        material.SetOverrideTag("RenderType", "Transparent");
        material.SetOverrideTag("Queue", "Transparent");

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", additive ? 1f : 0f);
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", GetSoftTexture());
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", GetSoftTexture());
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 7f);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }

        return material;
    }

    private static Texture2D GetSoftTexture()
    {
        if (softTexture != null)
        {
            return softTexture;
        }

        const int size = 96;
        softTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Muzzle Soft Glow Texture",
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
                float alpha = Mathf.Clamp01(1f - distance);
                alpha = alpha * alpha * (3f - 2f * alpha);
                softTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        softTexture.Apply(false, true);
        return softTexture;
    }

    private sealed class ShotTracer : MonoBehaviour
    {
        private TrailRenderer trail;
        private Vector3 localVelocity;
        private float lifetime;
        private float startTime;

        public void Configure(Vector3 velocity, float life, TrailRenderer shotTrail)
        {
            localVelocity = velocity;
            lifetime = Mathf.Max(0.01f, life);
            trail = shotTrail;
            startTime = Time.time;
        }

        private void Update()
        {
            float age = Time.time - startTime;
            float t = Mathf.Clamp01(age / lifetime);
            transform.localPosition += localVelocity * (Mathf.Lerp(1f, 0.22f, t * t) * Time.deltaTime);

            if (trail != null)
            {
                trail.startWidth = Mathf.Lerp(trail.startWidth, 0f, t * 0.2f);
            }

            if (age >= lifetime)
            {
                if (trail != null)
                {
                    trail.emitting = false;
                    transform.SetParent(null, true);
                    Destroy(gameObject, trail.time + 0.04f);
                }
                else
                {
                    Destroy(gameObject);
                }

                enabled = false;
            }
        }
    }

    private sealed class ShotLightFade : MonoBehaviour
    {
        private Light targetLight;
        private float lifetime;
        private float startIntensity;
        private float startTime;

        public void Configure(float life, float intensity)
        {
            targetLight = GetComponent<Light>();
            lifetime = Mathf.Max(0.01f, life);
            startIntensity = intensity;
            startTime = Time.time;
        }

        private void Update()
        {
            if (targetLight == null)
            {
                Destroy(gameObject);
                return;
            }

            float t = Mathf.Clamp01((Time.time - startTime) / lifetime);
            targetLight.intensity = Mathf.Lerp(startIntensity, 0f, t * t);
            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
