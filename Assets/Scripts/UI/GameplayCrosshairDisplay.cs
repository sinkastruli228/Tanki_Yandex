using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameplayCrosshairDisplay : MonoBehaviour
{
    [SerializeField] private TankShooter shooter;
    [SerializeField] private RectTransform recoilRing;
    [SerializeField] private Image reloadFill;
    [SerializeField] private Image centerDiamond;
    [SerializeField] private float baseRingSize = 64f;
    [SerializeField] private float recoilDistance = 11f;
    [SerializeField] private float recoilDuration = .24f;

    private TankShooter subscribedShooter;
    private float recoilAge = float.PositiveInfinity;
    private float currentRecoilExpansion;

    public Image ReloadFill => reloadFill;
    public Image CenterDiamond => centerDiamond;
    public RectTransform RecoilRing => recoilRing;
    public float CurrentRecoilExpansion => currentRecoilExpansion;

    public void Configure(TankShooter tankShooter, RectTransform ring, Image radialFill, Image diamond)
    {
        shooter = tankShooter;
        recoilRing = ring;
        reloadFill = radialFill;
        centerDiamond = diamond;
        Subscribe(tankShooter);
        ApplyVisuals();
    }

    private void OnEnable()
    {
        Subscribe(shooter);
    }

    private void OnDisable()
    {
        Subscribe(null);
    }

    private void Update()
    {
        UpdateRecoil();
        ApplyVisuals();
    }

    private void UpdateRecoil()
    {
        if (recoilRing == null || recoilAge >= recoilDuration)
        {
            SetRecoilExpansion(0f);
            return;
        }

        recoilAge += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(recoilAge / Mathf.Max(.01f, recoilDuration));
        float expansion;
        if (t < .28f)
        {
            float outward = t / .28f;
            expansion = recoilDistance * (1f - Mathf.Pow(1f - outward, 3f));
        }
        else
        {
            float returning = (t - .28f) / .72f;
            float smoothReturn = returning * returning * (3f - 2f * returning);
            expansion = recoilDistance * (1f - smoothReturn);
        }

        SetRecoilExpansion(expansion);
    }

    private void ApplyVisuals()
    {
        float reload = shooter != null ? shooter.ReloadNormalized : 1f;
        if (reloadFill != null)
        {
            reloadFill.fillAmount = reload;
            reloadFill.color = new Color(.98f, .95f, .87f, 1f);
        }

        if (centerDiamond != null)
        {
            Color ready = new Color(.96f, .71f, .30f, 1f);
            Color waiting = new Color(.57f, .36f, .13f, 1f);
            centerDiamond.color = Color.Lerp(waiting, ready, reload);
            float readyPulse = reload >= .995f ? .04f * (.5f + .5f * Mathf.Sin(Time.unscaledTime * 6f)) : 0f;
            centerDiamond.rectTransform.localScale = Vector3.one * (1f + readyPulse);
        }
    }

    private void HandleShot()
    {
        recoilAge = 0f;
        SetRecoilExpansion(recoilDistance * .12f);
        ApplyVisuals();
    }

    private void SetRecoilExpansion(float expansion)
    {
        currentRecoilExpansion = expansion;
        if (recoilRing != null)
        {
            float size = baseRingSize + expansion * 2f;
            recoilRing.sizeDelta = new Vector2(size, size);
        }
    }

    private void Subscribe(TankShooter target)
    {
        if (subscribedShooter == target)
        {
            return;
        }

        if (subscribedShooter != null)
        {
            subscribedShooter.Shot -= HandleShot;
        }

        subscribedShooter = target;
        if (subscribedShooter != null)
        {
            subscribedShooter.Shot += HandleShot;
        }
    }
}
