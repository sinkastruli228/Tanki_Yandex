using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class GarageMenuChecks
{
    private const string Folder = ".utmp/garage/";
    private static int stage;
    private static double due;
    private static int startingCoins;
    private static bool running;
    static GarageMenuChecks()
    {
        if (SessionState.GetBool("GarageChecksRunning", false))
        { running = true; due = EditorApplication.timeSinceStartup + 3; EditorApplication.update += Tick; }
    }
    [MenuItem("Tools/Tanki/Run Garage Smoke Checks")]
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder + "checks.txt", "Garage smoke checks\n");
        try
        {
            Check(Resources.Load<GameObject>("Models/Tank/Tank") != null && Resources.Load<GameObject>("Models/Tank/Tank Desert") != null && Resources.Load<GameObject>("Models/Tank/Tank Snow") != null && Resources.Load<GameObject>("Models/Tank/Tank_Maus") != null, "All showcase prefabs are available in player builds");
            var save = new TankGarageSave();
            Check(save.Owns(0) && !save.Owns(1) && !save.Owns(2), "Green is the free default");
            save.coins = 999;
            Check(!save.TryBuy(1) && save.coins == 999, "Insufficient funds cannot buy");
            save.coins = 2000;
            Check(save.TryBuy(1) && save.coins == 1000 && save.selectedSkin == 1, "Purchase debits exactly 1000 and selects skin");
            Check(!save.TryBuy(1) && save.coins == 1000, "Duplicate purchase cannot debit twice");
            Check(save.TryBuy(2) && save.coins == 0, "Second skin purchases independently");
            var copy = JsonUtility.FromJson<TankGarageSave>(JsonUtility.ToJson(save));
            copy.Normalize();
            Check(copy.Owns(0) && copy.Owns(1) && copy.Owns(2) && copy.selectedSkin == 2 && copy.coins == 0, "Save round trip preserves wallet and skins");
            copy.coins = -100; copy.unlockedMask = 0; copy.selectedSkin = 9; copy.Normalize();
            Check(copy.coins == 0 && copy.Owns(0) && copy.selectedSkin == 0, "Malformed values normalize safely");
            SessionState.SetBool("GarageChecksRunning", true);
            running = true; stage = 0; due = EditorApplication.timeSinceStartup + 3;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Fail(e); }
    }
    private static void Tick()
    {
        if (!running || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < due) return;
        var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
        if (menu == null || menu.IsBusy) { due = EditorApplication.timeSinceStartup + .2; return; }
        var view = UnityEngine.Object.FindAnyObjectByType<GarageMenuView>();
        try
        {
            switch (stage++)
            {
                case 0:
                    startingCoins = TankGarageProgress.Coins;
                    Check(view != null && Time.timeScale == 0 && PlayerHealthBar.GameplayInputBlocked, "Garage freezes combat");
                    Check(UnityEngine.Object.FindObjectsByType<TankSelectionMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0, "Legacy selector removed");
                    Check(view.CoinsLabel.text == startingCoins.ToString("N0"), "Menu shows saved balance");
                    ScreenCapture.CaptureScreenshot(Folder + "01-garage.png");
                    view.NextButton.onClick.Invoke(); break;
                case 1:
                    Check(menu.PreviewSkin == 1 && view.PlayButton.interactable == TankGarageProgress.Owns(1), "Desert preview respects ownership");
                    ScreenCapture.CaptureScreenshot(Folder + "02-desert.png");
                    view.NextButton.onClick.Invoke(); break;
                case 2:
                    Check(menu.PreviewSkin == 2 && view.PlayButton.interactable == TankGarageProgress.Owns(2), "Snow preview respects ownership");
                    view.SecretButton.onClick.Invoke(); break;
                case 3:
                    Check(menu.PreviewSkin == 3 && view.PlayButton.interactable, "Secret Maus remains available");
                    Check(TankiGameplayBootstrap.FindGarageTurret(GameObject.Find("Garage Showcase Tank").transform) != null, "Maus turret found for animation");
                    ScreenCapture.CaptureScreenshot(Folder + "03-maus.png");
                    view.NextButton.onClick.Invoke(); break;
                case 4:
                    // From Maus the next regular entry is desert, then previous is green.
                    view.PreviousButton.onClick.Invoke(); break;
                case 5:
                    Check(menu.PreviewSkin == 0, "Carousel returns to green");
                    Check(TankGarageProgress.Coins == startingCoins, "Cosmetic shots award no coins");
                    view.PlayButton.onClick.Invoke(); break;
                case 6:
                    ValidateBattle(false);
                    ScreenCapture.CaptureScreenshot(Folder + "04-battle.png");
                    TankiGameplayBootstrap.ReturnToMainMenu(); break;
                case 7:
                    Check(view != null && TankGarageProgress.Coins == startingCoins, "Return rebuilds garage and keeps balance");
                    view.SecretButton.onClick.Invoke(); break;
                case 8:
                    view.InfiniteButton.onClick.Invoke(); break;
                case 9:
                    ValidateBattle(true);
                    TankiGameplayBootstrap.ReturnToMainMenu(); break;
                default:
                    Check(view != null && menu.PreviewSkin == TankGarageProgress.SelectedSkin, "Reload restores owned selection");
                    File.AppendAllText(Folder + "checks.txt", "ALL CHECKS PASSED\n");
                    running = false; SessionState.SetBool("GarageChecksRunning", false); EditorApplication.update -= Tick;
                    Debug.Log("Garage smoke checks passed: " + Folder + "checks.txt"); break;
            }
            due = EditorApplication.timeSinceStartup + 2;
        }
        catch (Exception e) { Fail(e); }
    }
    private static void ValidateBattle(bool maus)
    {
        var tank = GameObject.Find("Tank");
        Check(Time.timeScale == 1 && !PlayerHealthBar.GameplayInputBlocked && tank.GetComponent<TankController>().enabled, "Battle enables player controls");
        Check(Camera.main.GetComponent<TopDownCameraFollow>().enabled && Mathf.Abs(Camera.main.fieldOfView - 58) < .01f, "Camera reaches gameplay pose");
        Check(UnityEngine.Object.FindAnyObjectByType<EnemyWaveSpawner>() != null, "Waves begin after transition");
        var hud = GameObject.Find("Player Health UI");
        Check(hud != null && hud.transform.Find("Tank Selection Panel") == null, "Gameplay HUD replaces menu without old selector");
        Check(Mathf.Abs(((RectTransform)hud.transform.Find("Health Bar Background")).anchoredPosition.x - 28) < .01f, "HUD returns to exact resting position");
        if (maus) Check(tank.transform.Find("Runtime Tank Model") != null, "Maus is applied to gameplay tank");
    }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        File.AppendAllText(Folder + "checks.txt", "PASS: " + label + "\n");
    }
    private static void Fail(Exception error)
    {
        running = false; SessionState.SetBool("GarageChecksRunning", false); EditorApplication.update -= Tick;
        File.AppendAllText(Folder + "checks.txt", "FAIL: " + error + "\n"); Debug.LogException(error);
    }
}
