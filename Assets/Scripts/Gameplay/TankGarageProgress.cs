using System;
using UnityEngine;

[Serializable]
public sealed class TankGarageSave
{
    public int coins;
    public int unlockedMask = 1;
    public int selectedSkin;

    public void Normalize()
    {
        coins = Mathf.Max(0, coins);
        unlockedMask = (unlockedMask & 7) | 1;
        if (!Owns(selectedSkin)) selectedSkin = 0;
    }

    public bool Owns(int skin) => skin >= 0 && skin < 3 && (unlockedMask & (1 << skin)) != 0;

    public bool TryBuy(int skin)
    {
        if (skin < 1 || skin > 2 || Owns(skin) || coins < TankGarageProgress.SkinPrice) return false;
        coins -= TankGarageProgress.SkinPrice;
        unlockedMask |= 1 << skin;
        selectedSkin = skin;
        return true;
    }
}

public static class TankGarageProgress
{
    public const int SkinPrice = 1000;
    private const string SaveKey = "Tanki.Garage.v1";
    private static TankGarageSave save;
    public static event Action Changed;
    public static int Coins => Data.coins;
    public static int SelectedSkin => Data.selectedSkin;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() { save = null; Changed = null; }

    private static TankGarageSave Data
    {
        get
        {
            if (save != null) return save;
            try { save = JsonUtility.FromJson<TankGarageSave>(PlayerPrefs.GetString(SaveKey, "")); }
            catch (ArgumentException) { save = null; }
            save ??= new TankGarageSave();
            save.Normalize();
            return save;
        }
    }

    public static bool Owns(int skin) => Data.Owns(skin);
    public static bool TryBuy(int skin)
    {
        if (!Data.TryBuy(skin)) return false;
        Persist();
        return true;
    }

    public static void Select(int skin)
    {
        if (!Owns(skin) || Data.selectedSkin == skin) return;
        Data.selectedSkin = skin;
        Persist();
    }

    public static void AddCoins(int amount)
    {
        if (amount <= 0) return;
        Data.coins = (int)Math.Min(int.MaxValue, (long)Data.coins + amount);
        Persist();
    }

    private static void Persist()
    {
        // One record keeps the debit and unlock together, including on WebGL.
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
