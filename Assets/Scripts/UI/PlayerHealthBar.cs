using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerHealthBar : MonoBehaviour
{
    public static bool GameplayInputBlocked { get; private set; }

    [SerializeField] private TankHealth target;
    [SerializeField] private Image fillImage;
    [SerializeField] private RectTransform fillRect;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private Image gameplayCursorImage;
    [SerializeField] private Text healthValue;

    private bool gameOverShown;
    private float fillMaxWidth;
    private float fillHeight;

    public bool IsGameOverShown => gameOverShown;

    public void Configure(TankHealth playerHealth, Image healthFill, Text valueLabel, GameObject gameOverRoot, Button restart, Button menu, Image cursorImage)
    {
        target = playerHealth;
        fillImage = healthFill;
        fillRect = healthFill != null ? healthFill.rectTransform : null;
        if (fillRect != null && fillRect.parent is RectTransform trackRect)
        {
            fillMaxWidth = Mathf.Max(0f, trackRect.rect.width - 8f);
            fillHeight = Mathf.Max(1f, trackRect.rect.height - 4f);
            fillRect.anchorMin = fillRect.anchorMax = new Vector2(0f, .5f);
            fillRect.pivot = new Vector2(0f, .5f);
            fillRect.anchoredPosition = new Vector2(4f, 0f);
        }
        healthValue = valueLabel;
        gameOverPanel = gameOverRoot;
        restartButton = restart;
        menuButton = menu;
        gameplayCursorImage = cursorImage;
        gameOverShown = false;
        GameplayModalState.Set(GameplayBlockReason.Defeat, false, false);
        SetPlayerControlEnabled(true);
        SetCameraFrozen(false);
        SetGameplayCursorActive(true);

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartScene);
            restartButton.onClick.AddListener(RestartScene);
        }

        if (menuButton != null)
        {
            menuButton.onClick.RemoveListener(ReturnToMenu);
            menuButton.onClick.AddListener(ReturnToMenu);
        }

        UpdateVisual();
    }

    private void Update()
    {
        UpdateVisual();
        UpdateGameplayCursor();
        TryHandleRestartClickFallback();
    }

    private void UpdateVisual()
    {
        if (target == null || fillImage == null || fillRect == null)
        {
            return;
        }

        fillImage.type = Image.Type.Simple;
        fillImage.fillAmount = target.Normalized;
        fillRect.sizeDelta = new Vector2(fillMaxWidth * target.Normalized, fillHeight);
        fillImage.enabled = target.Normalized > .001f;
        if (healthValue != null) healthValue.text = $"{target.CurrentHealth} / {target.MaxHealth}";

        bool isGameOver = !target.IsAlive;
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(isGameOver);
        }

        if (isGameOver && !gameOverShown)
        {
            ShowGameOver();
        }
    }

    private void ShowGameOver()
    {
        gameOverShown = true;
        GameplayModalState.Set(GameplayBlockReason.Defeat, true, false);
        TankiPlatformServices.SetBattleActive(false);
        SetPlayerControlEnabled(false);
        SetCameraFrozen(true);
        SetGameplayCursorActive(false);
        if (gameOverPanel != null) gameOverPanel.transform.SetAsLastSibling();
        FindFirstObjectByType<SceneAudioController>()?.StopMusicForDefeat();
    }

    private void SetGameplayCursorActive(bool isActive)
    {
        bool showGameplayCursor = isActive && !GameplayInputBlocked && Application.isPlaying;
        if (gameplayCursorImage != null)
        {
            gameplayCursorImage.gameObject.SetActive(showGameplayCursor);
            gameplayCursorImage.raycastTarget = false;
        }

        if (Application.isPlaying)
        {
            Cursor.visible = !showGameplayCursor;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    private void UpdateGameplayCursor()
    {
        if (!Application.isPlaying || gameplayCursorImage == null || Mouse.current == null)
        {
            return;
        }

        bool isActive = !gameOverShown && !GameplayInputBlocked && target != null && target.IsAlive;
        if (gameplayCursorImage.gameObject.activeSelf != isActive)
        {
            gameplayCursorImage.gameObject.SetActive(isActive);
        }

        if (!isActive)
        {
            return;
        }

        gameplayCursorImage.rectTransform.position = GameplayPointer.Position;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;
    }


    private void SetPlayerControlEnabled(bool isEnabled)
    {
        if (target == null)
        {
            return;
        }

        TankController controller = target.GetComponent<TankController>();
        if (controller != null)
        {
            controller.enabled = isEnabled;
        }

        TankShooter shooter = target.GetComponent<TankShooter>();
        if (shooter != null)
        {
            shooter.enabled = isEnabled;
        }

        TankTurretAim turretAim = target.GetComponent<TankTurretAim>();
        if (turretAim != null) turretAim.enabled = isEnabled;
        TankAimLaser aimLaser = target.GetComponent<TankAimLaser>();
        if (aimLaser != null) aimLaser.enabled = isEnabled;
        TankNitro nitro = target.GetComponent<TankNitro>();
        if (nitro != null) nitro.enabled = isEnabled;
        TankSpecialWeapon specialWeapon = target.GetComponent<TankSpecialWeapon>();
        if (specialWeapon != null) specialWeapon.enabled = isEnabled;
    }

    private static void SetCameraFrozen(bool isFrozen)
    {
        TopDownCameraFollow cameraFollow = Camera.main != null
            ? Camera.main.GetComponent<TopDownCameraFollow>()
            : FindFirstObjectByType<TopDownCameraFollow>();

        if (cameraFollow != null)
        {
            cameraFollow.SetFrozen(isFrozen);
        }
    }

    private void TryHandleRestartClickFallback()
    {
        if (!gameOverShown || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Vector2 position = Mouse.current.position.ReadValue();
        RectTransform restartRect = restartButton != null ? restartButton.transform as RectTransform : null;
        RectTransform menuRect = menuButton != null ? menuButton.transform as RectTransform : null;
        if (restartRect != null && RectTransformUtility.RectangleContainsScreenPoint(restartRect, position, null))
        {
            RestartScene();
        }
        else if (menuRect != null && RectTransformUtility.RectangleContainsScreenPoint(menuRect, position, null))
        {
            ReturnToMenu();
        }
    }

    private static void RestartScene()
    {
        TankiGameplayBootstrap.RestartCurrentBattle();
    }

    private static void ReturnToMenu() => TankiGameplayBootstrap.ReturnToMainMenu();

    internal static void SetGameplayInputBlocked(bool blocked)
    {
        GameplayInputBlocked = blocked;
    }
}
