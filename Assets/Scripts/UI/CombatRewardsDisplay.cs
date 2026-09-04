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

    public void Configure(
        TankCombatRewards combatRewards,
        TankSpecialWeapon weapon,
        Text coinsLabel,
        Image radialFill,
        Image enemyTargetMarker)
    {
        rewards = combatRewards;
        specialWeapon = weapon;
        coinText = coinsLabel;
        chargeFill = radialFill;
        targetMarker = enemyTargetMarker;
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
            coinText.text = $"МОНЕТЫ: {rewards.Coins}";
        }

        if (chargeFill != null)
        {
            chargeFill.fillAmount = rewards.ChargeNormalized;
            chargeFill.color = rewards.IsSpecialArmed
                ? new Color(1f, 0.35f, 0.08f, 1f)
                : new Color(0.1f, 0.85f, 1f, 1f);
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
