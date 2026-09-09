using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class GameplayUiAndDefeatChecks
{
    private const string Folder = ".utmp/gameplay-ui-defeat/";
    private const string RunningKey = "GameplayUiAndDefeatChecks.Running";

    private static bool running;
    private static int stage;
    private static double due;
    private static double deadline;

    static GameplayUiAndDefeatChecks()
    {
        if (SessionState.GetBool(RunningKey, false)) Resume();
    }

    [MenuItem("Tools/Tanki/Check Gameplay UI And Defeat")]
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "checks.txt", "Gameplay UI and defeat checks\n");
        SessionState.SetBool(RunningKey, true);
        Resume();
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
    }

    private static void Resume()
    {
        running = true;
        stage = 0;
        due = EditorApplication.timeSinceStartup + 1.5;
        deadline = due + 55;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!running || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < due) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Gameplay UI and defeat check timed out");
            switch (stage)
            {
                case 0: LaunchBattle(); break;
                case 1: CheckHudAndDefeat(); break;
                case 2: CheckDefeatState(); break;
                case 3: RestartFromDefeat(); break;
                case 4: CheckDirectRestartAndDefeatAgain(); break;
                case 5: ReturnToMenu(); break;
                case 6: CheckMenuReturn(); break;
            }
        }
        catch (Exception error)
        {
            File.AppendAllText(Folder + "checks.txt", "FAIL: " + error + "\n");
            Debug.LogException(error);
            Finish();
        }
    }

    private static void LaunchBattle()
    {
        MainMenuController menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
        GarageMenuView view = UnityEngine.Object.FindAnyObjectByType<GarageMenuView>();
        if (menu == null || view == null || menu.IsBusy)
        {
            due = EditorApplication.timeSinceStartup + .1;
            return;
        }

        view.PlayButton.onClick.Invoke();
        stage = 1;
        due = EditorApplication.timeSinceStartup + 1.8;
    }

    private static void CheckHudAndDefeat()
    {
        TankHealth health = FindPlayer();
        if (health == null || PlayerHealthBar.GameplayInputBlocked)
        {
            due = EditorApplication.timeSinceStartup + .1;
            return;
        }

        foreach (EnemyWaveSpawner spawner in UnityEngine.Object.FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None))
            UnityEngine.Object.Destroy(spawner.gameObject);

        RectTransform healthPanel = GameObject.Find("Health Bar Background")?.GetComponent<RectTransform>();
        RectTransform nitroPanel = GameObject.Find("Nitro Bar Background")?.GetComponent<RectTransform>();
        Check(healthPanel != null && healthPanel.rect.size == new Vector2(260f, 76f), "Health HUD uses a compact menu-style card");
        Check(healthPanel.Find("Health Icon/Plus")?.GetComponent<Text>().text == "+", "Health is marked with a plus icon");
        Image healthFill = healthPanel.Find("Health Track/Health Bar Fill")?.GetComponent<Image>();
        Check(healthFill != null && healthFill.type == Image.Type.Simple && healthFill.sprite.name.Contains("Brush"), "Health uses an unclipped brush-stroke fill");
        Check(healthFill.rectTransform.rect.width <= healthFill.rectTransform.parent.GetComponent<RectTransform>().rect.width - 7f, "Health fill keeps safe space at both track edges");
        Check(nitroPanel != null && nitroPanel.rect.size == new Vector2(260f, 76f), "Nitro HUD uses a matching menu-style card");
        Check(nitroPanel.Find("Nitro Icon/Nitro Symbol")?.GetComponent<Text>().text == "N₂O", "Nitro is labelled N2O");
        NitroBarDisplay nitroDisplay = nitroPanel.GetComponent<NitroBarDisplay>();
        Check(nitroDisplay != null && nitroDisplay.GlowImage != null && !nitroDisplay.GlowImage.gameObject.activeSelf, "Nitro glow is prepared and idle until boosting");
        Check(nitroDisplay.FillImage.type == Image.Type.Simple && nitroDisplay.FillImage.sprite.name.Contains("Brush"), "Nitro uses an unclipped brush-stroke fill");
        Image ultimateFill = GameObject.Find("Special Charge Fill")?.GetComponent<Image>();
        Check(ultimateFill != null && ultimateFill.type == Image.Type.Simple && ultimateFill.sprite.name.Contains("Brush"), "Ultimate uses the same brush-stroke fill");
        Text ultimateShortcut = GameObject.Find("E Shortcut")?.transform.Find("Label")?.GetComponent<Text>();
        Check(ultimateShortcut != null && ultimateShortcut.text == "E", "Ultimate HUD shows the E keyboard shortcut");

        TankBattleProgression progression = health.GetComponent<TankBattleProgression>();
        Check(progression != null && progression.Level == 1 && TankBattleProgression.MaximumUpgradeTier == 5, "Battle progression starts at level 1 with five tiers per branch");
        Check(progression.Experience == 0 && progression.ExperienceRequired == 100, "The first level requires 100 XP");
        Check(TankBattleProgression.BaseKillExperience == 100 && TankBattleProgression.CriticalKillBonusExperience == 50, "Kills grant 100 XP and critical kills add 50 XP");
        Check(progression.ExperienceFill != null && progression.ExperienceFill.sprite.name.Contains("Brush"), "Top-center experience bar matches the brush-stroke HUD style");
        float[] expectedBonuses = { .5f, .4f, .3f, .2f, .1f };
        int[] expectedExperienceGrowth = { 50, 40, 30, 20, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 };
        float[] expectedMobilityBonuses = { .10f, .08f, .06f, .04f, .02f };
        for (int i = 0; i < expectedBonuses.Length; i++)
        {
            Check(Mathf.Approximately(TankBattleProgression.GetCannonTierBonus(i), .5f), $"Cannon tier {i + 1} grants 50 percent to normal and critical damage");
            Check(Mathf.Approximately(TankBattleProgression.GetTierBonus(i), expectedBonuses[i]), $"Upgrade tier {i + 1} grants the configured descending bonus");
            Check(Mathf.Approximately(TankBattleProgression.GetMobilityTierBonus(i), expectedMobilityBonuses[i]), $"Mobility tier {i + 1} grants the configured speed and nitro bonus");
        }
        for (int i = 0; i < expectedExperienceGrowth.Length; i++)
        {
            Check(TankBattleProgression.GetExperienceRequirementGrowthPercent(i + 1) == expectedExperienceGrowth[i], $"XP requirement growth at level {i + 1} follows the non-doubling curve");
        }
        Check(TankBattleProgression.CalculateNextExperienceRequirement(100, 1) == 150
            && TankBattleProgression.CalculateNextExperienceRequirement(150, 2) == 210,
            "XP requirement starts with 100, 150, and 210 instead of doubling");

        TankShooter playerShooter = health.GetComponent<TankShooter>();
        MethodInfo rollDamage = typeof(TankShooter).GetMethod("RollShotDamage", BindingFlags.Instance | BindingFlags.NonPublic);
        int normalRolls = 0;
        int criticalRolls = 0;
        bool normalRangeValid = true;
        bool criticalRangeValid = true;
        for (int i = 0; i < 512; i++)
        {
            object[] arguments = { false };
            int rolledDamage = (int)rollDamage.Invoke(playerShooter, arguments);
            bool critical = (bool)arguments[0];
            if (critical)
            {
                criticalRolls++;
                criticalRangeValid &= rolledDamage >= 38 && rolledDamage <= 50;
            }
            else
            {
                normalRolls++;
                normalRangeValid &= rolledDamage >= 18 && rolledDamage <= 28;
            }
        }
        Check(normalRangeValid, "Normal player damage stays inside 18-28");
        Check(criticalRangeValid, "Critical player damage stays inside 38-50");
        Check(normalRolls > 0 && criticalRolls > 0, "Player damage produces both normal and 15-percent critical rolls");
        float observedCriticalRate = criticalRolls / 512f;
        Check(observedCriticalRate > .08f && observedCriticalRate < .22f, "Observed critical rate stays close to 15 percent");

        GameObject damageTargetObject = new GameObject("Damage Color Check Target");
        TankHealth damageTarget = damageTargetObject.AddComponent<TankHealth>();
        damageTarget.Configure(TankTeam.Enemy, 500, false);
        damageTarget.TakeDamage(35);
        Check(!EnemyDamageNumberDisplay.LastShownCritical && EnemyDamageNumberDisplay.LastShownColor.r > .95f && EnemyDamageNumberDisplay.LastShownColor.g > .9f, "Normal and special damage numbers use warm white");
        damageTarget.TakeDamage(45, true);
        Check(EnemyDamageNumberDisplay.LastShownCritical && EnemyDamageNumberDisplay.LastShownColor.r > .95f && EnemyDamageNumberDisplay.LastShownColor.g < .3f, "Critical damage numbers use red");
        damageTarget.TakeDamage(100);
        Check(!EnemyDamageNumberDisplay.LastShownCritical && EnemyDamageNumberDisplay.LastShownColor.g > .9f, "Special attack damage numbers stay white");
        UnityEngine.Object.Destroy(damageTargetObject);

        float baseProjectileSpeed = playerShooter.EffectiveProjectileSpeed;
        float baseCooldown = playerShooter.EffectiveShotCooldown;
        float baseNitroCapacity = health.GetComponent<TankNitro>().Capacity;
        GameObject scalingEnemy = new GameObject("Level Scaling Check Enemy", typeof(Rigidbody));
        TankController scalingController = scalingEnemy.AddComponent<TankController>();
        TankHealth scalingHealth = scalingEnemy.AddComponent<TankHealth>();
        scalingHealth.Configure(TankTeam.Enemy, 100, false);
        StaticEnemyTank scalingAi = scalingEnemy.AddComponent<StaticEnemyTank>();
        scalingAi.Configure(health, null, null, null, 0f, 40, 0f, 0f, 1f, Vector3.forward);
        TankWorldHealthBar scalingBar = scalingEnemy.AddComponent<TankWorldHealthBar>();
        scalingBar.Configure(scalingHealth, Camera.main);
        EnemyLevelScaling.ApplyToEnemy(scalingEnemy);
        Check(scalingHealth.MaxHealth == 100 && scalingBar.SegmentCount == 1, "Enemy starts at its base stats and with one health-bar section");
        scalingHealth.TakeDamage(40);
        progression.RegisterEnemyKill(false);
        Check(progression.Level == 2 && progression.Experience == 0 && progression.ExperienceRequired == 150, "A normal kill reaches level 2 and grows the next XP requirement by 50 percent");
        Check(Mathf.Approximately(EnemyLevelScaling.CurrentMultiplier, 1.2f)
            && scalingHealth.MaxHealth == 120
            && scalingHealth.CurrentHealth == 72
            && Mathf.Approximately(scalingController.BattleSpeedMultiplier, 1f)
            && scalingAi.EffectiveDamage == 48,
            "Level 2 raises enemy health and damage by 20 percent without changing speed");
        Check(scalingBar.SegmentCount == 2, "Enemy health above 100 is split into multiple visible sections");
        Check(progression.IsSelectionOpen && Time.timeScale == 0f, "Level-up pauses and darkens the battle for an upgrade choice");
        Check(progression.IsSelectionAnimating, "Upgrade selection enters from above with an unscaled-time bounce");
        Transform selectorPanel = progression.ChoiceRoot.transform.Find("Upgrade Card Panel");
        Check(selectorPanel != null && ((RectTransform)selectorPanel).anchoredPosition.y > 500f, "Upgrade panel starts above the screen");
        Text firstDescription = selectorPanel?.Find("Upgrade Card 1/Description")?.GetComponent<Text>();
        Text firstAction = selectorPanel?.Find("Upgrade Card 1/Bonus/Text")?.GetComponent<Text>();
        Check(firstDescription != null && !firstDescription.gameObject.activeSelf, "Upgrade cards hide their short descriptions");
        Check(firstAction != null && firstAction.text == GameLanguage.Text("ПРОКАЧАТЬ", "UPGRADE"), "Upgrade buttons use a clear action label instead of percentages");
        Transform tierScale = progression.ChoiceRoot.transform.Find("Upgrade Card Panel/Upgrade Card 1/Tier Scale");
        Check(tierScale != null && tierScale.childCount == 5, "Upgrade cards show a five-cell branch scale");
        progression.CompleteSelectionAnimationForTests();
        int cannonCard = FindOffer(progression, TankUpgradeType.Cannon);
        progression.ChooseCard(cannonCard);
        Check(progression.IsSelectionAnimating, "Choosing an upgrade starts the cell-fill and card-glow feedback");
        progression.CompleteSelectionAnimationForTests();
        Check(progression.CannonTier == 1 && Mathf.Approximately(progression.WeaponMultiplier, 1.5f), "First cannon upgrade grants 50 percent");
        Check(Mathf.Approximately(playerShooter.EffectiveProjectileSpeed, baseProjectileSpeed) && Mathf.Approximately(playerShooter.EffectiveShotCooldown, baseCooldown), "Cannon upgrade changes damage without changing shell speed or reload");
        bool upgradedDamageValid = true;
        for (int i = 0; i < 256; i++)
        {
            object[] arguments = { false };
            int rolledDamage = (int)rollDamage.Invoke(playerShooter, arguments);
            bool critical = (bool)arguments[0];
            upgradedDamageValid &= critical
                ? rolledDamage >= 57 && rolledDamage <= 75
                : rolledDamage >= 27 && rolledDamage <= 42;
        }
        Check(upgradedDamageValid, "Cannon percentage scales both normal and critical damage ranges");

        progression.RegisterEnemyKill(true);
        Check(progression.Level == 3 && progression.Experience == 0 && progression.ExperienceRequired == 210, "A critical kill reaches the 150 XP threshold and advances to the 40-percent requirement step");
        Check(Mathf.Approximately(EnemyLevelScaling.CurrentMultiplier, 1.44f)
            && scalingHealth.MaxHealth == Mathf.RoundToInt(100f * 1.44f)
            && scalingHealth.CurrentHealth == Mathf.RoundToInt(scalingHealth.MaxHealth * .6f)
            && Mathf.Approximately(scalingController.BattleSpeedMultiplier, 1f)
            && scalingAi.EffectiveDamage == Mathf.RoundToInt(40f * 1.44f),
            "A second level-up compounds enemy health and damage while speed stays unchanged");
        progression.CompleteSelectionAnimationForTests();
        progression.ChooseCard(FindOffer(progression, TankUpgradeType.Armor));
        progression.CompleteSelectionAnimationForTests();
        Check(progression.ArmorTier == 1 && health.MaxHealth == Mathf.RoundToInt(health.BaseMaxHealth * 1.5f), "Armor upgrade increases maximum health and repairs the added capacity");
        progression.RegisterEnemyKill(false);
        progression.RegisterEnemyKill(false);
        Check(progression.Level == 3 && progression.Experience == 200 && !progression.IsSelectionOpen, "XP accumulates below the 210-point level-3 requirement");
        progression.RegisterEnemyKill(false);
        Check(progression.Level == 4 && progression.Experience == 90 && progression.ExperienceRequired == 273, "Overflow XP carries into the next bar using the 30-percent growth step");
        progression.CompleteSelectionAnimationForTests();
        progression.ChooseCard(FindOffer(progression, TankUpgradeType.Mobility));
        progression.CompleteSelectionAnimationForTests();
        Check(progression.MobilityTier == 1 && Mathf.Approximately(health.GetComponent<TankController>().BattleSpeedMultiplier, 1.1f), "First mobility upgrade improves normal tank speed by 10 percent");
        Check(Mathf.Approximately(health.GetComponent<TankNitro>().Capacity, baseNitroCapacity * 1.1f), "First mobility upgrade increases nitro capacity by 10 percent");
        Check(!progression.IsSelectionOpen && Time.timeScale > 0f && !PlayerHealthBar.GameplayInputBlocked, "Choosing an upgrade resumes the battle");
        UnityEngine.Object.Destroy(scalingEnemy);

        GameplayCrosshairDisplay crosshair = GameObject.Find("Gameplay Cursor")?.GetComponent<GameplayCrosshairDisplay>();
        Check(crosshair != null && crosshair.ReloadFill != null && crosshair.CenterDiamond != null, "Variant 20 crosshair is assembled in the gameplay HUD");
        Check(crosshair.ReloadFill.sprite.name.Contains("Variant 20") && crosshair.CenterDiamond.sprite.name.Contains("Variant 20"), "Crosshair uses the selected segmented ring and gold diamond design");
        Check(crosshair.ReloadFill.type == Image.Type.Filled && crosshair.ReloadFill.fillMethod == Image.FillMethod.Radial360, "Crosshair reload uses radial filling");
        TankShooter cursorShooter = health.GetComponent<TankShooter>();
        cursorShooter.NotifySpecialShotFired();
        Check(crosshair.ReloadFill.fillAmount < .05f, "Crosshair radial fill resets immediately after a shot");
        Check(crosshair.CurrentRecoilExpansion > 0f && crosshair.RecoilRing.rect.width > 64f, "Crosshair segments kick away from the center on a shot");
        FieldInfo recoilAge = typeof(GameplayCrosshairDisplay).GetField("recoilAge", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo updateCrosshair = typeof(GameplayCrosshairDisplay).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        recoilAge.SetValue(crosshair, .24f);
        updateCrosshair.Invoke(crosshair, null);
        Check(Mathf.Approximately(crosshair.CurrentRecoilExpansion, 0f) && Mathf.Approximately(crosshair.RecoilRing.rect.width, 64f), "Crosshair returns to its resting size after recoil");

        TankTurretAim turretAim = health.GetComponent<TankTurretAim>();
        MethodInfo updateTurret = typeof(TankTurretAim).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        GameplayPointer.BeginRecentering(new Vector2(Screen.width - 2f, Screen.height * .5f));
        Ray aimRay = Camera.main.ScreenPointToRay(GameplayPointer.Position);
        Plane aimPlane = new Plane(Vector3.up, new Vector3(0f, turretAim.Turret.position.y, 0f));
        Check(aimPlane.Raycast(aimRay, out float aimDistance), "Turret test cursor reaches the aiming plane");
        Vector3 direction = aimRay.GetPoint(aimDistance) - turretAim.Turret.position;
        direction.y = 0f;
        Quaternion desiredRotation = TankPlaneMath.RotationLookingAlong(direction, Vector3.forward);
        turretAim.Turret.rotation = desiredRotation * Quaternion.Euler(0f, 180f, 0f);
        Quaternion beforeRotation = turretAim.Turret.rotation;
        updateTurret.Invoke(turretAim, null);
        float rotatedDegrees = Quaternion.Angle(beforeRotation, turretAim.Turret.rotation);
        Check(turretAim.RotationSpeed > 0f && turretAim.RotationSpeed <= 100f, "Turret turn speed is limited to a readable arcade value");
        Check(rotatedDegrees > .001f && rotatedDegrees <= turretAim.RotationSpeed * Time.deltaTime + .1f, "Turret approaches its target without snapping");
        GameplayPointer.ClearOverride();

        TankNitro nitro = health.GetComponent<TankNitro>();
        FieldInfo boostingField = typeof(TankNitro).GetField("<IsBoosting>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo refreshNitro = typeof(NitroBarDisplay).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic);
        boostingField.SetValue(nitro, true);
        refreshNitro.Invoke(nitroDisplay, null);
        Check(nitroDisplay.GlowImage.gameObject.activeSelf && nitroDisplay.FillImage.color.b > .9f, "Active nitro turns bright cyan and enables its edge glow");
        boostingField.SetValue(nitro, false);
        refreshNitro.Invoke(nitroDisplay, null);

        health.TakeDamage(health.MaxHealth);
        stage = 2;
        due = EditorApplication.timeSinceStartup + .2;
    }

    private static void CheckDefeatState()
    {
        PlayerHealthBar healthBar = UnityEngine.Object.FindAnyObjectByType<PlayerHealthBar>();
        TankHealth health = FindPlayer();
        TopDownCameraFollow cameraFollow = Camera.main != null ? Camera.main.GetComponent<TopDownCameraFollow>() : null;
        SceneAudioController audio = UnityEngine.Object.FindAnyObjectByType<SceneAudioController>();
        GameObject panel = GameObject.Find("Game Over Panel");
        Check(healthBar != null && healthBar.IsGameOverShown && panel != null && panel.activeInHierarchy, "Defeat overlay appears immediately");
        Check(Time.timeScale > 0f, "World keeps running after defeat");
        Check(PlayerHealthBar.GameplayInputBlocked && Cursor.visible, "Gameplay cursor is blocked and the system cursor is available");
        Check(cameraFollow != null && cameraFollow.IsFrozen, "Camera is frozen on defeat");
        Check(health != null && !health.GetComponent<TankController>().enabled && !health.GetComponent<TankShooter>().enabled && !health.GetComponent<TankTurretAim>().enabled, "Player controls are disabled after defeat");
        Check(audio == null || !audio.IsMusicPlaying, "Music stops on defeat");
        Check(panel.transform.Find("Defeat Card/Restart Button") != null && panel.transform.Find("Defeat Card/Menu Button") != null, "Defeat card has restart and menu buttons");
        ScreenCapture.CaptureScreenshot(Folder + "defeat.png");
        stage = 3;
        due = EditorApplication.timeSinceStartup + .35;
    }

    private static void RestartFromDefeat()
    {
        GameObject panel = GameObject.Find("Game Over Panel");
        panel.transform.Find("Defeat Card/Restart Button").GetComponent<Button>().onClick.Invoke();
        stage = 4;
        due = EditorApplication.timeSinceStartup + 1.6;
    }

    private static void CheckDirectRestartAndDefeatAgain()
    {
        TankHealth health = FindPlayer();
        if (health == null || PlayerHealthBar.GameplayInputBlocked)
        {
            due = EditorApplication.timeSinceStartup + .1;
            return;
        }

        Check(health.IsAlive && GameObject.Find("Garage Panel") == null, "Restart starts the same battle directly");
        health.TakeDamage(health.MaxHealth);
        stage = 5;
        due = EditorApplication.timeSinceStartup + .2;
    }

    private static void ReturnToMenu()
    {
        GameObject panel = GameObject.Find("Game Over Panel");
        Check(panel != null && panel.activeInHierarchy, "Defeat overlay can be shown again after restart");
        panel.transform.Find("Defeat Card/Menu Button").GetComponent<Button>().onClick.Invoke();
        stage = 6;
        due = EditorApplication.timeSinceStartup + 1.5;
    }

    private static void CheckMenuReturn()
    {
        MainMenuController menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
        GarageMenuView view = UnityEngine.Object.FindAnyObjectByType<GarageMenuView>();
        if (menu == null || view == null || menu.IsBusy)
        {
            due = EditorApplication.timeSinceStartup + .1;
            return;
        }

        Check(view.gameObject.activeInHierarchy && PlayerHealthBar.GameplayInputBlocked && Time.timeScale == 0f, "Menu button returns to the garage");
        File.AppendAllText(Folder + "checks.txt", "ALL CHECKS PASSED\n");
        Debug.Log("Gameplay UI and defeat checks passed: " + Folder + "checks.txt");
        Finish();
    }

    private static TankHealth FindPlayer()
    {
        foreach (TankHealth candidate in UnityEngine.Object.FindObjectsByType<TankHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (candidate.Team == TankTeam.Player) return candidate;
        return null;
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        File.AppendAllText(Folder + "checks.txt", "PASS: " + label + "\n");
    }

    private static int FindOffer(TankBattleProgression progression, TankUpgradeType type)
    {
        for (int i = 0; i < 3; i++)
            if (progression.GetOffer(i) == type) return i;
        throw new InvalidOperationException("Upgrade offer not found: " + type);
    }

    private static void Finish()
    {
        running = false;
        SessionState.SetBool(RunningKey, false);
        EditorApplication.update -= Tick;
    }
}
