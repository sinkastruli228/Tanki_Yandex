using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NitroBarDisplay : MonoBehaviour
{
    [SerializeField] private TankNitro nitro;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image glowImage;
    [SerializeField] private Text valueText;

    private static readonly Color IdleColor = new Color(.96f, .71f, .30f, 1f);
    private static readonly Color BoostColor = new Color(.12f, .86f, 1f, 1f);
    private float fillMaxWidth;
    private float fillHeight;
    private float glowMaxWidth;
    private float glowHeight;

    public Image FillImage => fillImage;
    public Image GlowImage => glowImage;
    public Text ValueText => valueText;

    public void Configure(TankNitro tankNitro, Image fill, Image glow, Text value)
    {
        nitro = tankNitro;
        fillImage = fill;
        glowImage = glow;
        valueText = value;
        if (fillImage != null && fillImage.rectTransform.parent is RectTransform trackRect)
        {
            fillMaxWidth = Mathf.Max(0f, trackRect.rect.width - 8f);
            fillHeight = Mathf.Max(1f, trackRect.rect.height - 4f);
            RectTransform rect = fillImage.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(4f, 0f);
        }
        if (glowImage != null)
        {
            glowMaxWidth = glowImage.rectTransform.rect.width;
            glowHeight = glowImage.rectTransform.rect.height;
            glowImage.rectTransform.pivot = new Vector2(0f, 1f);
        }
        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        if (nitro == null || fillImage == null)
        {
            return;
        }

        float amount = nitro.Normalized;
        bool boosting = nitro.IsBoosting;
        fillImage.fillAmount = amount;
        fillImage.rectTransform.sizeDelta = new Vector2(fillMaxWidth * amount, fillHeight);
        fillImage.enabled = amount > .001f;
        fillImage.color = boosting ? BoostColor : IdleColor;
        if (valueText != null) valueText.text = Mathf.RoundToInt(amount * 100f) + "%";

        if (glowImage != null)
        {
            glowImage.gameObject.SetActive(boosting);
            glowImage.fillAmount = amount;
            glowImage.rectTransform.sizeDelta = new Vector2(glowMaxWidth * amount, glowHeight);
            float pulse = .22f + (.12f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 9f)));
            glowImage.color = new Color(BoostColor.r, BoostColor.g, BoostColor.b, pulse);
        }
    }
}
