using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EnemyDamageNumberDisplay : MonoBehaviour
{
    private const int InitialPoolSize = 16;
    private const float Lifetime = .9f;

    private sealed class Popup
    {
        public Text Text;
        public RectTransform Rect;
        public Vector3 WorldPosition;
        public float ShownAt;
        public float HorizontalDrift;
        public bool Active;
    }

    private static EnemyDamageNumberDisplay instance;
    private readonly List<Popup> popups = new List<Popup>(InitialPoolSize);
    private Canvas displayCanvas;
    private Font displayFont;

    public static int ActiveCount { get; private set; }
    public static int LastShownDamage { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        ActiveCount = 0;
        LastShownDamage = 0;
    }

    public static void Show(TankHealth target, int damage)
    {
        if (!Application.isPlaying || target == null || target.Team != TankTeam.Enemy || damage <= 0)
        {
            return;
        }

        EnsureInstance().ShowDamage(target, damage);
    }

    private static EnemyDamageNumberDisplay EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindFirstObjectByType<EnemyDamageNumberDisplay>();
        if (instance == null)
        {
            GameObject root = new GameObject("Enemy Damage Numbers");
            instance = root.AddComponent<EnemyDamageNumberDisplay>();
        }

        instance.EnsureCanvas();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureCanvas();
    }

    private void Update()
    {
        Camera camera = Camera.main;
        float now = Time.unscaledTime;
        int active = 0;

        foreach (Popup popup in popups)
        {
            if (!popup.Active)
            {
                continue;
            }

            float progress = Mathf.Clamp01((now - popup.ShownAt) / Lifetime);
            if (progress >= 1f)
            {
                SetActive(popup, false);
                continue;
            }

            active++;
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            Vector3 animatedWorldPosition = popup.WorldPosition
                + Vector3.up * (eased * 2.2f)
                + Vector3.right * (popup.HorizontalDrift * eased);
            Vector3 screenPoint = camera != null
                ? camera.WorldToScreenPoint(animatedWorldPosition)
                : new Vector3(-1000f, -1000f, -1f);
            popup.Text.enabled = screenPoint.z > 0f;
            if (popup.Text.enabled)
            {
                popup.Rect.position = screenPoint;
            }

            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.42f, 1f, progress));
            Color color = popup.Text.color;
            color.a = fade;
            popup.Text.color = color;
            popup.Rect.localScale = Vector3.one * Mathf.LerpUnclamped(1.22f, .82f, eased);
        }

        ActiveCount = active;
    }

    private void ShowDamage(TankHealth target, int damage)
    {
        Popup popup = GetAvailablePopup();
        Bounds bounds = GetTargetBounds(target);
        Vector2 randomOffset = Random.insideUnitCircle * 1.05f;
        popup.WorldPosition = new Vector3(
            bounds.center.x + randomOffset.x,
            bounds.max.y + Random.Range(.45f, 1.05f),
            bounds.center.z + randomOffset.y);
        popup.HorizontalDrift = Random.Range(-.7f, .7f);
        popup.ShownAt = Time.unscaledTime;
        popup.Text.text = damage.ToString();
        popup.Text.color = damage >= 100
            ? new Color(1f, .72f, .25f, 1f)
            : new Color(1f, .96f, .84f, 1f);
        popup.Rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-5f, 5f));
        popup.Rect.localScale = Vector3.one * 1.22f;
        SetActive(popup, true);
        popup.Rect.SetAsLastSibling();
        LastShownDamage = damage;
    }

    private Popup GetAvailablePopup()
    {
        foreach (Popup popup in popups)
        {
            if (!popup.Active)
            {
                return popup;
            }
        }

        return CreatePopup();
    }

    private Popup CreatePopup()
    {
        GameObject labelObject = new GameObject("Enemy Damage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        labelObject.transform.SetParent(displayCanvas.transform, false);

        Text label = labelObject.GetComponent<Text>();
        label.font = displayFont;
        label.fontSize = 38;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;

        Outline outline = labelObject.GetComponent<Outline>();
        outline.effectColor = new Color(.06f, .075f, .07f, .9f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;

        RectTransform rect = label.rectTransform;
        rect.sizeDelta = new Vector2(140f, 58f);

        Popup popup = new Popup { Text = label, Rect = rect };
        popups.Add(popup);
        SetActive(popup, false);
        return popup;
    }

    private void EnsureCanvas()
    {
        if (displayCanvas != null)
        {
            return;
        }

        displayCanvas = GetComponent<Canvas>();
        if (displayCanvas == null)
        {
            displayCanvas = gameObject.AddComponent<Canvas>();
        }

        displayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        displayCanvas.sortingOrder = 82;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        displayFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (displayFont == null)
        {
            displayFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        while (popups.Count < InitialPoolSize)
        {
            CreatePopup();
        }
    }

    private static Bounds GetTargetBounds(TankHealth target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(target.transform.position + Vector3.up * 1.5f, Vector3.one);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || renderer is ParticleSystemRenderer || renderer is TrailRenderer)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private static void SetActive(Popup popup, bool active)
    {
        popup.Active = active;
        popup.Text.gameObject.SetActive(active);
    }
}
