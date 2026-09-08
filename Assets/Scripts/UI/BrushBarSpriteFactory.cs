using UnityEngine;

public static class BrushBarSpriteFactory
{
    private static Sprite horizontal;
    private static Sprite vertical;

    public static Sprite Horizontal => horizontal != null ? horizontal : horizontal = Create(false);
    public static Sprite Vertical => vertical != null ? vertical : vertical = Create(true);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        horizontal = null;
        vertical = null;
    }

    private static Sprite Create(bool isVertical)
    {
        const int length = 192;
        const int thickness = 28;
        int width = isVertical ? thickness : length;
        int height = isVertical ? length : thickness;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = isVertical ? "Vertical Brush Status Bar" : "Horizontal Brush Status Bar",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float along = isVertical ? y : x;
                float across = isVertical ? x : y;
                float leftEdge = 5f + Mathf.Sin(across * 1.37f) * 1.7f + Mathf.Sin(across * .47f) * 1.1f;
                float rightEdge = length - 6f + Mathf.Sin(across * 1.11f + 1.8f) * 1.9f + Mathf.Sin(across * .39f) * .9f;
                float endAlpha = Mathf.Clamp01(along - leftEdge + 1f) * Mathf.Clamp01(rightEdge - along + 1f);

                float halfThickness = thickness * .36f
                    + Mathf.Sin(along * .19f) * .8f
                    + Mathf.Sin(along * .061f + 2.1f) * .7f;
                float edgeAlpha = Mathf.Clamp01(halfThickness - Mathf.Abs(across - (thickness - 1f) * .5f) + 1f);
                float grain = .9f + .1f * Mathf.Abs(Mathf.Sin((x * 12.9898f + y * 78.233f) * .071f));
                pixels[y * width + x] = new Color(1f, 1f, 1f, endAlpha * edgeAlpha * grain);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
