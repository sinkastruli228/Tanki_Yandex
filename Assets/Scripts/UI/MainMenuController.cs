using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections.Generic;

[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button toBattleButton;
    [SerializeField] private Button infiniteButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Text settingsStubText;
    [SerializeField] private Camera sceneCamera;
    [SerializeField] private Transform previewTarget;
    [SerializeField] private GameObject enemyPreviewPrefab;

    private Quaternion gameplayTargetRotation;
    private Transform previewTurret;
    private Quaternion gameplayTurretLocalRotation;
    private readonly List<GameObject> previewEnemies = new List<GameObject>();

    private bool isMenuOpen;

    public void Configure(
        GameObject panel,
        Button battle,
        Button infinite,
        Button settings,
        Button exit,
        Text settingsText,
        Camera camera,
        Transform target,
        GameObject enemyPrefab)
    {
        panelRoot = panel;
        toBattleButton = battle;
        infiniteButton = infinite;
        settingsButton = settings;
        exitButton = exit;
        settingsStubText = settingsText;
        sceneCamera = camera;
        previewTarget = target;
        enemyPreviewPrefab = enemyPrefab;
        if (previewTarget != null)
        {
            gameplayTargetRotation = previewTarget.rotation;
            previewTurret = TankiGameplayBootstrap.FindChildRecursive(previewTarget, "Cylinder.002")
                ?? TankiGameplayBootstrap.FindChildRecursive(previewTarget, "cylinder.002");
            if (previewTurret != null)
            {
                gameplayTurretLocalRotation = previewTurret.localRotation;
            }
        }

        if (toBattleButton != null)
        {
            toBattleButton.onClick.RemoveListener(HandleToBattle);
            toBattleButton.onClick.AddListener(HandleToBattle);
        }

        if (infiniteButton != null)
        {
            infiniteButton.onClick.RemoveListener(HandleInfinite);
            infiniteButton.onClick.AddListener(HandleInfinite);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(HandleSettings);
            settingsButton.onClick.AddListener(HandleSettings);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(HandleExit);
            exitButton.onClick.AddListener(HandleExit);
        }

        ShowMenu();
    }

    private void Update()
    {
        if (isMenuOpen || Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        TankiGameplayBootstrap.ReturnToMainMenu();
    }

    public void ShowMenu()
    {
        isMenuOpen = true;
        DisableMenuGameplayInput();
        ApplyMenuTankPose();
        EnsurePreviewEnemies();
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        if (settingsStubText != null)
        {
            settingsStubText.gameObject.SetActive(false);
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 0f;
        UpdatePreviewCamera();
    }

    public void HideMenu()
    {
        isMenuOpen = false;
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        if (settingsStubText != null)
        {
            settingsStubText.gameObject.SetActive(false);
        }

        if (previewTarget != null)
        {
            previewTarget.rotation = gameplayTargetRotation;
            Rigidbody body = previewTarget.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.rotation = gameplayTargetRotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        if (previewTurret != null)
        {
            previewTurret.localRotation = gameplayTurretLocalRotation;
        }

        ClearPreviewEnemies();
    }

    private void HandleToBattle()
    {
        TankiGameplayBootstrap.StartBattle();
    }

    private void HandleInfinite()
    {
        TankiGameplayBootstrap.StartInfiniteBattle();
    }

    private void HandleSettings()
    {
        if (settingsStubText != null)
        {
            settingsStubText.gameObject.SetActive(true);
        }
    }

    private void HandleExit()
    {
        TankiGameplayBootstrap.QuitGame();
    }

    private void UpdatePreviewCamera()
    {
        if (sceneCamera == null || previewTarget == null)
        {
            return;
        }

        TopDownCameraFollow follow = sceneCamera.GetComponent<TopDownCameraFollow>();
        if (follow != null)
        {
            follow.enabled = false;
        }

        ApplyMenuTankPose();
        Vector3 targetPosition = previewTarget.position + new Vector3(-3.2f, 0.55f, 0f);
        sceneCamera.transform.position = previewTarget.position + new Vector3(-19f, 13.2f, -23f);
        sceneCamera.transform.rotation = Quaternion.LookRotation(targetPosition - sceneCamera.transform.position, Vector3.up);
        sceneCamera.fieldOfView = 40f;
    }

    private void ApplyMenuTankPose()
    {
        if (previewTarget == null)
        {
            return;
        }

        TankiGameplayBootstrap.ApplyDesertTankSkin(previewTarget.gameObject);
        Quaternion menuRotation = Quaternion.Euler(0f, gameplayTargetRotation.eulerAngles.y + 180f, 0f);
        previewTarget.rotation = menuRotation;
        if (previewTurret != null)
        {
            previewTurret.localRotation = gameplayTurretLocalRotation * Quaternion.Euler(0f, 30f, 0f);
        }

        Rigidbody body = previewTarget.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.rotation = menuRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    private void EnsurePreviewEnemies()
    {
        if (previewTarget == null || enemyPreviewPrefab == null || previewEnemies.Count > 0)
        {
            return;
        }

        Vector3[] offsets =
        {
            new Vector3(-13f, 0f, 16f),
            new Vector3(27f, 0f, 42f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 position = previewTarget.position + offsets[i];
            position.y = TankiGameplayBootstrap.GetGroundY(position);
            Vector3 toPlayer = TankPlaneMath.Flatten(previewTarget.position - position);
            Quaternion rotation = toPlayer.sqrMagnitude > 0.001f
                ? TankPlaneMath.RotationLookingAlong(toPlayer, Vector3.forward)
                : Quaternion.identity;

            GameObject enemy = Instantiate(enemyPreviewPrefab, position, rotation);
            enemy.name = $"Menu Preview Enemy {i + 1}";
            AlignPreviewEnemyToGround(enemy);
            DisablePreviewEnemyGameplay(enemy);
            previewEnemies.Add(enemy);
        }
    }

    private static void AlignPreviewEnemyToGround(GameObject enemy)
    {
        if (enemy == null)
        {
            return;
        }

        Renderer[] renderers = enemy.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float groundY = TankiGameplayBootstrap.GetGroundY(enemy.transform.position);
        Vector3 enemyPosition = enemy.transform.position;
        enemyPosition.y += groundY - bounds.min.y;
        enemy.transform.position = enemyPosition;
    }

    private static void DisablePreviewEnemyGameplay(GameObject enemy)
    {
        if (enemy == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = enemy.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = false;
            }
        }

        Rigidbody[] bodies = enemy.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody body in bodies)
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    private void ClearPreviewEnemies()
    {
        foreach (GameObject enemy in previewEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy);
            }
        }

        previewEnemies.Clear();
    }

    private void DisableMenuGameplayInput()
    {
        if (previewTarget == null)
        {
            return;
        }

        TankController controller = previewTarget.GetComponent<TankController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        TankShooter shooter = previewTarget.GetComponent<TankShooter>();
        if (shooter != null)
        {
            shooter.enabled = false;
        }

        TankTurretAim turretAim = previewTarget.GetComponent<TankTurretAim>();
        if (turretAim != null)
        {
            turretAim.enabled = false;
        }
    }
}
