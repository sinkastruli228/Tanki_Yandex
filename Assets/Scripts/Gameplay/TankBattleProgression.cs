using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum TankUpgradeType
{
    Cannon,
    Armor,
    Mobility
}

[DisallowMultipleComponent]
public sealed class TankBattleProgression : MonoBehaviour
{
    public const int MaximumUpgradeTier = 5;
    public const int MaximumLevel = MaximumUpgradeTier * 3 + 1;
    public const int BaseKillExperience = 100;
    public const int CriticalKillBonusExperience = 50;
    public const int InitialExperienceRequirement = 100;

    private static readonly float[] CannonTierBonuses = { .5f, .5f, .5f, .5f, .5f };
    private static readonly float[] TierBonuses = { .5f, .4f, .3f, .2f, .1f };
    private static readonly float[] MobilityTierBonuses = { .10f, .08f, .06f, .04f, .02f };

    [SerializeField] private TankHealth health;
    [SerializeField] private TankShooter shooter;
    [SerializeField] private TankController controller;
    [SerializeField] private TankNitro nitro;
    [SerializeField] private int level = 1;
    [SerializeField] private int experience;
    [SerializeField] private int experienceRequired = InitialExperienceRequirement;
    [SerializeField] private int cannonTier;
    [SerializeField] private int armorTier;
    [SerializeField] private int mobilityTier;

    private Image experienceFill;
    private Text levelLabel;
    private Text experienceLabel;
    private Text experienceCounter;
    private GameObject choiceRoot;
    private Text choiceTitle;
    private Text choiceSubtitle;
    private Button[] cardButtons;
    private Text[] cardIcons;
    private Text[] cardNames;
    private Text[] cardDescriptions;
    private Text[] cardBonuses;
    private RectTransform selectorPanel;
    private Image overlayImage;
    private Vector2 selectorHomePosition;
    private float overlayVisibleAlpha;
    private Coroutine selectionRoutine;
    private readonly TankUpgradeType[] offers = new TankUpgradeType[3];
    private int pendingLevelUps;
    private bool selectionOpen;
    private bool selectionAnimating;
    private bool choiceCommitted;
    private bool initialized;
    private float displayedExperience;
    private float targetExperience;
    private float experienceMaxWidth;
    private float experienceHeight;
    private int lastCompletedRequirement;

    public int Level => level;
    public int Experience => experience;
    public int ExperienceRequired => experienceRequired;
    public int CannonTier => cannonTier;
    public int ArmorTier => armorTier;
    public int MobilityTier => mobilityTier;
    public int TotalUpgradeTierCount => cannonTier + armorTier + mobilityTier;
    public bool IsSelectionOpen => selectionOpen;
    public bool IsSelectionAnimating => selectionAnimating;
    public float ExperienceNormalized => displayedExperience;
    public float WeaponMultiplier => CalculateCannonMultiplier(cannonTier);
    public float HealthMultiplier => CalculateMultiplier(armorTier);
    public float SpeedMultiplier => CalculateMobilityMultiplier(mobilityTier);
    public float NitroCapacityMultiplier => CalculateMobilityMultiplier(mobilityTier);
    public Image ExperienceFill => experienceFill;
    public GameObject ChoiceRoot => choiceRoot;

    public TankUpgradeType GetOffer(int index)
    {
        return offers[Mathf.Clamp(index, 0, offers.Length - 1)];
    }

    public static float GetTierBonus(int tierIndex)
    {
        return TierBonuses[Mathf.Clamp(tierIndex, 0, TierBonuses.Length - 1)];
    }

    public static float GetCannonTierBonus(int tierIndex)
    {
        return CannonTierBonuses[Mathf.Clamp(tierIndex, 0, CannonTierBonuses.Length - 1)];
    }

    public static float GetMobilityTierBonus(int tierIndex)
    {
        return MobilityTierBonuses[Mathf.Clamp(tierIndex, 0, MobilityTierBonuses.Length - 1)];
    }

    public static int GetExperienceRequirementGrowthPercent(int currentLevel)
    {
        currentLevel = Mathf.Max(1, currentLevel);
        if (currentLevel == 1) return 50;
        if (currentLevel == 2) return 40;
        if (currentLevel == 3) return 30;
        if (currentLevel == 4) return 20;
        return Mathf.Max(1, 15 - currentLevel);
    }

    public static int CalculateNextExperienceRequirement(int currentRequirement, int currentLevel)
    {
        int safeRequirement = Mathf.Max(1, currentRequirement);
        int growthPercent = GetExperienceRequirementGrowthPercent(currentLevel);
        return Mathf.Max(safeRequirement + 1, Mathf.CeilToInt(safeRequirement * (1f + growthPercent / 100f)));
    }

    public void ConfigureGameplay(TankHealth playerHealth, TankShooter playerShooter, TankController playerController)
    {
        health = playerHealth;
        shooter = playerShooter;
        controller = playerController;
        nitro = GetComponent<TankNitro>();
        if (!initialized)
        {
            initialized = true;
            level = 1;
            experience = 0;
            experienceRequired = InitialExperienceRequirement;
            lastCompletedRequirement = 0;
            cannonTier = armorTier = mobilityTier = 0;
            pendingLevelUps = 0;
            displayedExperience = targetExperience = 0f;
            EnemyLevelScaling.ResetForBattle();
        }
        ApplyUpgrades();
    }

    public void ConfigureUi(
        Image xpFill,
        Text levelText,
        Text xpText,
        Text counterText,
        GameObject selector,
        Text selectorTitle,
        Text selectorSubtitle,
        Button[] buttons,
        Text[] icons,
        Text[] names,
        Text[] descriptions,
        Text[] bonuses)
    {
        experienceFill = xpFill;
        levelLabel = levelText;
        experienceLabel = xpText;
        experienceCounter = counterText;
        choiceRoot = selector;
        choiceTitle = selectorTitle;
        choiceSubtitle = selectorSubtitle;
        cardButtons = buttons;
        cardIcons = icons;
        cardNames = names;
        cardDescriptions = descriptions;
        cardBonuses = bonuses;
        selectorPanel = choiceRoot != null ? choiceRoot.transform.Find("Upgrade Card Panel") as RectTransform : null;
        overlayImage = choiceRoot != null ? choiceRoot.GetComponent<Image>() : null;
        if (selectorPanel != null)
        {
            selectorHomePosition = selectorPanel.anchoredPosition;
        }
        overlayVisibleAlpha = overlayImage != null ? overlayImage.color.a : 0f;

        if (experienceFill != null && experienceFill.rectTransform.parent is RectTransform track)
        {
            experienceMaxWidth = Mathf.Max(1f, track.rect.width - 8f);
            experienceHeight = Mathf.Max(1f, track.rect.height - 4f);
            RectTransform rect = experienceFill.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(4f, 0f);
        }

        for (int i = 0; i < cardButtons.Length; i++)
        {
            int cardIndex = i;
            cardButtons[i].onClick.RemoveAllListeners();
            cardButtons[i].onClick.AddListener(() => ChooseCard(cardIndex));
        }

        if (choiceRoot != null) choiceRoot.SetActive(selectionOpen);
        RefreshCopy();
        RefreshHud();
    }

    public void RegisterEnemyKill(bool killedWithCritical = false)
    {
        if (!initialized || health == null || !health.IsAlive || level >= MaximumLevel)
        {
            return;
        }

        experience += BaseKillExperience + (killedWithCritical ? CriticalKillBonusExperience : 0);
        bool leveledUp = false;
        while (level < MaximumLevel && experience >= experienceRequired)
        {
            experience -= experienceRequired;
            lastCompletedRequirement = experienceRequired;
            experienceRequired = CalculateNextExperienceRequirement(experienceRequired, level);
            level++;
            leveledUp = true;
            if (HasAvailableUpgrade()) pendingLevelUps++;
        }

        if (leveledUp)
        {
            EnemyLevelScaling.SetPlayerLevel(level);
        }

        targetExperience = leveledUp && pendingLevelUps > 0 ? 1f : CurrentExperienceNormalized();

        RefreshHud();
        TryOpenSelection();
    }

    public void ChooseCard(int cardIndex)
    {
        if (!selectionOpen || selectionAnimating || cardIndex < 0 || cardIndex >= offers.Length)
        {
            return;
        }

        TankUpgradeType selected = offers[cardIndex];
        int previousTier = GetTier(selected);
        if (previousTier >= MaximumUpgradeTier)
        {
            return;
        }

        SetTier(selected, previousTier + 1);
        ApplyUpgrades();
        pendingLevelUps = Mathf.Max(0, pendingLevelUps - 1);
        targetExperience = pendingLevelUps > 0 ? 1f : CurrentExperienceNormalized();
        RefreshHud();

        choiceCommitted = true;
        selectionAnimating = true;
        SetCardButtonsInteractable(false);
        if (selectionRoutine != null)
        {
            StopCoroutine(selectionRoutine);
        }
        selectionRoutine = StartCoroutine(AnimateChosenUpgrade(cardIndex, previousTier));
    }

    private void OnEnable()
    {
        GameLanguage.Changed += RefreshCopy;
    }

    private void OnDisable()
    {
        GameLanguage.Changed -= RefreshCopy;
    }

    private void Update()
    {
        displayedExperience = Mathf.MoveTowards(displayedExperience, targetExperience, Time.unscaledDeltaTime * 4.5f);
        RefreshExperienceFill();
        if (!selectionOpen && pendingLevelUps > 0)
        {
            TryOpenSelection();
        }
    }

    private void TryOpenSelection()
    {
        if (selectionOpen || pendingLevelUps <= 0 || !HasAvailableUpgrade() || PlayerHealthBar.GameplayInputBlocked)
        {
            return;
        }

        TankBombardmentUltimate bombardment = GetComponent<TankBombardmentUltimate>();
        if (bombardment != null && bombardment.IsActive)
        {
            return;
        }

        selectionOpen = true;
        choiceCommitted = false;
        BuildOffers();
        if (choiceRoot != null)
        {
            choiceRoot.SetActive(true);
            choiceRoot.transform.SetAsLastSibling();
        }

        SetCombatControls(false);
        GameplayModalState.Set(GameplayBlockReason.UpgradeSelection, true, true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        RefreshCopy();
        selectionAnimating = true;
        SetCardButtonsInteractable(false);
        if (selectionRoutine != null)
        {
            StopCoroutine(selectionRoutine);
        }
        selectionRoutine = StartCoroutine(AnimateSelectionEntrance());
    }

    private void CompleteSelection()
    {
        selectionOpen = false;
        choiceCommitted = false;
        if (choiceRoot != null) choiceRoot.SetActive(false);
        selectionAnimating = false;
        selectionRoutine = null;
        if (health == null || !health.IsAlive)
        {
            return;
        }

        SetCombatControls(true);
        GameplayModalState.Set(GameplayBlockReason.UpgradeSelection, false, true);
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;

        if (pendingLevelUps > 0 && HasAvailableUpgrade())
        {
            TryOpenSelection();
        }
    }

    private void BuildOffers()
    {
        offers[0] = TankUpgradeType.Cannon;
        offers[1] = TankUpgradeType.Armor;
        offers[2] = TankUpgradeType.Mobility;
        for (int i = offers.Length - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            (offers[i], offers[swap]) = (offers[swap], offers[i]);
        }
    }

    private void RefreshCopy()
    {
        if (choiceTitle != null) choiceTitle.text = GameLanguage.Text("НОВЫЙ УРОВЕНЬ", "LEVEL UP");
        if (choiceSubtitle != null) choiceSubtitle.text = GameLanguage.Text("ВЫБЕРИ ОДНО УЛУЧШЕНИЕ", "CHOOSE ONE UPGRADE");
        if (levelLabel != null) levelLabel.text = GameLanguage.Text($"УР. {level}", $"LVL {level}");
        if (experienceLabel != null) experienceLabel.text = GameLanguage.Text("ОПЫТ", "XP");
        if (experienceCounter != null) experienceCounter.text = FormatExperienceCounter();

        if (cardButtons == null) return;
        for (int i = 0; i < cardButtons.Length && i < offers.Length; i++)
        {
            TankUpgradeType type = offers[i];
            int tier = GetTier(type);
            bool available = tier < MaximumUpgradeTier;
            cardButtons[i].interactable = available && !selectionAnimating;
            if (cardIcons[i] != null) cardIcons[i].text = GetIcon(type);
            if (cardNames[i] != null) cardNames[i].text = GetName(type);
            if (cardDescriptions[i] != null)
            {
                cardDescriptions[i].text = string.Empty;
                cardDescriptions[i].gameObject.SetActive(false);
            }
            if (cardBonuses[i] != null)
            {
                cardBonuses[i].text = available
                    ? GameLanguage.Text("ПРОКАЧАТЬ", "UPGRADE")
                    : GameLanguage.Text("МАКСИМУМ", "MAXIMUM");
            }
            RefreshTierScale(i, tier);
        }
    }

    private IEnumerator AnimateSelectionEntrance()
    {
        if (selectorPanel == null)
        {
            selectionAnimating = false;
            RefreshCopy();
            SelectFirstAvailableCard();
            selectionRoutine = null;
            yield break;
        }

        const float duration = .52f;
        Vector2 startPosition = selectorHomePosition + Vector2.up * GetSelectionTravelDistance();
        SetOverlayAlpha(0f);
        selectorPanel.anchoredPosition = startPosition;
        selectorPanel.localScale = Vector3.one * .97f;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = OutBack(progress);
            selectorPanel.anchoredPosition = Vector2.LerpUnclamped(startPosition, selectorHomePosition, eased);
            selectorPanel.localScale = Vector3.one * Mathf.LerpUnclamped(.97f, 1f, eased);
            SetOverlayAlpha(overlayVisibleAlpha * Mathf.SmoothStep(0f, 1f, progress));
            yield return null;
        }

        selectorPanel.anchoredPosition = selectorHomePosition;
        selectorPanel.localScale = Vector3.one;
        SetOverlayAlpha(overlayVisibleAlpha);
        selectionAnimating = false;
        RefreshCopy();
        SelectFirstAvailableCard();
        selectionRoutine = null;
    }

    private IEnumerator AnimateChosenUpgrade(int cardIndex, int previousTier)
    {
        Image card = cardButtons != null && cardIndex >= 0 && cardIndex < cardButtons.Length
            ? cardButtons[cardIndex].targetGraphic as Image
            : null;
        Image icon = card != null ? card.transform.Find("Icon")?.GetComponent<Image>() : null;
        Image segment = card != null
            ? card.transform.Find($"Tier Scale/Segment {previousTier + 1}")?.GetComponent<Image>()
            : null;
        Outline glow = card != null ? card.GetComponent<Outline>() : null;
        if (card != null && glow == null)
        {
            glow = card.gameObject.AddComponent<Outline>();
            glow.useGraphicAlpha = true;
            glow.effectDistance = new Vector2(4f, -4f);
        }

        Color gold = new Color(.96f, .71f, .30f, 1f);
        Color cardColor = card != null ? card.color : Color.white;
        Color iconColor = icon != null ? icon.color : Color.white;
        Color segmentColor = segment != null ? segment.color : Color.clear;
        const float duration = .55f;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            float fill = Mathf.SmoothStep(0f, 1f, progress);
            float pulse = Mathf.Sin(progress * Mathf.PI);

            if (segment != null)
            {
                segment.color = Color.Lerp(segmentColor, gold, fill);
                segment.rectTransform.localScale = Vector3.one * (1f + pulse * .42f);
            }
            if (card != null)
            {
                card.color = Color.Lerp(cardColor, new Color(.58f, .48f, .25f, 1f), pulse * .78f);
                card.rectTransform.localScale = Vector3.one * (1f + pulse * .025f);
            }
            if (icon != null)
            {
                icon.color = Color.Lerp(iconColor, new Color(.74f, .52f, .20f, 1f), pulse * .8f);
            }
            if (glow != null)
            {
                glow.effectColor = new Color(gold.r, gold.g, gold.b, pulse * .9f);
            }

            yield return null;
        }

        if (segment != null)
        {
            segment.color = gold;
            segment.rectTransform.localScale = Vector3.one;
        }
        if (card != null)
        {
            card.color = cardColor;
            card.rectTransform.localScale = Vector3.one;
        }
        if (icon != null) icon.color = iconColor;
        if (glow != null) glow.effectColor = new Color(gold.r, gold.g, gold.b, 0f);

        yield return new WaitForSecondsRealtime(.14f);
        yield return AnimateSelectionExit();
        CompleteSelection();
    }

    private IEnumerator AnimateSelectionExit()
    {
        if (selectorPanel == null)
        {
            yield break;
        }

        const float duration = .42f;
        Vector2 endPosition = selectorHomePosition + Vector2.up * GetSelectionTravelDistance();
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = InBack(progress);
            selectorPanel.anchoredPosition = Vector2.LerpUnclamped(selectorHomePosition, endPosition, eased);
            selectorPanel.localScale = Vector3.one * Mathf.Lerp(1f, .97f, progress);
            SetOverlayAlpha(overlayVisibleAlpha * (1f - Mathf.SmoothStep(0f, 1f, progress)));
            yield return null;
        }

        selectorPanel.anchoredPosition = selectorHomePosition;
        selectorPanel.localScale = Vector3.one;
        SetOverlayAlpha(overlayVisibleAlpha);
    }

    private void SetCardButtonsInteractable(bool interactable)
    {
        if (cardButtons == null)
        {
            return;
        }

        foreach (Button cardButton in cardButtons)
        {
            if (cardButton != null) cardButton.interactable = interactable;
        }
    }

    private void SelectFirstAvailableCard()
    {
        if (EventSystem.current == null || cardButtons == null)
        {
            return;
        }

        foreach (Button cardButton in cardButtons)
        {
            if (cardButton != null && cardButton.IsInteractable())
            {
                EventSystem.current.SetSelectedGameObject(cardButton.gameObject);
                return;
            }
        }
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (overlayImage == null)
        {
            return;
        }

        Color color = overlayImage.color;
        color.a = alpha;
        overlayImage.color = color;
    }

    private float GetSelectionTravelDistance()
    {
        return Mathf.Max(720f, selectorPanel != null ? selectorPanel.rect.height + 320f : 720f);
    }

    private static float OutBack(float value)
    {
        const float overshoot = 1.70158f;
        float shifted = value - 1f;
        return 1f + shifted * shifted * ((overshoot + 1f) * shifted + overshoot);
    }

    private static float InBack(float value)
    {
        const float overshoot = 1.45f;
        return value * value * ((overshoot + 1f) * value - overshoot);
    }

#if UNITY_EDITOR
    public void CompleteSelectionAnimationForTests()
    {
        if (!selectionAnimating)
        {
            return;
        }

        if (selectionRoutine != null)
        {
            StopCoroutine(selectionRoutine);
            selectionRoutine = null;
        }
        if (selectorPanel != null)
        {
            selectorPanel.anchoredPosition = selectorHomePosition;
            selectorPanel.localScale = Vector3.one;
        }
        SetOverlayAlpha(overlayVisibleAlpha);

        if (choiceCommitted)
        {
            CompleteSelection();
            return;
        }

        selectionAnimating = false;
        RefreshCopy();
        SelectFirstAvailableCard();
    }
#endif

    private void RefreshHud()
    {
        if (levelLabel != null) levelLabel.text = GameLanguage.Text($"УР. {level}", $"LVL {level}");
        if (experienceCounter != null) experienceCounter.text = FormatExperienceCounter();
        RefreshExperienceFill();
    }

    private void RefreshExperienceFill()
    {
        if (experienceFill == null) return;
        experienceFill.fillAmount = displayedExperience;
        experienceFill.rectTransform.sizeDelta = new Vector2(experienceMaxWidth * displayedExperience, experienceHeight);
        experienceFill.enabled = displayedExperience > .001f;
    }

    private void ApplyUpgrades()
    {
        shooter?.SetBattleUpgradeMultiplier(WeaponMultiplier);
        health?.SetBattleMaxHealthMultiplier(HealthMultiplier);
        controller?.SetBattleSpeedMultiplier(SpeedMultiplier);
        nitro?.SetBattleCapacityMultiplier(NitroCapacityMultiplier);
    }

    private void SetCombatControls(bool enabledState)
    {
        if (controller != null) controller.enabled = enabledState;
        if (shooter != null) shooter.enabled = enabledState;
        TankTurretAim turret = GetComponent<TankTurretAim>();
        if (turret != null) turret.enabled = enabledState;
        TankAimLaser laser = GetComponent<TankAimLaser>();
        if (laser != null) laser.enabled = enabledState;
        TankNitro nitro = GetComponent<TankNitro>();
        if (nitro != null) nitro.enabled = enabledState;
        TankSpecialWeapon special = GetComponent<TankSpecialWeapon>();
        if (special != null) special.enabled = enabledState;
    }

    private bool HasAvailableUpgrade()
    {
        return cannonTier < MaximumUpgradeTier || armorTier < MaximumUpgradeTier || mobilityTier < MaximumUpgradeTier;
    }

    private int GetTier(TankUpgradeType type)
    {
        return type == TankUpgradeType.Cannon ? cannonTier : type == TankUpgradeType.Armor ? armorTier : mobilityTier;
    }

    private void SetTier(TankUpgradeType type, int tier)
    {
        tier = Mathf.Clamp(tier, 0, MaximumUpgradeTier);
        if (type == TankUpgradeType.Cannon) cannonTier = tier;
        else if (type == TankUpgradeType.Armor) armorTier = tier;
        else mobilityTier = tier;
    }

    private static float CalculateMultiplier(int tier)
    {
        float multiplier = 1f;
        for (int i = 0; i < Mathf.Clamp(tier, 0, MaximumUpgradeTier); i++) multiplier += TierBonuses[i];
        return multiplier;
    }

    private static float CalculateCannonMultiplier(int tier)
    {
        float multiplier = 1f;
        for (int i = 0; i < Mathf.Clamp(tier, 0, MaximumUpgradeTier); i++) multiplier += CannonTierBonuses[i];
        return multiplier;
    }

    private static float CalculateMobilityMultiplier(int tier)
    {
        float multiplier = 1f;
        for (int i = 0; i < Mathf.Clamp(tier, 0, MaximumUpgradeTier); i++) multiplier += MobilityTierBonuses[i];
        return multiplier;
    }

    private float CurrentExperienceNormalized()
    {
        return level >= MaximumLevel ? 1f : Mathf.Clamp01((float)experience / Mathf.Max(1, experienceRequired));
    }

    private string FormatExperienceCounter()
    {
        if (level >= MaximumLevel) return "MAX";
        if (selectionOpen && lastCompletedRequirement > 0) return $"{lastCompletedRequirement} / {lastCompletedRequirement}";
        return $"{experience} / {experienceRequired}";
    }

    private void RefreshTierScale(int cardIndex, int tier)
    {
        if (cardButtons == null || cardIndex < 0 || cardIndex >= cardButtons.Length || cardButtons[cardIndex] == null) return;
        Transform scale = cardButtons[cardIndex].transform.Find("Tier Scale");
        if (scale == null) return;
        Color filled = new Color(.96f, .71f, .30f, 1f);
        Color empty = new Color(.095f, .14f, .14f, .82f);
        for (int segment = 0; segment < MaximumUpgradeTier; segment++)
        {
            Image image = scale.Find($"Segment {segment + 1}")?.GetComponent<Image>();
            if (image != null) image.color = segment < tier ? filled : empty;
        }
    }

    private static string GetIcon(TankUpgradeType type)
    {
        return type == TankUpgradeType.Cannon ? "III" : type == TankUpgradeType.Armor ? "+" : "N₂O";
    }

    private static string GetName(TankUpgradeType type)
    {
        if (type == TankUpgradeType.Cannon) return GameLanguage.Text("ОРУДИЕ", "CANNON");
        if (type == TankUpgradeType.Armor) return GameLanguage.Text("БРОНЯ", "ARMOR");
        return GameLanguage.Text("ДВИГАТЕЛЬ", "ENGINE");
    }

}
