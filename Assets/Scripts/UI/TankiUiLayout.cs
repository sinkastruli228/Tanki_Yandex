using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared responsive-canvas configuration. Balancing width and height prevents
/// the HUD from being sized exclusively from the width on 4:3 and tall screens.
/// </summary>
public static class TankiUiLayout
{
    public static void ConfigureScaler(CanvasScaler scaler, Vector2 referenceResolution)
    {
        if (scaler == null)
        {
            return;
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = .5f;
    }
}
