using System;
using UnityEngine;

public static class TankUltimateLoadout
{
    public const int SlotCount = 4;
    public const int AvailableSlotCount = 3;
    public const int RocketSlot = 0;
    public const int ShieldSlot = 1;
    public const int BombardmentSlot = 2;
    private const string PreferenceKey = "Tanki.Ultimate";
    private static int selected = -1;

    public static event Action Changed;
    public static int Selected => selected < 0
        ? selected = Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey, RocketSlot), 0, AvailableSlotCount - 1)
        : selected;

    public static bool IsAvailable(int slot) => slot >= 0 && slot < AvailableSlotCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        selected = -1;
        Changed = null;
    }

    public static void Select(int slot)
    {
        if (!IsAvailable(slot)) return;
        if (Selected == slot) return;
        selected = slot;
        PlayerPrefs.SetInt(PreferenceKey, selected);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
