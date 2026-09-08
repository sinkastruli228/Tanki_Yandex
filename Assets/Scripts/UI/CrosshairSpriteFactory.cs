using UnityEngine;

public static class CrosshairSpriteFactory
{
    private static Sprite segmentedRing;
    private static Sprite diamond;

    public static Sprite SegmentedRing => segmentedRing != null ? segmentedRing : segmentedRing = CreateSegmentedRing();
    public static Sprite Diamond => diamond != null ? diamond : diamond = CreateDiamond();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        segmentedRing = null;
        diamond = null;
    }

    private static Sprite CreateSegmentedRing()
    {
        const int size = 128;
        const float radius = 40f;
        const float thickness = 6f;
        float center = (size - 1f) * .5f;
        Texture2D texture = CreateTexture(size, size, "Variant 20 Segmented Reticle");
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                float nearestCardinal = Mathf.Round(angle / 90f) * 90f;
                float cardinalDistance = Mathf.Abs(Mathf.DeltaAngle(angle, nearestCardinal));

                float ringAlpha = SmoothCoverage(thickness * .5f - Mathf.Abs(distance - radius));
                ringAlpha *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 12f, cardinalDistance));

                float horizontalTick = SmoothBox(Mathf.Abs(dy), Mathf.Abs(dx) - 50f, 3.2f, 7f);
                float verticalTick = SmoothBox(Mathf.Abs(dx), Mathf.Abs(dy) - 50f, 3.2f, 7f);
                float alpha = Mathf.Max(ringAlpha, Mathf.Max(horizontalTick, verticalTick));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return CreateSprite(texture);
    }

    private static Sprite CreateDiamond()
    {
        const int size = 48;
        float center = (size - 1f) * .5f;
        Texture2D texture = CreateTexture(size, size, "Variant 20 Gold Diamond");
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                float alpha = SmoothCoverage(15f - distance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return CreateSprite(texture);
    }

    private static Texture2D CreateTexture(int width, int height, string textureName)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = textureName,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private static Sprite CreateSprite(Texture2D texture)
    {
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static float SmoothCoverage(float signedDistance)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1f, 1f, signedDistance));
    }

    private static float SmoothBox(float firstDistance, float secondDistance, float halfFirst, float halfSecond)
    {
        return Mathf.Min(SmoothCoverage(halfFirst - firstDistance), SmoothCoverage(halfSecond - Mathf.Abs(secondDistance)));
    }
}
