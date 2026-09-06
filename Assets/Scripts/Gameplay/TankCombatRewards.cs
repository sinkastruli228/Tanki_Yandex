using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class TankCombatRewards : MonoBehaviour
{
    private const float PassiveChargePerSecond = 0.01f;
    private const float ChargePerKill = 0.2f;
    private const int MinimumKillCoins = 275;
    private const int MaximumKillCoinsInclusive = 325;

    [SerializeField, Range(0f, 1f)] private float specialCharge;
    [SerializeField] private bool specialArmed;

    public event Action SpecialActivationRequested;

    public int Coins => TankGarageProgress.Coins;
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
            ForceChargeSpecial();
            return;
        }

        if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
        {
            RequestSpecialActivation();
        }
    }

    public void RegisterKill()
    {
        TankGarageProgress.AddCoins(UnityEngine.Random.Range(MinimumKillCoins, MaximumKillCoinsInclusive + 1));
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

    public bool RequestSpecialActivation()
    {
        if (specialArmed || !IsFullyCharged)
        {
            return false;
        }

        specialArmed = true;
        SpecialActivationRequested?.Invoke();
        return true;
    }

    public void CancelSpecialActivation()
    {
        if (!specialArmed)
        {
            return;
        }

        specialArmed = false;
        specialCharge = 1f;
    }

    public void ForceChargeSpecial()
    {
        specialCharge = 1f;
        specialArmed = false;
    }

    public void ForceArmSpecialShot()
    {
        ForceChargeSpecial();
        RequestSpecialActivation();
    }
}
