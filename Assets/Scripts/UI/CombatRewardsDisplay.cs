using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatRewardsDisplay : MonoBehaviour
{
    [SerializeField] private TankCombatRewards rewards;
    [SerializeField] private TankSpecialWeapon specialWeapon;
    [SerializeField] private Text coinText;
    [SerializeField] private Image chargeFill;
    [SerializeField] private Image targetMarker;
    [SerializeField] private Text ultimateNumeral;
    [SerializeField] private Text ultimateName;
    [SerializeField] private Text chargeStatus;
    [SerializeField] private Image shortcutBackground;

    public void Configure(
        TankCombatRewards combatRewards,
        TankSpecialWeapon weapon,
        Text coinsLabel,
        Image radialFill,
        Image enemyTargetMarker,
        Text numeralLabel,
        Text nameLabel,
        Text statusLabel,
        Image keyBackground)
    {
        rewards = combatRewards;
        specialWeapon = weapon;
        coinText = coinsLabel;
        chargeFill = radialFill;
        targetMarker = enemyTargetMarker;
        ultimateNumeral = numeralLabel;
        ultimateName = nameLabel;
        chargeStatus = statusLabel;
        shortcutBackground = keyBackground;
        UpdateVisuals();
    }

    private void Update()
    {
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (rewards == null)
        {
            return;
        }

        if (coinText != null)
        {
            coinText.text = GameLanguage.Text($"МОНЕТЫ: {rewards.Coins}", $"COINS: {rewards.Coins}");
        }

        bool shieldActive = specialWeapon != null && specialWeapon.IsShieldActive;
        bool bombardmentPlanning = specialWeapon != null && specialWeapon.IsBombardmentPlanning;
        bool bombardmentActive = specialWeapon != null && specialWeapon.IsBombardmentActive;
        if (chargeFill != null)
        {
            chargeFill.fillAmount = shieldActive || bombardmentActive
                ? 1f
                : rewards.ChargeNormalized;
            chargeFill.color = new Color(.96f, .71f, .30f, 1f);
        }

        int selected = TankUltimateLoadout.Selected;
        if (ultimateNumeral != null) ultimateNumeral.text = selected == TankUltimateLoadout.BombardmentSlot ? "III" : selected == TankUltimateLoadout.ShieldSlot ? "II" : "I";
        if (ultimateName != null)
        {
            ultimateName.text = selected == TankUltimateLoadout.BombardmentSlot
                ? GameLanguage.Text("БОМБАРДИРОВКА", "BOMBARDMENT")
                : selected == TankUltimateLoadout.ShieldSlot
                    ? GameLanguage.Text("ЩИТ", "SHIELD")
                    : GameLanguage.Text("РАКЕТА", "ROCKET");
        }

        bool ready = rewards.IsFullyCharged || rewards.IsSpecialArmed || shieldActive || bombardmentActive;
        if (chargeStatus != null)
        {
            chargeStatus.text = shieldActive
                ? GameLanguage.Text("ЩИТ АКТИВЕН", "SHIELD ACTIVE")
                : bombardmentPlanning
                    ? GameLanguage.Text("РИСУЙ ЗОНУ", "PAINT THE ZONE")
                    : bombardmentActive
                        ? GameLanguage.Text("ИДЁТ УДАР", "STRIKE ACTIVE")
                : rewards.IsSpecialArmed
                    ? GameLanguage.Text("ВЫБЕРИ ЦЕЛЬ", "SELECT TARGET")
                    : rewards.IsFullyCharged
                        ? GameLanguage.Text("НАЖМИ Q", "PRESS Q")
                        : GameLanguage.Text($"ЗАРЯД  {Mathf.RoundToInt(rewards.ChargeNormalized * 100f)}%", $"CHARGE  {Mathf.RoundToInt(rewards.ChargeNormalized * 100f)}%");
        }

        if (shortcutBackground != null)
        {
            float pulse = ready ? .92f + Mathf.Sin(Time.unscaledTime * 5f) * .08f : 1f;
            shortcutBackground.color = ready
                ? new Color(.96f * pulse, .71f * pulse, .30f * pulse, 1f)
                : new Color(.23f, .31f, .29f, 1f);
        }

        UpdateTargetMarker();
    }

    private void UpdateTargetMarker()
    {
        if (targetMarker == null)
        {
            return;
        }

        TankHealth target = specialWeapon != null ? specialWeapon.CurrentTarget : null;
        Camera camera = Camera.main;
        bool show = rewards != null && rewards.IsSpecialArmed && target != null && target.IsAlive && camera != null;
        targetMarker.gameObject.SetActive(show);
        if (!show)
        {
            return;
        }

        Vector3 screenPoint = camera.WorldToScreenPoint(TankSpecialWeapon.GetTargetPoint(target));
        targetMarker.rectTransform.position = screenPoint;
        targetMarker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 70f);
    }
}
