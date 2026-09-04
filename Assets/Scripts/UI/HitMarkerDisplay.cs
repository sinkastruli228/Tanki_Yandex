using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HitMarkerDisplay : MonoBehaviour
{
    [SerializeField] private Image markerImage;
    [SerializeField] private RectTransform markerRect;
    [SerializeField] private Canvas canvas;
    [SerializeField] private float visibleHoldTime = 0.04f;
    [SerializeField] private float fadeDuration = 0.55f;
    [SerializeField] private float startScale = 1.18f;
    [SerializeField] private float endScale = 0.82f;

    private float shownAt = -100f;
    private bool isVisible;
    private Vector3 hitWorldPoint;
    private Color markerTint = Color.white;
    private float markerScaleMultiplier = 1f;

    public void Configure(Image image, Canvas parentCanvas)
    {
        markerImage = image;
        markerRect = image != null ? image.rectTransform : null;
        canvas = parentCanvas;

        if (markerImage != null)
        {
            markerImage.raycastTarget = false;
            markerImage.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        ProjectileMovement.DamagedTank += ShowAtWorldPoint;
    }

    private void OnDisable()
    {
        ProjectileMovement.DamagedTank -= ShowAtWorldPoint;
    }

    private void Update()
    {
        if (!isVisible || markerImage == null || markerRect == null)
        {
            return;
        }

        UpdateMarkerScreenPosition();

        float age = Time.unscaledTime - shownAt;
        float fadeT = Mathf.Clamp01((age - visibleHoldTime) / Mathf.Max(0.01f, fadeDuration));
        float alpha = 1f - SmoothOut(fadeT);
        markerImage.color = new Color(markerTint.r, markerTint.g, markerTint.b, markerTint.a * alpha);

        float scale = Mathf.Lerp(startScale, endScale, SmoothOut(Mathf.Clamp01(age / (visibleHoldTime + fadeDuration)))) * markerScaleMultiplier;
        markerRect.localScale = Vector3.one * scale;

        if (fadeT >= 1f)
        {
            isVisible = false;
            markerImage.gameObject.SetActive(false);
        }
    }

    private void ShowAtWorldPoint(Vector3 worldPoint, bool wasFatal)
    {
        if (markerImage == null || markerRect == null)
        {
            return;
        }

        hitWorldPoint = worldPoint;
        markerTint = wasFatal ? new Color(1f, 0.12f, 0.12f, 1f) : Color.white;
        markerScaleMultiplier = wasFatal ? 2f : 1f;
        UpdateMarkerScreenPosition();
        markerRect.localScale = Vector3.one * (startScale * markerScaleMultiplier);
        markerRect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-14f, 14f));
        markerImage.color = markerTint;
        markerImage.gameObject.SetActive(true);
        markerImage.transform.SetAsLastSibling();

        shownAt = Time.unscaledTime;
        isVisible = true;
    }

    private void UpdateMarkerScreenPosition()
    {
        Camera camera = Camera.main;
        if (camera == null || markerImage == null || markerRect == null)
        {
            return;
        }

        Vector3 screenPoint = camera.WorldToScreenPoint(hitWorldPoint);
        bool isInFrontOfCamera = screenPoint.z >= 0f;
        markerImage.enabled = isInFrontOfCamera;
        if (isInFrontOfCamera)
        {
            markerRect.position = screenPoint;
        }
    }

    private static float SmoothOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }
}
