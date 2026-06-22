using UnityEngine;
using UnityEngine.Rendering;

public static class ImpactExplosion
{
    private enum ExplosionStyle
    {
        Volumetric,
        Billboard
    }

    private const ExplosionStyle ActiveStyle = ExplosionStyle.Volumetric;
    private const float ExplosionRadius = 7f;
    private const float ExplosionForce = 1260f;
    private static Material blobMaterial;
    private static Material flashMaterial;
    private static Material volumeHotMaterial;
    private static Material volumeRimMaterial;
    private static Material volumeSmokeMaterial;
    private static Material shockwaveMaterial;
    private static Texture2D blobTexture;

    public static void Spawn(Vector3 position)
    {
        GameObject explosion = new GameObject("Stylized Impact Explosion");
        explosion.transform.position = position;

        PushRigidbodies(position);
        SpawnFlashLight(explosion.transform);
        CreateShockwave(explosion.transform);
        if (ActiveStyle == ExplosionStyle.Volumetric)
        {
            SpawnVolumetricExplosion(explosion.transform);
        }
        else
        {
            SpawnBillboardExplosion(explosion.transform);
        }

        Object.Destroy(explosion, 1.4f);
    }

    private static void SpawnVolumetricExplosion(Transform parent)
    {
        CreateVolumetricBlobs(parent);
        CreateVolumetricSmoke(parent);
    }

    private static void SpawnBillboardExplosion(Transform parent)
    {
        CreateBlobBurst(parent);
        CreateHotCore(parent);
        CreateSmokePuffs(parent);
    }

    private static void CreateVolumetricBlobs(Transform parent)
    {
        Vector3[] offsets =
        {
            new Vector3(0f, 1.2f, 0f),
            new Vector3(-1.45f, 0.9f, 0.35f),
            new Vector3(1.2f, 1f, -0.35f),
            new Vector3(-0.35f, 2.35f, -0.65f),
            new Vector3(0.95f, 2.45f, 0.5f),
            new Vector3(-1.35f, 2.1f, 0.8f),
            new Vector3(0.15f, 3.35f, 0.1f),
            new Vector3(1.75f, 1.85f, 0.7f),
            new Vector3(-2.1f, 1.45f, -0.55f),
            new Vector3(0.2f, 0.45f, 1.2f)
        };

        float[] sizes = { 3.4f, 2.9f, 2.75f, 2.45f, 2.15f, 2.25f, 1.85f, 1.9f, 1.8f, 2.2f };
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 direction = offsets[i].sqrMagnitude > 0.001f ? offsets[i].normalized : Vector3.up;
            float delay = i * 0.012f;
            float lifetime = Mathf.Lerp(0.42f, 0.7f, i / (float)(offsets.Length - 1));
            CreateVolumetricBlob(parent, offsets[i], direction, sizes[i], delay, lifetime);
        }
    }

    private static void CreateShockwave(Transform parent)
    {
        GameObject shockwave = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shockwave.name = "Explosion Radial Shockwave";
        shockwave.transform.SetParent(parent, false);
        shockwave.transform.localPosition = Vector3.up * 0.95f;
        shockwave.transform.localScale = Vector3.one * 0.08f;
        Object.Destroy(shockwave.GetComponent<Collider>());

        Renderer renderer = shockwave.GetComponent<Renderer>();
        renderer.sharedMaterial = GetShockwaveMaterial();

        ShockwaveSphere pulse = shockwave.AddComponent<ShockwaveSphere>();
        pulse.Configure(renderer, 0.34f, 5.2f, new Color(1f, 0.82f, 0.3f, 0.34f));
    }

    private static void CreateVolumetricBlob(Transform parent, Vector3 offset, Vector3 travelDirection, float size, float delay, float lifetime)
    {
        GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rim.name = "Volumetric Explosion Blob Rim";
        rim.transform.SetParent(parent, false);
        rim.transform.localPosition = offset;
        rim.transform.localRotation = Quaternion.Euler(offset.z * 18f, offset.x * 27f, offset.y * 21f);
        rim.transform.localScale = Vector3.one * size * 0.28f;
        Object.Destroy(rim.GetComponent<Collider>());

        Renderer rimRenderer = rim.GetComponent<Renderer>();
        rimRenderer.sharedMaterial = GetVolumeRimMaterial();
        VolumetricExplosionBlob rimBlob = rim.AddComponent<VolumetricExplosionBlob>();
        rimBlob.Configure(
            rimRenderer,
            offset,
            offset + travelDirection * 1.85f,
            Vector3.one * size * 1.08f,
            new Color(1f, 0.47f, 0.05f, 1f),
            new Color(0.34f, 0.12f, 0.02f, 0f),
            delay,
            lifetime,
            true);

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Volumetric Explosion Blob Core";
        core.transform.SetParent(rim.transform, false);
        core.transform.localPosition = Vector3.zero;
        core.transform.localRotation = Quaternion.identity;
        core.transform.localScale = Vector3.one * 0.66f;
        Object.Destroy(core.GetComponent<Collider>());

        Renderer coreRenderer = core.GetComponent<Renderer>();
        coreRenderer.sharedMaterial = GetVolumeHotMaterial();
        VolumetricExplosionBlob coreBlob = core.AddComponent<VolumetricExplosionBlob>();
        coreBlob.Configure(
            coreRenderer,
            Vector3.zero,
            Vector3.zero,
            Vector3.one * 0.88f,
            new Color(1f, 0.94f, 0.28f, 1f),
            new Color(1f, 0.42f, 0.04f, 0f),
            delay + 0.02f,
            lifetime * 0.75f,
            false);
    }

    private static void CreateVolumetricSmoke(Transform parent)
    {
        Vector3[] offsets =
        {
            new Vector3(-1.7f, 0.65f, -0.9f),
            new Vector3(1.6f, 0.75f, 0.85f),
            new Vector3(-0.4f, 1.95f, 1.1f),
            new Vector3(0.85f, 2.2f, -1.05f),
            new Vector3(-1.2f, 2.55f, 0.1f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            GameObject smoke = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            smoke.name = "Volumetric Explosion Smoke";
            smoke.transform.SetParent(parent, false);
            smoke.transform.localPosition = offsets[i];
            smoke.transform.localScale = Vector3.one * 0.4f;
            Object.Destroy(smoke.GetComponent<Collider>());

            Renderer renderer = smoke.GetComponent<Renderer>();
            renderer.sharedMaterial = GetVolumeSmokeMaterial();
            VolumetricExplosionBlob blob = smoke.AddComponent<VolumetricExplosionBlob>();
            blob.Configure(
                renderer,
                offsets[i],
                offsets[i] + offsets[i].normalized * 1.35f + Vector3.up * 0.65f,
                Vector3.one * Mathf.Lerp(2.2f, 3.2f, i / 4f),
                new Color(0.24f, 0.13f, 0.06f, 0.62f),
                new Color(0.1f, 0.08f, 0.06f, 0f),
                0.08f + i * 0.025f,
                0.78f,
                true);
        }
    }

    private static void CreateBlobBurst(Transform parent)
    {
        ParticleSystem particles = parent.gameObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.duration = 0.18f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.54f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4.8f, 10.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(2.4f, 5.2f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.98f, 0.48f, 1f),
            new Color(1f, 0.58f, 0.12f, 1f));
        main.gravityModifier = -0.08f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.8f;
        shape.position = new Vector3(0f, 0.35f, 0f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.62f), 0f),
                new GradientColorKey(new Color(1f, 0.72f, 0.18f), 0.38f),
                new GradientColorKey(new Color(0.42f, 0.2f, 0.08f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.94f, 0.42f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.18f),
            new Keyframe(0.18f, 1.18f),
            new Keyframe(0.62f, 0.92f),
            new Keyframe(1f, 0.18f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = GetBlobMaterial();
        renderer.sortingFudge = 2f;

        particles.Emit(18);
        particles.Play();
    }

    private static void CreateHotCore(Transform parent)
    {
        ParticleSystem core = new GameObject("Explosion Hot Core").AddComponent<ParticleSystem>();
        core.transform.SetParent(parent, false);
        core.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = core.main;
        main.playOnAwake = false;
        main.duration = 0.08f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(3.2f, 6.3f);
        main.startColor = new Color(1f, 0.98f, 0.42f, 0.95f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = core.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = core.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = core.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.2f),
            new Keyframe(0.35f, 1.25f),
            new Keyframe(1f, 0.05f)));

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = core.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 0.68f), 0f),
                new GradientColorKey(new Color(1f, 0.68f, 0.12f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer = core.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = GetFlashMaterial();
        renderer.sortingFudge = 4f;

        core.Emit(6);
        core.Play();
    }

    private static void CreateSmokePuffs(Transform parent)
    {
        ParticleSystem smoke = new GameObject("Explosion Smoke Puffs").AddComponent<ParticleSystem>();
        smoke.transform.SetParent(parent, false);
        smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = smoke.main;
        main.playOnAwake = false;
        main.duration = 0.2f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.72f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(2.1f, 4.7f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.32f, 0.22f, 0.16f, 0.68f),
            new Color(0.52f, 0.31f, 0.12f, 0.55f));
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = smoke.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = smoke.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.65f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = smoke.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.45f, 0.24f, 0.11f), 0f),
                new GradientColorKey(new Color(0.16f, 0.14f, 0.12f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.55f, 0.18f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = smoke.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.3f),
            new Keyframe(0.3f, 1.15f),
            new Keyframe(1f, 0.55f)));

        ParticleSystemRenderer renderer = smoke.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = GetBlobMaterial();
        renderer.sortingFudge = -1f;

        smoke.Emit(10);
        smoke.Play();
    }

    private static void SpawnFlashLight(Transform parent)
    {
        GameObject lightObject = new GameObject("Explosion Flash Light");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = Vector3.up * 1.2f;

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.62f, 0.12f);
        light.range = 16f;
        light.intensity = 5.5f;
        light.shadows = LightShadows.None;

        ExplosionLightFade fade = lightObject.AddComponent<ExplosionLightFade>();
        fade.Configure(0.22f, 5.5f);
    }

    private static void PushRigidbodies(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapSphere(position, ExplosionRadius);
        foreach (Collider collider in colliders)
        {
            Rigidbody body = collider.attachedRigidbody;
            if (body == null || body.isKinematic)
            {
                continue;
            }

            body.AddExplosionForce(ExplosionForce, position, ExplosionRadius, 0.4f, ForceMode.Impulse);
        }
    }

    private static Material GetBlobMaterial()
    {
        if (blobMaterial != null)
        {
            return blobMaterial;
        }

        blobMaterial = CreateParticleMaterial("Stylized Explosion Blob Material");
        SetMaterialTexture(blobMaterial, GetBlobTexture());
        return blobMaterial;
    }

    private static Material GetFlashMaterial()
    {
        if (flashMaterial != null)
        {
            return flashMaterial;
        }

        flashMaterial = CreateParticleMaterial("Stylized Explosion Flash Material");
        SetMaterialTexture(flashMaterial, GetBlobTexture());
        SetMaterialColor(flashMaterial, new Color(1f, 0.95f, 0.38f, 1f));
        return flashMaterial;
    }

    private static Material GetVolumeHotMaterial()
    {
        if (volumeHotMaterial != null)
        {
            return volumeHotMaterial;
        }

        volumeHotMaterial = CreateVolumeMaterial("Volumetric Explosion Hot Material", new Color(1f, 0.88f, 0.2f, 1f), 7.5f);
        return volumeHotMaterial;
    }

    private static Material GetVolumeRimMaterial()
    {
        if (volumeRimMaterial != null)
        {
            return volumeRimMaterial;
        }

        volumeRimMaterial = CreateVolumeMaterial("Volumetric Explosion Rim Material", new Color(1f, 0.34f, 0.04f, 1f), 4.2f);
        return volumeRimMaterial;
    }

    private static Material GetVolumeSmokeMaterial()
    {
        if (volumeSmokeMaterial != null)
        {
            return volumeSmokeMaterial;
        }

        volumeSmokeMaterial = CreateVolumeMaterial("Volumetric Explosion Smoke Material", new Color(0.24f, 0.12f, 0.06f, 0.62f), 0.4f);
        return volumeSmokeMaterial;
    }

    private static Material GetShockwaveMaterial()
    {
        if (shockwaveMaterial != null)
        {
            return shockwaveMaterial;
        }

        shockwaveMaterial = CreateVolumeMaterial("Explosion Shockwave Material", new Color(1f, 0.75f, 0.18f, 0.28f), 2.2f);
        if (shockwaveMaterial.HasProperty("_Smoothness"))
        {
            shockwaveMaterial.SetFloat("_Smoothness", 0.8f);
        }

        return shockwaveMaterial;
    }

    private static Material CreateVolumeMaterial(string materialName, Color color, float emissionMultiplier)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        Material material = new Material(shader)
        {
            name = materialName,
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = (int)RenderQueue.Transparent
        };

        ConfigureTransparentMaterial(material);
        SetMaterialColor(material, color);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * emissionMultiplier);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.28f);
        }

        return material;
    }

    private static Material CreateParticleMaterial(string materialName)
    {
        Shader shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
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

        ConfigureTransparentMaterial(material);
        SetMaterialColor(material, Color.white);
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        material.renderQueue = (int)RenderQueue.Transparent;
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

        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 0f);
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

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", (float)CullMode.Off);
        }

        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void SetMaterialTexture(Material material, Texture2D texture)
    {
        if (material == null || texture == null)
        {
            return;
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }
    }

    private static Texture2D GetBlobTexture()
    {
        if (blobTexture != null)
        {
            return blobTexture;
        }

        const int size = 96;
        blobTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Stylized Explosion Blob Texture",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.43f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                Color color;
                if (distance <= 0.62f)
                {
                    color = new Color(1f, 0.98f, 0.45f, 1f);
                }
                else if (distance <= 0.86f)
                {
                    float t = Mathf.InverseLerp(0.62f, 0.86f, distance);
                    color = Color.Lerp(new Color(1f, 0.9f, 0.22f, 1f), new Color(1f, 0.42f, 0.04f, 1f), t);
                }
                else if (distance <= 1f)
                {
                    float t = Mathf.InverseLerp(0.86f, 1f, distance);
                    color = Color.Lerp(new Color(0.9f, 0.22f, 0.02f, 1f), new Color(0f, 0f, 0f, 0f), t);
                }
                else
                {
                    color = Color.clear;
                }

                blobTexture.SetPixel(x, y, color);
            }
        }

        blobTexture.Apply(false, true);
        return blobTexture;
    }

    private sealed class ExplosionLightFade : MonoBehaviour
    {
        private Light targetLight;
        private float lifetime;
        private float startIntensity;
        private float startTime;

        public void Configure(float newLifetime, float newStartIntensity)
        {
            targetLight = GetComponent<Light>();
            lifetime = Mathf.Max(0.01f, newLifetime);
            startIntensity = newStartIntensity;
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

    private sealed class VolumetricExplosionBlob : MonoBehaviour
    {
        private Renderer targetRenderer;
        private Material materialInstance;
        private Vector3 startLocalPosition;
        private Vector3 endLocalPosition;
        private Vector3 startScale;
        private Vector3 endScale;
        private Color startColor;
        private Color endColor;
        private float delay;
        private float lifetime;
        private float startTime;
        private bool rotate;

        public void Configure(
            Renderer renderer,
            Vector3 fromLocalPosition,
            Vector3 toLocalPosition,
            Vector3 targetScale,
            Color fromColor,
            Color toColor,
            float startDelay,
            float life,
            bool shouldRotate)
        {
            targetRenderer = renderer;
            if (targetRenderer != null)
            {
                materialInstance = new Material(targetRenderer.sharedMaterial)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                targetRenderer.material = materialInstance;
            }

            startLocalPosition = fromLocalPosition;
            endLocalPosition = toLocalPosition;
            startScale = transform.localScale;
            endScale = targetScale;
            startColor = fromColor;
            endColor = toColor;
            delay = Mathf.Max(0f, startDelay);
            lifetime = Mathf.Max(0.01f, life);
            startTime = Time.time;
            rotate = shouldRotate;

            transform.localPosition = startLocalPosition;
            transform.localScale = Vector3.zero;
            SetMaterialColor(materialInstance, Color.clear);
        }

        private void Update()
        {
            float age = Time.time - startTime - delay;
            if (age < 0f)
            {
                return;
            }

            float t = Mathf.Clamp01(age / lifetime);
            float grow = EaseOutBack(Mathf.Clamp01(t / 0.38f));
            float fade = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.42f, 1f, t));

            transform.localPosition = Vector3.Lerp(startLocalPosition, endLocalPosition, EaseOutCubic(t));
            transform.localScale = Vector3.LerpUnclamped(startScale, endScale, grow);
            if (rotate)
            {
                transform.Rotate(Vector3.up, 95f * Time.deltaTime, Space.Self);
                transform.Rotate(Vector3.right, 37f * Time.deltaTime, Space.Self);
            }

            Color color = Color.Lerp(startColor, endColor, t);
            color.a *= fade;
            SetMaterialColor(materialInstance, color);
            if (materialInstance != null && materialInstance.HasProperty("_EmissionColor"))
            {
                Color emission = new Color(color.r, color.g, color.b, 1f) * Mathf.Lerp(7.2f, 0.1f, t);
                materialInstance.SetColor("_EmissionColor", emission);
            }

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private void OnDestroy()
        {
            if (materialInstance != null)
            {
                Destroy(materialInstance);
            }
        }
    }

    private sealed class ShockwaveSphere : MonoBehaviour
    {
        private Renderer targetRenderer;
        private Material materialInstance;
        private Color startColor;
        private float lifetime;
        private float maxScale;
        private float startTime;

        public void Configure(Renderer renderer, float life, float targetScale, Color color)
        {
            targetRenderer = renderer;
            if (targetRenderer != null)
            {
                materialInstance = new Material(targetRenderer.sharedMaterial)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                targetRenderer.material = materialInstance;
            }

            lifetime = Mathf.Max(0.01f, life);
            maxScale = Mathf.Max(0.1f, targetScale);
            startColor = color;
            startTime = Time.time;
            SetMaterialColor(materialInstance, Color.clear);
        }

        private void Update()
        {
            float t = Mathf.Clamp01((Time.time - startTime) / lifetime);
            float expand = 1f - Mathf.Pow(1f - t, 3f);
            float ripple = 1f + Mathf.Sin(t * Mathf.PI * 7f) * 0.035f * (1f - t);
            float verticalSquash = Mathf.Lerp(0.42f, 0.75f, t);

            transform.localScale = new Vector3(maxScale * expand * ripple, maxScale * expand * verticalSquash, maxScale * expand / ripple);
            transform.Rotate(Vector3.up, 210f * Time.deltaTime, Space.Self);

            float alpha = Mathf.SmoothStep(startColor.a, 0f, t);
            Color color = startColor;
            color.a = alpha;
            SetMaterialColor(materialInstance, color);

            if (materialInstance != null && materialInstance.HasProperty("_EmissionColor"))
            {
                Color emission = new Color(startColor.r, startColor.g, startColor.b, 1f) * Mathf.Lerp(3.2f, 0f, t);
                materialInstance.SetColor("_EmissionColor", emission);
            }

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (materialInstance != null)
            {
                Destroy(materialInstance);
            }
        }
    }
}
