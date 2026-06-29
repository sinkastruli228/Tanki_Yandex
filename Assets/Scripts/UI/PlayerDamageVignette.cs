using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerDamageVignette : MonoBehaviour
{
    [SerializeField] private TankHealth target;
    [SerializeField] private Image vignetteImage;
    [SerializeField] private float damageFlash = 0.55f;
    [SerializeField] private float flashDecay = 2.8f;
    [SerializeField] private float lowHealthThreshold = 0.25f;
    [SerializeField] private float lowHealthBaseAlpha = 0.28f;
    [SerializeField] private float lowHealthPulseAlpha = 0.24f;
    [SerializeField] private float lowHealthPulseSpeed = 5.5f;

    private float flashAlpha;

    public void Configure(TankHealth playerHealth, Image image)
    {
        if (target != playerHealth)
        {
            Unsubscribe();
            target = playerHealth;
            Subscribe();
        }

        vignetteImage = image;
        if (vignetteImage != null)
        {
            vignetteImage.raycastTarget = false;
            vignetteImage.sprite = CreateVignetteSprite();
            vignetteImage.type = Image.Type.Simple;
            vignetteImage.preserveAspect = false;
            vignetteImage.color = Color.clear;
        }
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (vignetteImage == null)
        {
            return;
        }

        float lowHealthAlpha = 0f;
        if (target != null && target.IsAlive && target.Normalized <= lowHealthThreshold)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * lowHealthPulseSpeed) + 1f) * 0.5f;
            lowHealthAlpha = lowHealthBaseAlpha + lowHealthPulseAlpha * pulse;
        }

        flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, flashDecay * Time.unscaledDeltaTime);
        float alpha = Mathf.Clamp01(Mathf.Max(flashAlpha, lowHealthAlpha));
        vignetteImage.color = new Color(1f, 0.02f, 0f, alpha);
        vignetteImage.enabled = alpha > 0.001f;
    }

    private void Subscribe()
    {
        if (target != null)
        {
            target.Damaged -= HandleDamaged;
            target.Damaged += HandleDamaged;
        }
    }

    private void Unsubscribe()
    {
        if (target != null)
        {
            target.Damaged -= HandleDamaged;
        }
    }

    private void HandleDamaged(TankHealth health, int damage)
    {
        flashAlpha = Mathf.Max(flashAlpha, damageFlash);
    }

    private static Sprite CreateVignetteSprite()
    {
        const int size = 192;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Player Damage Vignette Texture",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDistance = center.magnitude;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pixel = new Vector2(x, y);
                float distance = Vector2.Distance(pixel, center) / maxDistance;
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.38f, 0.98f, distance));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private void OnValidate()
    {
        damageFlash = Mathf.Clamp01(damageFlash);
        flashDecay = Mathf.Max(0.01f, flashDecay);
        lowHealthThreshold = Mathf.Clamp01(lowHealthThreshold);
        lowHealthBaseAlpha = Mathf.Clamp01(lowHealthBaseAlpha);
        lowHealthPulseAlpha = Mathf.Clamp01(lowHealthPulseAlpha);
        lowHealthPulseSpeed = Mathf.Max(0f, lowHealthPulseSpeed);
    }
}
