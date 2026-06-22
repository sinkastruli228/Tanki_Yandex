using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class TankTrackDust : MonoBehaviour
{
    private const float MinDustSpeed = 0.18f;

    [SerializeField] private TankController tankController;
    [SerializeField] private Vector3 localForwardAxis = Vector3.forward;
    [SerializeField] private ParticleSystem leftDust;
    [SerializeField] private ParticleSystem rightDust;

    private static Material dustMaterial;
    private static Texture2D dustTexture;

    private Vector3 localRightAxis = Vector3.right;
    private float trackSideOffset = 0.9f;
    private float trackEndOffset = 1.6f;
    private float trackHeight = 0.08f;

    public void Configure(TankController controller, Vector3 forwardAxis)
    {
        tankController = controller;
        localForwardAxis = TankPlaneMath.SafeLocalForwardAxis(forwardAxis);
        localRightAxis = Vector3.Cross(Vector3.up, localForwardAxis).normalized;
        if (localRightAxis.sqrMagnitude < 0.001f)
        {
            localRightAxis = Vector3.right;
        }

        CalculateTrackLayout();
        leftDust = EnsureDustEmitter("Left Track Dust", leftDust);
        rightDust = EnsureDustEmitter("Right Track Dust", rightDust);
        UpdateEmitterPositions(0f);
        SetEmission(leftDust, 0f);
        SetEmission(rightDust, 0f);
    }

    private void Reset()
    {
        tankController = GetComponent<TankController>();
    }

    private void Awake()
    {
        if (tankController == null)
        {
            tankController = GetComponent<TankController>();
        }

        Configure(tankController, localForwardAxis);
    }

    private void Update()
    {
        if (tankController == null)
        {
            SetEmission(leftDust, 0f);
            SetEmission(rightDust, 0f);
            return;
        }

        float speed = tankController.CurrentSpeed;
        float speed01 = tankController.enabled ? tankController.CurrentSpeedNormalized : 0f;
        bool isMoving = Mathf.Abs(speed) > MinDustSpeed && speed01 > 0.01f;
        UpdateEmitterPositions(speed);

        float rate = isMoving ? Mathf.Lerp(36f, 110f, speed01) : 0f;
        SetEmission(leftDust, rate);
        SetEmission(rightDust, rate);
    }

    private void CalculateTrackLayout()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        float minY = 0f;
        float maxSide = 0f;
        float maxForward = 0f;

        foreach (Renderer renderer in renderers)
        {
            if (renderer is ParticleSystemRenderer)
            {
                continue;
            }

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
                        Vector3 localCorner = transform.InverseTransformPoint(corner);

                        if (!hasBounds)
                        {
                            minY = localCorner.y;
                            hasBounds = true;
                        }
                        else
                        {
                            minY = Mathf.Min(minY, localCorner.y);
                        }

                        maxSide = Mathf.Max(maxSide, Mathf.Abs(Vector3.Dot(localCorner, localRightAxis)));
                        maxForward = Mathf.Max(maxForward, Mathf.Abs(Vector3.Dot(localCorner, localForwardAxis)));
                    }
                }
            }
        }

        if (!hasBounds)
        {
            return;
        }

        trackSideOffset = Mathf.Max(0.25f, maxSide * 0.52f);
        trackEndOffset = Mathf.Max(0.45f, maxForward * 0.62f);
        trackHeight = minY + 0.12f;
    }

    private ParticleSystem EnsureDustEmitter(string emitterName, ParticleSystem existing)
    {
        Transform existingTransform = transform.Find(emitterName);
        GameObject emitterObject = existingTransform != null ? existingTransform.gameObject : new GameObject(emitterName);
        emitterObject.transform.SetParent(transform, false);

        ParticleSystem particles = existing != null ? existing : emitterObject.GetComponent<ParticleSystem>();
        if (particles == null)
        {
            particles = emitterObject.AddComponent<ParticleSystem>();
        }

        ConfigureDustParticles(particles);
        return particles;
    }

    private void ConfigureDustParticles(ParticleSystem particles)
    {
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.85f, 1.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 2.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.95f, 2.15f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.62f, 0.54f, 0.42f, 0.72f),
            new Color(0.9f, 0.78f, 0.56f, 0.58f));
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.38f;
        shape.radiusThickness = 0.7f;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.9f, 0.9f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.7f, 2.2f);
        velocity.z = new ParticleSystem.MinMaxCurve(-1.15f, 1.15f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.72f, 0.62f, 0.46f), 0f),
                new GradientColorKey(new Color(0.54f, 0.5f, 0.42f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.72f, 0.12f),
                new GradientAlphaKey(0.34f, 0.62f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.24f, 1.25f),
            new Keyframe(1f, 1.9f)));

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = GetDustMaterial();
        renderer.sortingFudge = -2f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        if (!particles.isPlaying)
        {
            particles.Play();
        }
    }

    private void UpdateEmitterPositions(float currentSpeed)
    {
        bool reversing = currentSpeed < -MinDustSpeed;
        float endSign = reversing ? 1f : -1f;
        Vector3 endOffset = localForwardAxis * (trackEndOffset * endSign);
        Vector3 heightOffset = Vector3.up * trackHeight;

        if (leftDust != null)
        {
            leftDust.transform.localPosition = heightOffset + endOffset - localRightAxis * trackSideOffset;
        }

        if (rightDust != null)
        {
            rightDust.transform.localPosition = heightOffset + endOffset + localRightAxis * trackSideOffset;
        }
    }

    private static void SetEmission(ParticleSystem particles, float rate)
    {
        if (particles == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = Mathf.Max(0f, rate);
    }

    private static Material GetDustMaterial()
    {
        if (dustMaterial != null)
        {
            return dustMaterial;
        }

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

        dustMaterial = new Material(shader)
        {
            name = "Tank Track Dust Material",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = (int)RenderQueue.Transparent
        };
        ConfigureDustMaterial(dustMaterial);
        return dustMaterial;
    }

    private static void ConfigureDustMaterial(Material material)
    {
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetOverrideTag("Queue", "Transparent");

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", GetDustTexture());
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", GetDustTexture());
        }

        Color color = new Color(0.72f, 0.62f, 0.46f, 0.78f);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }

        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }

        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }
    }

    private static Texture2D GetDustTexture()
    {
        if (dustTexture != null)
        {
            return dustTexture;
        }

        const int size = 64;
        dustTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Tank Track Dust Soft Texture",
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
                dustTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        dustTexture.Apply(false, true);
        return dustTexture;
    }
}
