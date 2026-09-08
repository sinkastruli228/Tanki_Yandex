using UnityEngine;
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
    private readonly TankUpgradeType[] offers = new TankUpgradeType[3];
    private int pendingLevelUps;
    private bool selectionOpen;
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
    public bool IsSelectionOpen => selectionOpen;
    public float ExperienceNormalized => displayedExperience;
    public float WeaponMultiplier => CalculateMultiplier(cannonTier);
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

    public static float GetMobilityTierBonus(int tierIndex)
    {
        return MobilityTierBonuses[Mathf.Clamp(tierIndex, 0, MobilityTierBonuses.Length - 1)];
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
            experienceRequired = Mathf.Min(int.MaxValue, experienceRequired * 2);
            level++;
            leveledUp = true;
            if (HasAvailableUpgrade()) pendingLevelUps++;
        }

        targetExperience = leveledUp && pendingLevelUps > 0 ? 1f : CurrentExperienceNormalized();

        RefreshHud();
        TryOpenSelection();
    }

    public void ChooseCard(int cardIndex)
    {
        if (!selectionOpen || cardIndex < 0 || cardIndex >= offers.Length)
        {
            return;
        }

        TankUpgradeType selected = offers[cardIndex];
        if (GetTier(selected) >= MaximumUpgradeTier)
        {
            return;
        }

        SetTier(selected, GetTier(selected) + 1);
        ApplyUpgrades();
        pendingLevelUps = Mathf.Max(0, pendingLevelUps - 1);
        targetExperience = pendingLevelUps > 0 ? 1f : CurrentExperienceNormalized();

        if (pendingLevelUps > 0 && HasAvailableUpgrade())
        {
            BuildOffers();
            RefreshCopy();
        }
        else
        {
            CloseSelection();
        }

        RefreshHud();
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
        BuildOffers();
        if (choiceRoot != null)
        {
            choiceRoot.SetActive(true);
            choiceRoot.transform.SetAsLastSibling();
        }

        SetCombatControls(false);
        PlayerHealthBar.GameplayInputBlocked = true;
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        RefreshCopy();
    }

    private void CloseSelection()
    {
        selectionOpen = false;
        if (choiceRoot != null) choiceRoot.SetActive(false);
        if (health == null || !health.IsAlive) return;

        SetCombatControls(true);
        PlayerHealthBar.GameplayInputBlocked = false;
        Time.timeScale = 1f;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;
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
            cardButtons[i].interactable = available;
            if (cardIcons[i] != null) cardIcons[i].text = GetIcon(type);
            if (cardNames[i] != null) cardNames[i].text = GetName(type);
            if (cardDescriptions[i] != null) cardDescriptions[i].text = GetDescription(type);
            if (cardBonuses[i] != null)
            {
                float nextBonus = type == TankUpgradeType.Mobility
                    ? MobilityTierBonuses[Mathf.Clamp(tier, 0, MaximumUpgradeTier - 1)]
                    : TierBonuses[Mathf.Clamp(tier, 0, MaximumUpgradeTier - 1)];
                cardBonuses[i].text = available
                    ? $"+{Mathf.RoundToInt(nextBonus * 100f)}%"
                    : GameLanguage.Text("МАКСИМУМ", "MAXIMUM");
            }
            RefreshTierScale(i, tier);
        }
    }

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

    private static string GetDescription(TankUpgradeType type)
    {
        if (type == TankUpgradeType.Cannon) return GameLanguage.Text("Урон обычных и\nкритических выстрелов", "Normal and critical\nshot damage");
        if (type == TankUpgradeType.Armor) return GameLanguage.Text("Максимальное здоровье\nи ремонт брони", "Maximum health\nand armor repair");
        return GameLanguage.Text("Скорость танка и\nзапас закиси азота", "Tank speed and\nnitro capacity");
    }
}
