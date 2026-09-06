using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class TankShieldUltimate : MonoBehaviour
{
    private const int PlateCount = 6;
    private const float DropHeight = 13f;
    private const float DropDuration = 0.34f;
    private const float DropPause = 0.08f;
    private const float ActiveDuration = 8f;
    private const float DismissDuration = 0.55f;

    private static readonly int[] DropOrder = { 0, 3, 1, 4, 2, 5 };
    private static Material plateMaterial;
    private static Material trimMaterial;
    private static Material slitMaterial;
    private static Material dustMaterial;
    private static Texture2D dustTexture;

    private readonly List<Transform> plates = new List<Transform>(PlateCount);
    private TankController controller;
    private TankHealth health;
    private GameObject shieldRoot;
    private Coroutine routine;

    public bool IsActive { get; private set; }
    public int ActivePlateCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        plateMaterial = null;
        trimMaterial = null;
        slitMaterial = null;
        dustMaterial = null;
        dustTexture = null;
    }

    public bool Activate()
    {
        if (IsActive || !isActiveAndEnabled)
        {
            return false;
        }

        controller = GetComponent<TankController>();
        health = GetComponent<TankHealth>();
        IsActive = true;
        ActivePlateCount = 0;
        controller?.SetMovementLocked(true);
        health?.SetDamageBlocked(true);
        routine = StartCoroutine(RunShield());
        return true;
    }

    private IEnumerator RunShield()
    {
        float groundY = FindGroundY();
        GetShieldDimensions(out float radius, out float plateWidth, out float plateHeight);
        Vector3 center = new Vector3(transform.position.x, groundY, transform.position.z);
        Vector3 forward = controller != null ? controller.ForwardOnPlane : TankPlaneMath.Flatten(transform.forward);
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;

        shieldRoot = new GameObject("Ultimate Shield Fortress");
        shieldRoot.transform.SetPositionAndRotation(center, Quaternion.LookRotation(forward, Vector3.up));
        plates.Clear();

        Transform[] orderedPlates = new Transform[PlateCount];
        for (int i = 0; i < PlateCount; i++)
        {
            float angle = i * (360f / PlateCount);
            Vector3 radial = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 localLanding = radial * radius + Vector3.up * (plateHeight * 0.5f);
            Transform plate = CreatePlate(i + 1, plateWidth, plateHeight, radial);
            plate.SetParent(shieldRoot.transform, false);
            plate.localPosition = localLanding + Vector3.up * DropHeight;
            orderedPlates[i] = plate;
            plates.Add(plate);
        }

        for (int orderIndex = 0; orderIndex < DropOrder.Length; orderIndex++)
        {
            Transform plate = orderedPlates[DropOrder[orderIndex]];
            Vector3 landing = plate.localPosition - Vector3.up * DropHeight;
            Vector3 start = plate.localPosition;
            float elapsed = 0f;
            while (elapsed < DropDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / DropDuration);
                plate.localPosition = Vector3.LerpUnclamped(start, landing, t * t);
                yield return null;
            }

            plate.localPosition = landing;
            BoxCollider collider = plate.GetComponent<BoxCollider>();
            if (collider != null) collider.enabled = true;
            ActivePlateCount++;
            Vector3 impactPoint = plate.position - Vector3.up * (plateHeight * 0.5f);
            SpawnImpactDust(impactPoint, plateWidth);
            TopDownCameraFollow.ShakeAllExplosions(0.72f);
            yield return new WaitForSeconds(DropPause);
        }

        yield return new WaitForSeconds(ActiveDuration);

        Vector3[] starts = new Vector3[plates.Count];
        for (int i = 0; i < plates.Count; i++) starts[i] = plates[i].localPosition;
        float dismissElapsed = 0f;
        while (dismissElapsed < DismissDuration)
        {
            dismissElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(dismissElapsed / DismissDuration);
            float eased = t * t * (3f - 2f * t);
            for (int i = 0; i < plates.Count; i++)
            {
                if (plates[i] != null) plates[i].localPosition = starts[i] + Vector3.down * (4.5f * eased);
            }
            yield return null;
        }

        routine = null;
        FinishShield();
    }

    private Transform CreatePlate(int number, float width, float height, Vector3 radial)
    {
        GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plate.name = "Shield Plate " + number;
        plate.transform.localRotation = Quaternion.LookRotation(radial, Vector3.up);
        plate.transform.localScale = new Vector3(width, height, 0.46f);
        plate.GetComponent<Renderer>().sharedMaterial = GetPlateMaterial();
        BoxCollider collider = plate.GetComponent<BoxCollider>();
        collider.enabled = false;
        plate.AddComponent<TankShieldPlate>().Configure(TankTeam.Player);

        CreatePlateDetail(plate.transform, "Top Trim", new Vector3(0f, 0.43f, 0f), new Vector3(1.03f, 0.10f, 1.16f), GetTrimMaterial());
        CreatePlateDetail(plate.transform, "Embrasure Outside", new Vector3(0f, 0.12f, 0.56f), new Vector3(0.52f, 0.15f, 0.08f), GetSlitMaterial());
        CreatePlateDetail(plate.transform, "Embrasure Inside", new Vector3(0f, 0.12f, -0.56f), new Vector3(0.52f, 0.15f, 0.08f), GetSlitMaterial());
        return plate.transform;
    }

    private static void CreatePlateDetail(Transform parent, string name, Vector3 normalizedPosition, Vector3 normalizedScale, Material material)
    {
        GameObject detail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        detail.name = name;
        detail.transform.SetParent(parent, false);
        detail.transform.localPosition = normalizedPosition;
        detail.transform.localRotation = Quaternion.identity;
        detail.transform.localScale = normalizedScale;
        Collider collider = detail.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        detail.GetComponent<Renderer>().sharedMaterial = material;
    }

    private void GetShieldDimensions(out float radius, out float width, out float height)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        Bounds bounds = new Bounds(transform.position, new Vector3(4.6f, 2.8f, 6.4f));
        bool found = false;
        foreach (Collider collider in colliders)
        {
            if (collider == null || collider.isTrigger || collider.GetComponentInParent<TankShieldPlate>() != null) continue;
            if (!found)
            {
                bounds = collider.bounds;
                found = true;
            }
            else bounds.Encapsulate(collider.bounds);
        }

        radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) + 1.25f, 3.6f, 6.2f);
        width = Mathf.Clamp(radius * 1.13f, 3.4f, 6.4f);
        height = Mathf.Clamp(bounds.size.y + 1.15f, 3.4f, 5.4f);
    }

    private float FindGroundY()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        float bottom = float.PositiveInfinity;
        foreach (Collider collider in colliders)
        {
            if (collider != null && !collider.isTrigger)
            {
                bottom = Mathf.Min(bottom, collider.bounds.min.y);
            }
        }

        return float.IsPositiveInfinity(bottom) ? transform.position.y : bottom;
    }

    private static void SpawnImpactDust(Vector3 position, float width)
    {
        GameObject dustObject = new GameObject("Shield Plate Dust");
        dustObject.transform.position = position + Vector3.up * 0.08f;
        ParticleSystem particles = dustObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.65f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.15f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 5.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.75f, 1.7f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.65f, 0.44f, 0.8f), new Color(0.48f, 0.40f, 0.28f, 0.55f));
        main.gravityModifier = 0.08f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = width * 0.36f;
        shape.radiusThickness = 0.75f;
        shape.rotation = new Vector3(90f, 0f, 0f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.35f, 1.25f);
        velocity.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(new Color(0.76f, 0.63f, 0.43f), 0f), new GradientColorKey(new Color(0.48f, 0.40f, 0.3f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.75f, 0.1f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(0.25f, 1f), new Keyframe(1f, 1.65f)));

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = GetDustMaterial();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        particles.Play();
    }

    private static Material GetPlateMaterial()
    {
        if (plateMaterial == null) plateMaterial = CreateLitMaterial("Shield Plate Material", new Color(0.14f, 0.21f, 0.20f), 0.18f, 0.28f);
        return plateMaterial;
    }

    private static Material GetTrimMaterial()
    {
        if (trimMaterial == null) trimMaterial = CreateLitMaterial("Shield Plate Trim", new Color(0.62f, 0.43f, 0.19f), 0.28f, 0.35f);
        return trimMaterial;
    }

    private static Material GetSlitMaterial()
    {
        if (slitMaterial == null) slitMaterial = CreateLitMaterial("Shield Plate Embrasure", new Color(0.025f, 0.045f, 0.043f), 0.05f, 0.1f);
        return slitMaterial;
    }

    private static Material CreateLitMaterial(string name, Color color, float metallic, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material material = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        return material;
    }

    private static Material GetDustMaterial()
    {
        if (dustMaterial != null) return dustMaterial;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
        dustMaterial = new Material(shader)
        {
            name = "Shield Impact Dust Material",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = (int)RenderQueue.Transparent
        };
        dustMaterial.SetOverrideTag("RenderType", "Transparent");
        if (dustMaterial.HasProperty("_BaseMap")) dustMaterial.SetTexture("_BaseMap", GetDustTexture());
        if (dustMaterial.HasProperty("_MainTex")) dustMaterial.SetTexture("_MainTex", GetDustTexture());
        if (dustMaterial.HasProperty("_BaseColor")) dustMaterial.SetColor("_BaseColor", Color.white);
        if (dustMaterial.HasProperty("_Color")) dustMaterial.SetColor("_Color", Color.white);
        if (dustMaterial.HasProperty("_Surface")) dustMaterial.SetFloat("_Surface", 1f);
        if (dustMaterial.HasProperty("_Blend")) dustMaterial.SetFloat("_Blend", 0f);
        if (dustMaterial.HasProperty("_SrcBlend")) dustMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (dustMaterial.HasProperty("_DstBlend")) dustMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (dustMaterial.HasProperty("_ZWrite")) dustMaterial.SetFloat("_ZWrite", 0f);
        dustMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return dustMaterial;
    }

    private static Texture2D GetDustTexture()
    {
        if (dustTexture != null) return dustTexture;
        const int size = 32;
        dustTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Shield Dust Soft Disc",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = (x + 0.5f) / size * 2f - 1f;
            float ny = (y + 0.5f) / size * 2f - 1f;
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny)), 1.6f);
            dustTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        dustTexture.Apply(false, true);
        return dustTexture;
    }

    private void OnDisable()
    {
        if (IsActive) FinishShield();
    }

    private void FinishShield()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        if (shieldRoot != null) Destroy(shieldRoot);
        shieldRoot = null;
        plates.Clear();
        ActivePlateCount = 0;
        controller?.SetMovementLocked(false);
        health?.SetDamageBlocked(false);
        IsActive = false;
    }
}
