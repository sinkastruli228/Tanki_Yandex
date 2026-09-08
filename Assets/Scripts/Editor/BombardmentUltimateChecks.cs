using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[InitializeOnLoad]
public static class BombardmentUltimateChecks
{
    private const string Folder = ".utmp/bombardment-ultimate/";
    private const string RunningKey = "BombardmentUltimateChecks.Running";
    private const string SavedUltimateKey = "BombardmentUltimateChecks.SavedUltimate";

    private static bool running;
    private static int stage;
    private static double due;
    private static double deadline;
    private static TankBombardmentUltimate bombardment;
    private static TankCombatRewards rewards;
    private static TankController controller;
    private static TankShooter shooter;
    private static TankTurretAim turretAim;
    private static TankHealth dummyTarget;
    private static bool impactCaptured;

    static BombardmentUltimateChecks()
    {
        if (SessionState.GetBool(RunningKey, false)) Resume();
    }

    [MenuItem("Tools/Tanki/Check Bombardment Ultimate")]
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "checks.txt", "Bombardment ultimate checks\n");
        SessionState.SetInt(SavedUltimateKey, TankUltimateLoadout.Selected);
        TankUltimateLoadout.Select(TankUltimateLoadout.BombardmentSlot);
        SessionState.SetBool(RunningKey, true);
        Resume();
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
    }

    private static void Resume()
    {
        running = true;
        stage = 0;
        impactCaptured = false;
        due = EditorApplication.timeSinceStartup + 1.5;
        deadline = due + 50;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!running || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < due) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Bombardment ultimate check timed out");
            switch (stage)
            {
                case 0: CheckMenuAndLaunch(); break;
                case 1: PrepareBattle(); break;
                case 2: OpenPlanner(); break;
                case 3: CheckPlannerAndDraw(); break;
                case 4: SubmitPaintedStrike(); break;
                case 5: CheckCursorRecentering(); break;
                case 6: CheckShellsInFlight(); break;
                case 7: CheckImpacts(); break;
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
            due = EditorApplication.timeSinceStartup + .1;
            return;
        }

        Transform choices = view.UltimatePicker.transform.Find("Ultimate Choices");
        Check(choices != null, "Ultimate choice row exists");
        Check(choices.Find("Ultimate 3/Name").GetComponent<Text>().text == GameLanguage.Text("БОМБАРДИРОВКА", "BOMBARDMENT"), "Third cell is labelled Bombardment");
        Check(choices.Find("Ultimate 3/Description").GetComponent<Text>().text.Length > 20, "Bombardment card has a short description");
        Check(choices.Find("Ultimate 3/Preview 4x3") != null, "Bombardment card reserves the video preview");
        Check(choices.Find("Ultimate 3").GetComponent<Button>().interactable, "Bombardment can be selected");
        Check(TankUltimateLoadout.Selected == TankUltimateLoadout.BombardmentSlot, "Bombardment loadout is selected");
        view.PlayButton.onClick.Invoke();
        stage = 1;
        due = 0;
    }

    private static void PrepareBattle()
    {
        GameObject tank = GameObject.Find("Tank");
        if (tank == null || PlayerHealthBar.GameplayInputBlocked || Time.timeScale <= 0f)
        {
            due = EditorApplication.timeSinceStartup + .05;
            return;
        }

        foreach (EnemyWaveSpawner spawner in UnityEngine.Object.FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None))
            UnityEngine.Object.Destroy(spawner.gameObject);
        foreach (TankHealth candidate in UnityEngine.Object.FindObjectsByType<TankHealth>(FindObjectsSortMode.None))
            if (candidate.Team == TankTeam.Enemy) UnityEngine.Object.Destroy(candidate.gameObject);

        bombardment = tank.GetComponent<TankBombardmentUltimate>();
        rewards = tank.GetComponent<TankCombatRewards>();
        controller = tank.GetComponent<TankController>();
        shooter = tank.GetComponent<TankShooter>();
        turretAim = tank.GetComponent<TankTurretAim>();
        Check(bombardment != null && rewards != null && controller != null && shooter != null && turretAim != null, "Player bombardment dependencies are configured");
        rewards.ForceChargeSpecial();
        stage = 2;
        due = EditorApplication.timeSinceStartup + .3;
    }

    private static void OpenPlanner()
    {
        Check(rewards.IsFullyCharged && !rewards.IsSpecialArmed, "Bombardment waits at full charge for Q");
        Check(!bombardment.IsPlanning && !bombardment.IsActive, "Bombardment does not start automatically");
        Check(rewards.RequestSpecialActivation(), "Q activation request opens bombardment planning");
        stage = 3;
        due = EditorApplication.timeSinceStartup + .35;
    }

    private static void CheckPlannerAndDraw()
    {
        Check(bombardment.IsPlanning, "Orthographic planning mode is open");
        Check(bombardment.PlannerRoot != null && bombardment.PlannerRoot.activeSelf, "Bombardment planner UI is visible");
        Check(bombardment.MapCamera != null && bombardment.MapCamera.orthographic, "Planner uses an orthographic map camera");
        Check(bombardment.MapTexture != null && bombardment.MapTexture.width * 3 == bombardment.MapTexture.height * 4, "Live terrain map uses a 4:3 render target");
        Check(controller.MovementLocked && !shooter.enabled && !turretAim.enabled, "Tank controls are held while drawing on the map");
        Check(Time.timeScale == 0f, "Battle pauses while the strike zone is painted");
        Check(bombardment.StrokeCapacity == 41, "Available painted route is halved again");
        Check(bombardment.DamagePerImpact == 100, "Bombardment impact damage is 100");
        Check(Mathf.Approximately(bombardment.DamageRadius, 10.5f), "Bombardment damage radius is reduced by 30 percent");
        Check(bombardment.PlannerRoot.transform.Find("Bombardment Console/Hint") == null, "Planner instruction caption is removed");
        Check(bombardment.PlannerRoot.transform.Find("Bombardment Console/Route Status/Ink Hint") == null, "Route helper caption is removed");
        Check(bombardment.PlannerRoot.transform.Find("Bombardment Console/Live Dot") != null, "LIVE badge uses a blinking red dot");
        Image inkFill = bombardment.PlannerRoot.transform.Find("Bombardment Console/Route Status/Ink Track/Ink Fill").GetComponent<Image>();
        Check(inkFill.type == Image.Type.Simple && inkFill.sprite.name.Contains("Brush"), "Route limit uses an unclipped vertical brush stroke");
        Check(inkFill.rectTransform.rect.height < inkFill.rectTransform.parent.GetComponent<RectTransform>().rect.height - 12f, "Route fill keeps safe space at both track edges");

        Vector3 desiredTarget = controller.transform.position + controller.ForwardOnPlane * 24f;
        Vector2 normalized = bombardment.WorldToMapNormalized(desiredTarget);
        Vector3 targetPoint = bombardment.MapNormalizedToWorld(normalized);
        GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dummy.name = "Bombardment Check Target";
        dummy.transform.position = targetPoint + Vector3.up * .8f;
        dummy.transform.localScale = new Vector3(3f, 1.6f, 3f);
        dummyTarget = dummy.AddComponent<TankHealth>();
        dummyTarget.Configure(TankTeam.Enemy, 200, false);
        bombardment.RefreshMarkersForTests();
        Check(bombardment.EnemyMarkerCount >= 1, "Enemies are shown as map markers");

        bombardment.DrawTestStroke(normalized - new Vector2(.035f, 0f), normalized + new Vector2(.035f, 0f), 14);
        Check(bombardment.StrokePointCount >= 10, "A thick strike route can be painted on the map");
        ScreenCapture.CaptureScreenshot(Folder + "planner.png");
        stage = 4;
        due = EditorApplication.timeSinceStartup + .4;
    }

    private static void SubmitPaintedStrike()
    {
        Check(bombardment.SubmitStrike(), "Painted zone can be submitted for bombardment");
        Check(!bombardment.IsPlanning && bombardment.IsActive, "Planner closes when the strike begins");
        Check(controller.MovementLocked && !shooter.enabled && !turretAim.enabled && Time.timeScale == 0f, "Controls stay held while the cursor recenters");
        Check(!rewards.IsSpecialArmed && rewards.ChargeNormalized < .01f, "Ultimate charge is consumed only on submission");
        Check(bombardment.LastStrikeImpactCount >= 5, "Strike density covers even a short painted zone");
        stage = 5;
        due = EditorApplication.timeSinceStartup + .08;
    }

    private static void CheckCursorRecentering()
    {
        Check(bombardment.IsRecenteringCursor, "Cursor smoothly recenters before gameplay resumes");
        Check(Time.timeScale == 0f && PlayerHealthBar.GameplayInputBlocked, "Battle remains paused during cursor recentering");
        stage = 6;
        due = EditorApplication.timeSinceStartup + .38;
    }

    private static void CheckShellsInFlight()
    {
        if (bombardment.IsRecenteringCursor || BombardmentShell.ActiveCount == 0)
        {
            due = EditorApplication.timeSinceStartup + .05;
            return;
        }
        Check(Time.timeScale > 0f && !controller.MovementLocked && shooter.enabled && turretAim.enabled, "Gameplay resumes after the cursor reaches screen center");
        if (Mouse.current != null)
        {
            Vector2 center = new Vector2(Screen.width * .5f, Screen.height * .5f);
            Vector2 cursor = GameplayPointer.Position;
            File.AppendAllText(Folder + "checks.txt", $"INFO: Gameplay cursor {cursor}, center {center}, hardware {Mouse.current.position.ReadValue()}\n");
            Check(GameplayPointer.OverrideActive, "Gameplay uses the recentered virtual cursor");
            Check(Vector2.Distance(cursor, center) < 24f, "Cursor finishes at the screen center");
        }
        Check(BombardmentShell.ActiveCount > 0, "Heavy bombardment shells fly in from above");
        Check(BombardmentShell.LastLaunchedVisualScale > 1f, "Bombardment shell visual is enlarged");
        Check(!Cursor.visible, "Only the in-game crosshair is visible during battle");
        ScreenCapture.CaptureScreenshot(Folder + "strike.png");
        stage = 7;
        due = EditorApplication.timeSinceStartup + .45;
    }

    private static void CheckImpacts()
    {
        if (!impactCaptured && bombardment.ResolvedImpactCount > 0)
        {
            Check(GameObject.Find("Bombardment Distortion Shockwave") != null, "Bombardment creates an optimized spherical shockwave");
            AudioSource bombardmentAudio = GameObject.Find("Bombardment Explosion Audio")?.GetComponent<AudioSource>();
            Check(bombardmentAudio != null && bombardmentAudio.spatialBlend >= .99f, "Bombardment audio remains fully spatial");
            Check(
                bombardmentAudio.rolloffMode == AudioRolloffMode.Custom
                && bombardmentAudio.GetCustomCurve(AudioSourceCurveType.CustomRolloff).Evaluate(1f) >= .99f,
                "Bombardment audio has no distance volume limit");
            ScreenCapture.CaptureScreenshot(Folder + "impact.png");
            impactCaptured = true;
            due = EditorApplication.timeSinceStartup + .2;
            return;
        }
        if (bombardment.IsActive || BombardmentShell.ActiveCount > 0)
        {
            due = EditorApplication.timeSinceStartup + .2;
            return;
        }
        File.AppendAllText(Folder + "checks.txt", $"INFO: {bombardment.ResolvedImpactCount} of {bombardment.LastStrikeImpactCount} impacts resolved\n");
        Check(bombardment.ResolvedImpactCount >= 5, "Large explosions resolve across the painted zone");
        if (dummyTarget != null)
            File.AppendAllText(Folder + "checks.txt", $"INFO: Area target health {dummyTarget.CurrentHealth}/{dummyTarget.MaxHealth}\n");
        Check(dummyTarget != null && dummyTarget.CurrentHealth < dummyTarget.MaxHealth, "Bombardment deals area damage to enemies");
        Color popupColor = EnemyDamageNumberDisplay.LastShownColor;
        Check(
            !EnemyDamageNumberDisplay.LastShownCritical
            && popupColor.r >= .9f
            && popupColor.g >= .9f
            && popupColor.b >= .75f,
            "Bombardment damage popup uses the normal white color");
        Check(!bombardment.IsActive, "Bombardment finishes and returns to normal charging");
        File.AppendAllText(Folder + "checks.txt", "ALL CHECKS PASSED\n");
        Debug.Log("Bombardment ultimate checks passed: " + Folder + "checks.txt");
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
