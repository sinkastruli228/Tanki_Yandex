using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class GarageRevisionChecks
{
    private const string Folder = ".utmp/garage-revision/";
    private static int stage, savedSkin, savedCoins;
    private static bool running, originalEnglish, watchWallet;
    private static double due, switchStarted, deadline;
    private static RectTransform wallet;
    private static Vector3 walletPosition;
    static GarageRevisionChecks()
    {
        if (SessionState.GetBool("GarageRevisionChecks", false)) Resume();
    }
    [MenuItem("Tools/Tanki/Check Language And Wallet")]
    public static void Run()
    {
        Directory.CreateDirectory(Folder); File.WriteAllText(Folder + "checks.txt", "Language, responsiveness and wallet checks\n");
        SessionState.SetBool("GarageRevisionChecks", true); Resume();
        if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
    }
    private static void Resume()
    {
        running = true; stage = 0; due = EditorApplication.timeSinceStartup + 2; deadline = due + 60;
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (!running || !EditorApplication.isPlaying) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Test timed out");
            if (watchWallet && (wallet == null || !wallet.gameObject.activeInHierarchy || Vector3.Distance(wallet.position, walletPosition) > .1f))
                throw new Exception("Wallet moved or disappeared during launch");
            if (EditorApplication.timeSinceStartup < due) return;
            var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            if (menu == null || menu.IsBusy) return;
            var view = UnityEngine.Object.FindAnyObjectByType<GarageMenuView>();
            switch (stage++)
            {
                case 0:
                    savedSkin = TankGarageProgress.SelectedSkin; savedCoins = TankGarageProgress.Coins; originalEnglish = GameLanguage.IsEnglish;
                    ClickLanguage(view, true);
                    Check(view.PlayButton.GetComponentInChildren<Text>().text == "PLAY", "English menu applied immediately");
                    Check(view.Wallet.Find("Currency").GetComponent<Text>().text == "COINS", "Wallet title translated");
                    Check(view.transform.Find("Garage Title/Title").GetComponent<Text>().text == "DESERT TANKS", "Game title stays unchanged");
                    Check(view.transform.Find("Garage Title/Eyebrow") == null && view.transform.Find("Garage Actions/Section") == null && view.transform.Find("Garage Hint") == null, "Marked captions removed");
                    Check(view.PreviousButton.transform.Find("Chevron").GetComponent<RectTransform>().anchoredPosition == Vector2.zero, "Chevron geometry is centered");
                    Check(PlayerPrefs.GetInt("Tanki.Language") == 1, "Language preference is saved");
                    ScreenCapture.CaptureScreenshot(Folder + "english-menu.png");
                    switchStarted = EditorApplication.timeSinceStartup; view.NextButton.onClick.Invoke(); due = 0; return;
                case 1:
                    double duration = EditorApplication.timeSinceStartup - switchStarted;
                    Check(duration < 1.0 && view.NextButton.interactable, "Carousel unlocks promptly: " + duration.ToString("F3") + " s");
                    view.SecretButton.onClick.Invoke(); due = 0; return;
                case 2:
                    wallet = view.Wallet; walletPosition = wallet.position; watchWallet = true;
                    // Start immediately after parking, while the cosmetic shot is still running.
                    view.PlayButton.onClick.Invoke(); due = 0; return;
                case 3:
                    Check(!PlayerHealthBar.GameplayInputBlocked, "Starting during cosmetic shot enters battle safely");
                    Check(wallet != null && wallet.gameObject.activeInHierarchy, "Same wallet remains visible in gameplay");
                    Check(GameObject.Find("Coin Counter") == null, "Old gameplay counter is absent");
                    Check(wallet.Find("Currency").GetComponent<Text>().text == "COINS", "Gameplay wallet uses selected language");
                    ScreenCapture.CaptureScreenshot(Folder + "english-gameplay.png");
                    watchWallet = false; TankiGameplayBootstrap.ReturnToMainMenu(); break;
                case 4:
                    Check(GameLanguage.IsEnglish && view.PlayButton.GetComponentInChildren<Text>().text == "PLAY", "English persists through scene reload");
                    ClickLanguage(view, false);
                    Check(view.PlayButton.GetComponentInChildren<Text>().text == "ИГРАТЬ" && view.Wallet.Find("Currency").GetComponent<Text>().text == "МОНЕТЫ", "Switching back restores Russian menu and wallet");
                    Check(savedCoins == TankGarageProgress.Coins, "Tests preserve the player's coins");
                    ScreenCapture.CaptureScreenshot(Folder + "russian-menu.png");
                    TankGarageProgress.Select(savedSkin); GameLanguage.SetEnglish(originalEnglish);
                    File.AppendAllText(Folder + "checks.txt", "ALL CHECKS PASSED\n");
                    Finish(); break;
            }
            due = EditorApplication.timeSinceStartup + 1;
        }
        catch (Exception error)
        {
            File.AppendAllText(Folder + "checks.txt", "FAIL: " + error + "\n"); Finish(); Debug.LogException(error);
        }
    }
    private static void ClickLanguage(GarageMenuView view, bool english)
    {
        var control = view.GetComponentInChildren<GarageLanguageSwitch>();
        var cell = control.Thumb.parent.Find(english ? "EN" : "RU");
        var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
        { position = RectTransformUtility.WorldToScreenPoint(null, cell.position) };
        var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0, "Language control receives pointer raycasts");
        UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        Check(GameLanguage.IsEnglish == english, "Language cell responds to pointer input");
    }
    private static void Finish() { running = false; watchWallet = false; SessionState.SetBool("GarageRevisionChecks", false); EditorApplication.update -= Tick; }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        File.AppendAllText(Folder + "checks.txt", "PASS: " + label + "\n");
    }
}
