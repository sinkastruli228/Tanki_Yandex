using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NitroSpeedVignette : MonoBehaviour
{
    [SerializeField] private TankNitro nitro;
    [SerializeField] private Image image;
    [SerializeField] private float maxAlpha = 0.28f;

    private float intensity;

    public void Configure(TankNitro tankNitro, Image targetImage)
    {
        nitro = tankNitro;
        image = targetImage;
        if (image == null)
        {
            return;
        }

        image.sprite = CreateVignetteSprite();
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;
        image.color = Color.clear;
    }

    private void Update()
    {
        if (image == null)
        {
            return;
        }

        float targetIntensity = nitro != null && nitro.IsBoosting ? 1f : 0f;
        intensity = Mathf.MoveTowards(intensity, targetIntensity, 2.8f * Time.unscaledDeltaTime);
        float pulse = 0.88f + Mathf.Sin(Time.unscaledTime * 16f) * 0.12f;
        image.color = new Color(0.02f, 0.42f, 1f, intensity * maxAlpha * pulse);
        image.rectTransform.localScale = Vector3.one * (1f + intensity * (0.012f + Mathf.Sin(Time.unscaledTime * 20f) * 0.008f));
        image.enabled = intensity > 0f;
    }

    private static Sprite CreateVignetteSprite()
    {
        const int size = 192;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Nitro Speed Edge Texture",
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
                float distance = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.98f, distance));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
