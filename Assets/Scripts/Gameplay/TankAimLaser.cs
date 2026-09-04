using UnityEngine;

[DisallowMultipleComponent]
public sealed class TankAimLaser : MonoBehaviour
{
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private Transform turret;
    [SerializeField] private TankShooter shooter;
    [SerializeField] private float length = 24f;
    [SerializeField] private float startWidth = 0.18f;
    [SerializeField] private float endWidth = 0.03f;
    [SerializeField] private LayerMask obstructionMask = ~0;

    private LineRenderer line;
    private LineRenderer glowLine;
    private Material material;

    public void Configure(Transform muzzleTransform, Transform turretTransform, TankShooter tankShooter)
    {
        muzzlePoint = muzzleTransform;
        turret = turretTransform;
        shooter = tankShooter;
        EnsureLine();
    }

    private void Awake()
    {
        EnsureLine();
    }

    private void OnDestroy()
    {
        if (material != null)
        {
            Destroy(material);
        }
    }

    private void LateUpdate()
    {
        if (muzzlePoint == null || turret == null || shooter == null)
        {
            line.enabled = false;
            glowLine.enabled = false;
            return;
        }

        Vector3 start = muzzlePoint.position;
        Vector3 direction = turret.forward;
        direction.y = 0f;
        direction.Normalize();
        float visibleLength = length;
        RaycastHit[] hits = Physics.RaycastAll(start, direction, length, obstructionMask, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (!hit.collider.transform.IsChildOf(transform))
            {
                visibleLength = Mathf.Min(visibleLength, hit.distance);
            }
        }

        bool reloading = shooter.ReloadNormalized < 0.995f;
        Color laserColor = reloading ? new Color(2f, 0.06f, 0.02f, 1f) : new Color(0.05f, 2f, 0.18f, 1f);
        line.colorGradient = CreateFadeGradient(laserColor, 1f);
        glowLine.colorGradient = CreateFadeGradient(laserColor, 0.28f);
        line.SetPosition(0, start);
        line.SetPosition(1, start + direction * visibleLength);
        glowLine.SetPosition(0, start);
        glowLine.SetPosition(1, start + direction * visibleLength);
        line.enabled = enabled && shooter.enabled;
        glowLine.enabled = line.enabled;
    }

    private void OnValidate()
    {
        length = Mathf.Max(0.1f, length);
        startWidth = Mathf.Max(0.001f, startWidth);
        endWidth = Mathf.Max(0f, endWidth);
    }

    private void EnsureLine()
    {
        if (line != null)
        {
            return;
        }

        material = new Material(Shader.Find("Sprites/Default"));
        material.renderQueue = 3100;
        line = CreateLine("Aim Laser", startWidth, endWidth, material);
        glowLine = CreateLine("Aim Laser Glow", startWidth * 3f, endWidth * 3f, material);
    }

    private LineRenderer CreateLine(string lineName, float lineStartWidth, float lineEndWidth, Material lineMaterial)
    {
        GameObject laserObject = new GameObject(lineName);
        laserObject.transform.SetParent(transform, false);
        LineRenderer result = laserObject.AddComponent<LineRenderer>();
        result.useWorldSpace = true;
        result.positionCount = 2;
        result.alignment = LineAlignment.View;
        result.numCapVertices = 4;
        result.widthCurve = new AnimationCurve(new Keyframe(0f, lineStartWidth), new Keyframe(1f, lineEndWidth));
        result.material = lineMaterial;
        return result;
    }

    private static Gradient CreateFadeGradient(Color color, float alphaMultiplier)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(alphaMultiplier, 0f), new GradientAlphaKey(alphaMultiplier * 0.75f, 0.65f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }
}
