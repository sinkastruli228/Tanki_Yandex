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
    private int displayedMaxHealth = -1;
    private int segmentCount = 1;

    public int SegmentCount => segmentCount;

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
        fillImage.sprite = BrushBarSpriteFactory.Horizontal;
        fillImage.type = Image.Type.Simple;
        fillImage.rectTransform.anchorMin = fillImage.rectTransform.anchorMax = new Vector2(0f, .5f);
        fillImage.rectTransform.pivot = new Vector2(0f, .5f);
        fillImage.rectTransform.anchoredPosition = new Vector2(.08f, 0f);
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
        fillRect.sizeDelta = new Vector2((Width - .16f) * target.Normalized, Height - .12f);
        fillImage.gameObject.SetActive(target.IsAlive);
        RefreshSegments();
    }

    private void RefreshSegments()
    {
        if (canvas == null || target == null || displayedMaxHealth == target.MaxHealth)
        {
            return;
        }

        displayedMaxHealth = target.MaxHealth;
        segmentCount = target.Team == TankTeam.Enemy && target.MaxHealth > 100
            ? Mathf.CeilToInt(target.MaxHealth / 100f)
            : 1;

        Transform root = canvas.transform;
        for (int childIndex = 0; childIndex < root.childCount; childIndex++)
        {
            Transform child = root.GetChild(childIndex);
            if (child.name.StartsWith("Health Segment Divider "))
            {
                child.gameObject.SetActive(false);
            }
        }

        for (int segment = 1; segment < segmentCount; segment++)
        {
            Image divider = GetOrCreateImage(root, $"Health Segment Divider {segment}");
            float position = Mathf.Clamp01(segment * 100f / target.MaxHealth);
            RectTransform dividerRect = divider.rectTransform;
            dividerRect.anchorMin = new Vector2(position, .12f);
            dividerRect.anchorMax = new Vector2(position, .88f);
            dividerRect.pivot = new Vector2(.5f, .5f);
            dividerRect.anchoredPosition = Vector2.zero;
            dividerRect.sizeDelta = new Vector2(.035f, 0f);
            divider.color = new Color(.025f, .045f, .04f, .92f);
            divider.raycastTarget = false;
            divider.gameObject.SetActive(true);
            divider.transform.SetAsLastSibling();
        }
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
