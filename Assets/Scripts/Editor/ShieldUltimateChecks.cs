using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ShieldUltimateChecks
{
    private const string Folder = ".utmp/shield-ultimate/";
    private const string RunningKey = "ShieldUltimateChecks.Running";
    private const string SavedUltimateKey = "ShieldUltimateChecks.SavedUltimate";

    private static bool running;
    private static int stage;
    private static double due;
    private static double deadline;
    private static TankShieldUltimate shield;
    private static TankHealth health;
    private static TankController controller;
    private static TankCombatRewards rewards;
    private static Vector3 lockedPosition;
    private static int protectedHealth;
    private static GameObject hostileProjectile;

    static ShieldUltimateChecks()
    {
        if (SessionState.GetBool(RunningKey, false)) Resume();
    }

    [MenuItem("Tools/Tanki/Check Shield Ultimate")]
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "checks.txt", "Shield ultimate checks\n");
        SessionState.SetInt(SavedUltimateKey, TankUltimateLoadout.Selected);
        TankUltimateLoadout.Select(TankUltimateLoadout.ShieldSlot);
        SessionState.SetBool(RunningKey, true);
        Resume();
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
    }

    private static void Resume()
    {
        running = true;
        stage = 0;
        due = EditorApplication.timeSinceStartup + 1.5;
        deadline = due + 35;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!running || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < due) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Shield ultimate check timed out");
            switch (stage)
            {
                case 0:
                    CheckMenuAndLaunch();
                    break;
                case 1:
                    StartShieldWhenBattleIsReady();
                    break;
                case 2:
                    CheckChargedShieldWaitsForInput();
                    break;
                case 3:
                    CheckActivationAndMovementLock();
                    break;
                case 4:
                    CheckFortressAndLaunchHostileShot();
                    break;
                case 5:
                    CheckHostileShotWasBlocked();
                    break;
                case 6:
                    CheckShieldExpired();
                    break;
            }
        }
        catch (Exception error)
        {
            Fail(error);
        }
    }

    private static void CheckMenuAndLaunch()
    {
        MainMenuController menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
        GarageMenuView view = UnityEngine.Object.FindAnyObjectByType<GarageMenuView>();
        if (menu == null || view == null || menu.IsBusy)
        {
            due = EditorApplication.timeSinceStartup + 0.1;
            return;
        }

        Transform choices = view.UltimatePicker.transform.Find("Ultimate Choices");
        Check(choices != null, "Ultimate choice row exists");
        Check(choices.Find("Ultimate 1/Name").GetComponent<Text>().text == GameLanguage.Text("РАКЕТА", "ROCKET"), "First cell is labelled Rocket");
        Check(choices.Find("Ultimate 2/Name").GetComponent<Text>().text == GameLanguage.Text("ЩИТ", "SHIELD"), "Second cell is labelled Shield");
        Check(choices.Find("Ultimate 1/Description").GetComponent<Text>().text.Length > 20, "Rocket card has a short description");
        Check(choices.Find("Ultimate 2/Description").GetComponent<Text>().text.Length > 20, "Shield card has a short description");
        RectTransform preview = choices.Find("Ultimate 2/Preview 4x3").GetComponent<RectTransform>();
        Check(Mathf.Abs(preview.rect.width / preview.rect.height - 4f / 3f) < .01f, "Ultimate cards reserve a 4:3 video preview");
        Check(choices.Find("Ultimate 2").GetComponent<RectTransform>().rect.height >= 220f, "Ultimate choice cards use the expanded layout");
        Check(choices.Find("Ultimate 3").GetComponent<Button>().interactable && !choices.Find("Ultimate 4").GetComponent<Button>().interactable, "Bombardment is available and the future placeholder is disabled");
        Check(TankUltimateLoadout.Selected == TankUltimateLoadout.ShieldSlot, "Shield loadout is selected");

        view.PlayButton.onClick.Invoke();
        stage = 1;
        due = 0;
    }

    private static void StartShieldWhenBattleIsReady()
    {
        GameObject tank = GameObject.Find("Tank");
        if (tank == null || PlayerHealthBar.GameplayInputBlocked || Time.timeScale <= 0f)
        {
            due = EditorApplication.timeSinceStartup + 0.05;
            return;
        }

        foreach (EnemyWaveSpawner spawner in UnityEngine.Object.FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None))
            UnityEngine.Object.Destroy(spawner.gameObject);
        foreach (TankHealth candidate in UnityEngine.Object.FindObjectsByType<TankHealth>(FindObjectsSortMode.None))
            if (candidate.Team == TankTeam.Enemy) UnityEngine.Object.Destroy(candidate.gameObject);

        shield = tank.GetComponent<TankShieldUltimate>();
        health = tank.GetComponent<TankHealth>();
        controller = tank.GetComponent<TankController>();
        rewards = tank.GetComponent<TankCombatRewards>();
        Check(shield != null && health != null && controller != null && rewards != null, "Player shield dependencies are configured");
        rewards.ForceChargeSpecial();
        stage = 2;
        due = EditorApplication.timeSinceStartup + 0.35;
    }

    private static void CheckChargedShieldWaitsForInput()
    {
        Check(rewards.IsFullyCharged && !rewards.IsSpecialArmed, "Shield can be fully charged without being armed");
        Check(!shield.IsActive, "A charged shield waits for the Q activation request");
        RectTransform combatPanel = GameObject.Find("Special Charge Background").GetComponent<RectTransform>();
        Check(combatPanel.rect.width >= 260f && combatPanel.Find("Ultimate Name") != null && combatPanel.Find("Charge Track") != null, "Combat ultimate HUD uses the garage card style");
        Check(rewards.RequestSpecialActivation(), "Q activation request is accepted when the shield is charged");
        stage = 3;
        due = EditorApplication.timeSinceStartup + 0.2;
    }

    private static void CheckActivationAndMovementLock()
    {
        Check(shield.IsActive, "Shield activates after the Q activation request");
        Check(controller.MovementLocked, "Shield locks chassis movement");
        Check(health.IsDamageBlocked, "Shield blocks player damage");
        Check(health.GetComponent<TankShooter>().enabled && health.GetComponent<TankTurretAim>().enabled, "Turret aiming and firing remain enabled");

        protectedHealth = health.CurrentHealth;
        health.TakeDamage(25);
        Check(health.CurrentHealth == protectedHealth, "Direct damage cannot pass through the active shield");

        bool shotFired = false;
        TankShooter shooter = health.GetComponent<TankShooter>();
        shooter.Shot += () => shotFired = true;
        shooter.Fire();
        Check(shotFired, "Player can fire while the shield is active");

        lockedPosition = health.transform.position;
        controller.SetExternalInput(1f, 1f);
        stage = 4;
        due = EditorApplication.timeSinceStartup + 2.8;
    }

    private static void CheckFortressAndLaunchHostileShot()
    {
        controller.ClearExternalInput();
        Check(Vector3.Distance(health.transform.position, lockedPosition) < 0.01f, "Movement input does not move the shielded tank");
        Check(shield.ActivePlateCount == 6, "All six plates land in sequence");
        Check(UnityEngine.Object.FindObjectsByType<TankShieldPlate>(FindObjectsSortMode.None).Length == 6, "Fortress has six projectile-blocking plates");
        CheckEnemyLineOfFireContinuesThroughShield();
        ScreenCapture.CaptureScreenshot(Folder + "shield-active.png");

        Vector3 direction = controller.ForwardOnPlane;
        hostileProjectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hostileProjectile.name = "Shield Check Hostile Projectile";
        hostileProjectile.transform.position = health.transform.position + direction * 10f + Vector3.up * 1.4f;
        ProjectileMovement projectile = hostileProjectile.AddComponent<ProjectileMovement>();
        projectile.ConfigureDamage(TankTeam.Enemy, 25, null);
        projectile.Launch(-direction, 140f, Vector3.forward);
        stage = 5;
        due = EditorApplication.timeSinceStartup + 0.25;
    }

    private static void CheckHostileShotWasBlocked()
    {
        Check(hostileProjectile == null, "Enemy projectile breaks on a shield plate");
        Check(health.CurrentHealth == protectedHealth, "Blocked enemy projectile deals no damage");
        stage = 6;
        due = EditorApplication.timeSinceStartup + 8.5;
    }

    private static void CheckEnemyLineOfFireContinuesThroughShield()
    {
        Vector3 direction = controller.ForwardOnPlane;
        GameObject probe = new GameObject("Shield Enemy Line Of Fire Probe");
        probe.transform.SetPositionAndRotation(
            health.transform.position + direction * 16f + Vector3.up * 1.1f,
            Quaternion.LookRotation(-direction, Vector3.up));
        StaticEnemyTank enemy = probe.AddComponent<StaticEnemyTank>();
        enemy.Configure(health, probe.transform, probe.transform, null, 100f, 25, 50f, 50f, 1f, Vector3.forward);
        MethodInfo method = typeof(StaticEnemyTank).GetMethod("HasClearLineOfFire", BindingFlags.Instance | BindingFlags.NonPublic);
        bool clear = method != null && (bool)method.Invoke(enemy, new object[] { probe.transform, -direction });
        Check(clear, "Enemy tanks keep the player as a valid firing target through shield plates");
        UnityEngine.Object.Destroy(probe);
    }

    private static void CheckShieldExpired()
    {
        Check(!shield.IsActive && !controller.MovementLocked && !health.IsDamageBlocked, "Shield expires and restores movement and damage handling");
        Check(UnityEngine.Object.FindObjectsByType<TankShieldPlate>(FindObjectsSortMode.None).Length == 0, "Shield plates clean up after the effect");
        File.AppendAllText(Folder + "checks.txt", "ALL CHECKS PASSED\n");
        Debug.Log("Shield ultimate checks passed: " + Folder + "checks.txt");
        Finish();
    }

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        File.AppendAllText(Folder + "checks.txt", "PASS: " + label + "\n");
    }

    private static void Fail(Exception error)
    {
        File.AppendAllText(Folder + "checks.txt", "FAIL: " + error + "\n");
        Debug.LogException(error);
        Finish();
    }

    private static void Finish()
    {
        running = false;
        SessionState.SetBool(RunningKey, false);
        EditorApplication.update -= Tick;
        TankUltimateLoadout.Select(SessionState.GetInt(SavedUltimateKey, TankUltimateLoadout.RocketSlot));
    }
}
