using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Small platform boundary for Yandex Games lifecycle events. In the editor and
/// non-WebGL builds every call is a safe no-op.
/// </summary>
public static class TankiPlatformServices
{
    private const string BridgeObjectName = "Tanki Platform Bridge";
    private static bool battleActive;
    private static bool platformPaused;
    private static bool gameReadySent;
    private static bool lastGameplayState;
    private static bool hasGameplayState;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void TankiYandexInitialize();
    [DllImport("__Internal")] private static extern void TankiYandexGameReady();
    [DllImport("__Internal")] private static extern void TankiYandexSetGameplay(int active);
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        battleActive = false;
        platformPaused = false;
        gameReadySent = false;
        lastGameplayState = false;
        hasGameplayState = false;

        GameObject bridge = GameObject.Find(BridgeObjectName);
        if (bridge == null)
        {
            bridge = new GameObject(BridgeObjectName);
            bridge.AddComponent<TankiPlatformBridge>();
            Object.DontDestroyOnLoad(bridge);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        TankiYandexInitialize();
#endif
    }

    public static void NotifyGameReady()
    {
        if (gameReadySent)
        {
            return;
        }

        gameReadySent = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        TankiYandexGameReady();
#endif
    }

    public static void SetBattleActive(bool active)
    {
        battleActive = active;
        RefreshGameplayMarkup();
    }

    public static void SetPlatformPaused(bool paused)
    {
        platformPaused = paused;
        AudioListener.pause = paused;
        GameplayModalState.Set(GameplayBlockReason.PlatformPause, paused, true);
    }

    public static bool IsPlatformPaused => platformPaused;

    public static void RefreshGameplayMarkup()
    {
        bool gameplayRunning = battleActive && !platformPaused && !GameplayModalState.IsInputBlocked && !GameplayModalState.IsTimePaused;
        if (hasGameplayState && gameplayRunning == lastGameplayState)
        {
            return;
        }

        hasGameplayState = true;
        lastGameplayState = gameplayRunning;
#if UNITY_WEBGL && !UNITY_EDITOR
        TankiYandexSetGameplay(gameplayRunning ? 1 : 0);
#endif
    }
}

public sealed class TankiPlatformBridge : MonoBehaviour
{
    // Called from the WebGL JavaScript bridge through SendMessage.
    public void OnYandexPause(string _)
    {
        TankiPlatformServices.SetPlatformPaused(true);
    }

    // Called from the WebGL JavaScript bridge through SendMessage.
    public void OnYandexResume(string _)
    {
        TankiPlatformServices.SetPlatformPaused(false);
    }
}
