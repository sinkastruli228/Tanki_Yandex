using System;
using UnityEngine;

[Flags]
public enum GameplayBlockReason
{
    None = 0,
    Garage = 1 << 0,
    UpgradeSelection = 1 << 1,
    BombardmentPlanner = 1 << 2,
    Defeat = 1 << 3,
    PlatformPause = 1 << 4,
    LegacyTankSelection = 1 << 5
}

/// <summary>
/// Owns the global gameplay pause/input state so independent overlays cannot
/// accidentally resume a battle while another modal state is still active.
/// </summary>
public static class GameplayModalState
{
    private static GameplayBlockReason inputBlockers;
    private static GameplayBlockReason timeBlockers;

    public static bool IsInputBlocked => inputBlockers != GameplayBlockReason.None;
    public static bool IsTimePaused => timeBlockers != GameplayBlockReason.None;
    public static GameplayBlockReason InputBlockers => inputBlockers;
    public static GameplayBlockReason TimeBlockers => timeBlockers;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        inputBlockers = GameplayBlockReason.None;
        timeBlockers = GameplayBlockReason.None;
    }

    public static void Reset()
    {
        inputBlockers = GameplayBlockReason.None;
        timeBlockers = GameplayBlockReason.None;
        if (TankiPlatformServices.IsPlatformPaused)
        {
            inputBlockers |= GameplayBlockReason.PlatformPause;
            timeBlockers |= GameplayBlockReason.PlatformPause;
        }
        Apply();
    }

    public static void Set(GameplayBlockReason reason, bool active, bool pauseTime)
    {
        if (reason == GameplayBlockReason.None)
        {
            return;
        }

        inputBlockers = active ? inputBlockers | reason : inputBlockers & ~reason;
        if (pauseTime)
        {
            timeBlockers = active ? timeBlockers | reason : timeBlockers & ~reason;
        }
        else
        {
            timeBlockers &= ~reason;
        }

        Apply();
    }

    public static bool Contains(GameplayBlockReason reason)
    {
        return (inputBlockers & reason) != 0;
    }

    private static void Apply()
    {
        PlayerHealthBar.SetGameplayInputBlocked(IsInputBlocked);
        Time.timeScale = IsTimePaused ? 0f : 1f;
        TankiPlatformServices.RefreshGameplayMarkup();
    }
}
