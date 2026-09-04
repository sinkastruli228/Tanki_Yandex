using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class TankCombatRewards : MonoBehaviour
{
    private const float PassiveChargePerSecond = 0.01f;
    private const float ChargePerKill = 0.2f;
    private const int MinimumKillCoins = 275;
    private const int MaximumKillCoinsInclusive = 325;

    [SerializeField] private int coins;
    [SerializeField, Range(0f, 1f)] private float specialCharge;
    [SerializeField] private bool specialArmed;

    public int Coins => coins;
    public float ChargeNormalized => specialCharge;
    public bool IsSpecialArmed => specialArmed;
    public bool IsFullyCharged => specialCharge >= 0.9999f;

    private void Update()
    {
        if (PlayerHealthBar.GameplayInputBlocked)
        {
            return;
        }

        if (!specialArmed && specialCharge < 1f)
        {
            specialCharge = Mathf.Min(1f, specialCharge + PassiveChargePerSecond * Time.deltaTime);
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.tKey.wasPressedThisFrame)
        {
            ForceArmSpecialShot();
            return;
        }

        if (!specialArmed && IsFullyCharged && keyboard != null && keyboard.qKey.wasPressedThisFrame)
        {
            specialArmed = true;
        }
    }

    public void RegisterKill()
    {
        coins += Random.Range(MinimumKillCoins, MaximumKillCoinsInclusive + 1);
        if (!specialArmed)
        {
            specialCharge = Mathf.Min(1f, specialCharge + ChargePerKill);
        }
    }

    public bool ConsumeArmedShot()
    {
        if (!specialArmed)
        {
            return false;
        }

        specialArmed = false;
        specialCharge = 0f;
        return true;
    }

    public void ForceArmSpecialShot()
    {
        specialCharge = 1f;
        specialArmed = true;
    }
}
