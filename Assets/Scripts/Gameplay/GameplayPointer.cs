using UnityEngine;
using UnityEngine.InputSystem;

public static class GameplayPointer
{
    private static bool overrideActive;
    private static bool followHardwareDelta;
    private static Vector2 overridePosition;
    private static int lastUpdatedFrame = -1;
    private static int suppressDeltaUntilFrame;

    public static bool OverrideActive => overrideActive;

    public static Vector2 Position
    {
        get
        {
            UpdateFromHardwareDelta();
            if (overrideActive)
            {
                return overridePosition;
            }

            return Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : new Vector2(Screen.width * .5f, Screen.height * .5f);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        overrideActive = false;
        followHardwareDelta = false;
        overridePosition = Vector2.zero;
        lastUpdatedFrame = -1;
        suppressDeltaUntilFrame = 0;
    }

    public static void BeginRecentering(Vector2 startPosition)
    {
        overrideActive = true;
        followHardwareDelta = false;
        overridePosition = ClampToScreen(startPosition);
        lastUpdatedFrame = Time.frameCount;
    }

    public static void SetRecenteringPosition(Vector2 position)
    {
        overridePosition = ClampToScreen(position);
        lastUpdatedFrame = Time.frameCount;
    }

    public static void FinishRecentering(Vector2 centerPosition)
    {
        overrideActive = true;
        followHardwareDelta = true;
        overridePosition = ClampToScreen(centerPosition);
        lastUpdatedFrame = Time.frameCount;
        suppressDeltaUntilFrame = Time.frameCount + 3;
    }

    public static void ClearOverride()
    {
        overrideActive = false;
        followHardwareDelta = false;
        lastUpdatedFrame = -1;
    }

    private static void UpdateFromHardwareDelta()
    {
        if (!overrideActive || !followHardwareDelta || lastUpdatedFrame == Time.frameCount)
        {
            return;
        }

        lastUpdatedFrame = Time.frameCount;
        if (Time.frameCount <= suppressDeltaUntilFrame || Mouse.current == null)
        {
            return;
        }

        overridePosition = ClampToScreen(overridePosition + Mouse.current.delta.ReadValue());
    }

    private static Vector2 ClampToScreen(Vector2 position)
    {
        return new Vector2(
            Mathf.Clamp(position.x, 0f, Mathf.Max(1f, Screen.width)),
            Mathf.Clamp(position.y, 0f, Mathf.Max(1f, Screen.height)));
    }
}
