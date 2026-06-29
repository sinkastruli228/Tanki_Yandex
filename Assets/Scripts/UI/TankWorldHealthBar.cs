using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TankWorldHealthBar : MonoBehaviour
{
    private const float Width = 2.8f;
    private const float Height = 0.28f;
    private const float VerticalPadding = 0.85f;

    [SerializeField] private TankHealth target;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Image fillImage;

    public void Configure(TankHealth tankHealth, Camera cameraOverride)
    {
        if (target != null)
        {
            target.Changed -= HandleHealthChanged;
            target.Died -= HandleDied;
        }

        target = tankHealth;
        targetCamera = cameraOverride != null ? cameraOverride : Camera.main;
        EnsureVisuals();
        PositionAboveTank();
        UpdateFill();

        if (target != null)
        {
            target.Changed += HandleHealthChanged;
            target.Died += HandleDied;
        }
    }

    private void LateUpdate()
    {
        if (target == null || !target.IsAlive)
        {
            if (canvas != null)
            {
                canvas.gameObject.SetActive(false);
            }

            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (canvas != null && targetCamera != null)
        {
            canvas.transform.rotation = Quaternion.LookRotation(canvas.transform.position - targetCamera.transform.position, Vector3.up);
        }
    }

    private void OnDestroy()
    {
        if (target != null)
        {
            target.Changed -= HandleHealthChanged;
            target.Died -= HandleDied;
        }
    }

    private void HandleHealthChanged(TankHealth tankHealth)
    {
        UpdateFill();
    }

    private void HandleDied(TankHealth tankHealth)
    {
        if (canvas != null)
        {
            canvas.gameObject.SetActive(false);
        }
    }

    private void EnsureVisuals()
    {
        Transform existing = transform.Find("World Health Bar");
        GameObject root = existing != null ? existing.gameObject : new GameObject("World Health Bar", typeof(RectTransform));
        root.transform.SetParent(transform, false);

        canvas = root.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = root.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 20;

        RectTransform canvasRect = root.transform as RectTransform;
        canvasRect.sizeDelta = new Vector2(Width, Height);
        canvasRect.localScale = Vector3.one;

        Image background = GetOrCreateImage(canvasRect, "Background");
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.offsetMin = Vector2.zero;
        background.rectTransform.offsetMax = Vector2.zero;
        background.color = new Color(0f, 0f, 0f, 0.68f);
        background.raycastTarget = false;

        fillImage = GetOrCreateImage(canvasRect, "Fill");
        fillImage.rectTransform.anchorMin = Vector2.zero;
        fillImage.rectTransform.anchorMax = Vector2.one;
        fillImage.rectTransform.offsetMin = new Vector2(0.08f, 0.06f);
        fillImage.rectTransform.offsetMax = new Vector2(-0.08f, -0.06f);
        fillImage.color = new Color(0.15f, 0.9f, 0.15f, 1f);
        fillImage.raycastTarget = false;

        root.SetActive(true);
    }

    private void PositionAboveTank()
    {
        if (canvas == null)
        {
            return;
        }

        float topY = transform.position.y + 3.2f;
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        if (hasBounds)
        {
            topY = bounds.max.y + VerticalPadding;
        }

        canvas.transform.position = new Vector3(transform.position.x, topY, transform.position.z);
    }

    private void UpdateFill()
    {
        if (fillImage == null || target == null)
        {
            return;
        }

        RectTransform fillRect = fillImage.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(target.Normalized, 1f);
        fillRect.offsetMin = new Vector2(0.08f, 0.06f);
        fillRect.offsetMax = new Vector2(-0.08f, -0.06f);
        fillImage.gameObject.SetActive(target.IsAlive);
    }

    private static Image GetOrCreateImage(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject imageObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        if (image == null)
        {
            image = imageObject.AddComponent<Image>();
        }

        return image;
    }
}
