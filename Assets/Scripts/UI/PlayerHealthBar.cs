using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerHealthBar : MonoBehaviour
{
    [SerializeField] private TankHealth target;
    [SerializeField] private Image fillImage;
    [SerializeField] private RectTransform fillRect;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Image gameplayCursorImage;
    [SerializeField] private RectTransform gameplayCursorRect;

    private bool gameOverShown;

    public void Configure(TankHealth playerHealth, Image healthFill, GameObject gameOverRoot, Button restart, Image cursorImage)
    {
        target = playerHealth;
        fillImage = healthFill;
        fillRect = healthFill != null ? healthFill.rectTransform : null;
        gameOverPanel = gameOverRoot;
        restartButton = restart;
        gameplayCursorImage = cursorImage;
        gameplayCursorRect = cursorImage != null ? cursorImage.rectTransform : null;
        gameOverShown = false;

        Time.timeScale = 1f;
        SetPlayerControlEnabled(true);
        SetCameraFrozen(false);
        SetGameplayCursorActive(true);

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartScene);
            restartButton.onClick.AddListener(RestartScene);
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

        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(target.Normalized, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillImage.color = new Color(0.15f, 0.85f, 0.15f, 1f);

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
        SetPlayerControlEnabled(false);
        SetCameraFrozen(true);
        Time.timeScale = 0f;
        SetGameplayCursorActive(false);
    }

    private void SetGameplayCursorActive(bool isActive)
    {
        bool showGameplayCursor = isActive && Application.isPlaying;
        if (gameplayCursorImage != null)
        {
            gameplayCursorImage.gameObject.SetActive(showGameplayCursor);
            gameplayCursorImage.raycastTarget = false;
        }

        if (Application.isPlaying)
        {
            Cursor.visible = !isActive;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    private void UpdateGameplayCursor()
    {
        if (!Application.isPlaying || gameplayCursorRect == null || Mouse.current == null)
        {
            return;
        }

        bool isActive = !gameOverShown && target != null && target.IsAlive;
        if (gameplayCursorImage != null && gameplayCursorImage.gameObject.activeSelf != isActive)
        {
            gameplayCursorImage.gameObject.SetActive(isActive);
        }

        if (!isActive)
        {
            return;
        }

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.None;
        gameplayCursorRect.position = Mouse.current.position.ReadValue();
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
        if (!gameOverShown || restartButton == null || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        RectTransform restartRect = restartButton.transform as RectTransform;
        if (restartRect == null)
        {
            return;
        }

        if (RectTransformUtility.RectangleContainsScreenPoint(restartRect, Mouse.current.position.ReadValue(), null))
        {
            RestartScene();
        }
    }

    private static void RestartScene()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.name);
    }
}
