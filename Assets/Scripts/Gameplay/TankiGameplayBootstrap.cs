using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class TankiGameplayBootstrap
{
    private const string TankPrefabPath = "Assets/Models/Tank/Tank.prefab";
    private const string TankDesertPrefabPath = "Assets/Models/Tank/Tank Desert.prefab";
    private const string TankSnowPrefabPath = "Assets/Models/Tank/Tank Snow.prefab";
    private const string TankEnemyPrefabPath = "Assets/Models/Tank/Tank Enemy.prefab";
    private const string TankMausPrefabPath = "Assets/Models/Tank/Tank_Maus.prefab";
    private const string MissilePrefabPath = "Assets/Models/Missle/Missile.prefab";
    private const string BoxPrefabPath = "Assets/Models/Box/Box.prefab";
    private const string ScopeSpritePath = "Assets/UI/Scope.png";
    private const string HealthBarSpritePath = "Assets/UI/Health_Bar.png";
    private const string HealthBarGreenSpritePath = "Assets/UI/Health_Bar_Green.png";
    private const string HealthBarBoltSpritePath = "Assets/UI/Health_Bar_Bolt.png";
    private const string NitroOpacitySpritePath = "Assets/UI/Nitro_Opacity.png";
    private const string NitroSpritePath = "Assets/UI/Nitro.png";
    private const string HitMarkerSpritePath = "Assets/UI/Hit_Marker.png";
    private const string EnemyMarkerSpritePath = "Assets/UI/Enemy Marker.png";
    private const string RuntimeTankModelRootName = "Runtime Tank Model";
    private const string AmbientClipPath = "Assets/Sounds/Ambient.mp3";
    private const string MovementClipPath = "Assets/Sounds/Movement.mp3";
    private const string MusicAmbientClipPath = "Assets/Sounds/Music_Ambient.mp3";
    private const string ShotClipPath = "Assets/Sounds/Shot.mp3";
    private const string RicochetClipPath = "Assets/Sounds/Richoshet.mp3";
    private const string ExplosionClipPath = "Assets/Sounds/Explosion.mp3";
    private const float GroundY = 0f;
    private const float FloorSize = 960f;
    private const float FloorTileSize = 8f;
    private const float TankForwardSpeed = 28.8f;
    private const float TankReverseSpeed = 16.8f;
    private const float TankAcceleration = 86.4f;
    private const float ProjectileSpeed = 140.4f;
    private const float PlayerShotCooldown = 1f;
    private const float PlayerMouseYawSensitivity = 0.18f;
    private const float PlayerTurretRotationSpeed = 100f;
    private const float MuzzleHeightOffset = 1.425f;
    private const float EnemyAttackRange = 170f;
    private const float EnemyDetectionRange = 202.5f;
    private const float EnemyShotCooldown = 2.5f;
    private const float PhysicsBoxGroundClearance = 0.3f;
    private const int TankMaxHealth = 100;
    private const int MausTankMaxHealth = 250;
    private const int ProjectileDamage = 25;
    private const int MausProjectileDamage = 50;
    private const float MausVisualScale = 1.5f;
    private static readonly Vector3 DefaultForwardAxis = Vector3.forward;
    private static readonly Vector3[] DefaultEnemyPositions =
    {
        new Vector3(110f, 0f, 120f),
        new Vector3(-120f, 0f, 145f),
        new Vector3(10f, 0f, 170f)
    };
    private static readonly Vector3[] DefaultBoxPositions =
    {
        new Vector3(18f, 0f, 36f),
        new Vector3(28f, 0f, 52f),
        new Vector3(-22f, 0f, 44f),
        new Vector3(-34f, 0f, 68f),
        new Vector3(8f, 0f, 96f),
        new Vector3(42f, 0f, 92f)
    };
    private static readonly Vector3[] DefaultBoxEulerAngles =
    {
        Vector3.zero,
        new Vector3(0f, 17f, 0f),
        new Vector3(0f, 34f, 0f),
        new Vector3(0f, 51f, 0f),
        new Vector3(0f, 68f, 0f),
        new Vector3(0f, 85f, 0f)
    };
    private static int lastSetupFrame = -1;
    private static GameObject currentTank;
    private static GameObject currentMissilePrefab;
    private static Camera currentCamera;
    private static TankHealth currentPlayerHealth;
    private static GameObject currentPlayerUi;

    private static EnemyWaveAnnouncement currentWaveAnnouncement;
    private static MainMenuController currentMainMenu;
    private static bool battleStarted;
    private static bool infiniteMode;
    private static bool hasInitialTankPose;
    private static Vector3 initialTankPosition;
    private static Quaternion initialTankRotation;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneReloadSetup()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void SetupSceneOnPlay()
    {
        if (lastSetupFrame == Time.frameCount)
        {
            return;
        }

        lastSetupFrame = Time.frameCount;
        Time.timeScale = 1f;
        PlayerHealthBar.GameplayInputBlocked = false;
        battleStarted = false;
        infiniteMode = false;

        GameObject tank = FindTankInScene();
        if (tank == null)
        {
            return;
        }

        ConfigureTank(tank, LoadMissilePrefab(), Camera.main, false);
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetupSceneOnPlay();
    }

    public static void ConfigureTank(GameObject tank, GameObject missilePrefab, Camera camera, bool persistent)
    {
        if (tank == null)
        {
            return;
        }

        currentTank = tank;
        currentMissilePrefab = missilePrefab;
        currentCamera = camera;
        if (!hasInitialTankPose)
        {
            initialTankPosition = tank.transform.position;
            initialTankRotation = tank.transform.rotation;
            hasInitialTankPose = true;
        }

        Rigidbody body = EnsureComponent<Rigidbody>(tank);
        body.useGravity = false;
        body.isKinematic = true;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        TankController controller = EnsureComponent<TankController>(tank);
        controller.enabled = true;
        controller.ConfigureModelAxis(DefaultForwardAxis);
        controller.ConfigureMovement(TankForwardSpeed, TankReverseSpeed, TankAcceleration);
        AlignTankBottomToGround(tank, body, GetGroundY(tank.transform.position));
        controller.RefreshMovementPlane();
        TankNitro playerNitro = EnsureComponent<TankNitro>(tank);
        playerNitro.Configure(controller);
        EnsureSingleBodyMeshCollider(tank);
        ConfigureShadowCasters(tank);

        TankHealth playerHealth = EnsureComponent<TankHealth>(tank);
        playerHealth.Configure(TankTeam.Player, TankMaxHealth, false);
        currentPlayerHealth = playerHealth;

        Transform turret = FindTankTurret(tank.transform);
        turret = turret != null ? turret : tank.transform;

        TankDeathEffect playerDeathEffect = EnsureComponent<TankDeathEffect>(tank);
        playerDeathEffect.Configure(playerHealth, turret, false);

        TankTurretAim turretAim = EnsureComponent<TankTurretAim>(tank);
        turretAim.enabled = true;
        turretAim.Configure(turret, camera);
        turretAim.ConfigureAimSettings(PlayerMouseYawSensitivity, PlayerTurretRotationSpeed);

        Transform muzzlePoint = FindChildRecursive(turret, "MuzzlePoint");
        if (muzzlePoint == null)
        {
            muzzlePoint = CreateMuzzlePoint(turret);
        }
        else
        {
            PositionMuzzlePoint(turret, muzzlePoint);
        }

        TankShooter shooter = EnsureComponent<TankShooter>(tank);
        shooter.enabled = true;
        shooter.Configure(turret, missilePrefab, muzzlePoint);
        shooter.ConfigureProjectileSpeed(ProjectileSpeed);
        shooter.ConfigureShotCooldown(PlayerShotCooldown);
        bool isMaus = IsMausTank(tank.transform, turret);
        shooter.ConfigureDamage(TankTeam.Player, isMaus ? MausProjectileDamage : ProjectileDamage);
        shooter.ConfigureLowerProjectileHitbox(isMaus);

        TankCombatRewards combatRewards = EnsureComponent<TankCombatRewards>(tank);
        TankSpecialWeapon specialWeapon = EnsureComponent<TankSpecialWeapon>(tank);
        specialWeapon.Configure(muzzlePoint, missilePrefab, combatRewards, camera);

        TankAimLaser aimLaser = EnsureComponent<TankAimLaser>(tank);
        aimLaser.enabled = true;
        aimLaser.Configure(muzzlePoint, turret, shooter);

        TankAudioController tankAudio = EnsureComponent<TankAudioController>(tank);
        tankAudio.Configure(controller, shooter, muzzlePoint, LoadMovementClip(), LoadShotClip());

        MuzzleShotEffect muzzleEffect = EnsureComponent<MuzzleShotEffect>(tank);
        muzzleEffect.Configure(muzzlePoint, DefaultForwardAxis, shooter);

        TankTrackDust trackDust = EnsureComponent<TankTrackDust>(tank);
        trackDust.Configure(controller, DefaultForwardAxis);

        if (camera != null)
        {
            TopDownCameraFollow follow = EnsureComponent<TopDownCameraFollow>(camera.gameObject);
            follow.enabled = true;
            follow.Configure(tank.transform, TopDownCameraFollow.DefaultOffset, TopDownCameraFollow.DefaultLookOffset);
            follow.ConfigureTurretCamera(new Vector3(0.8f, 4.5f, -1.2f));
            follow.ConfigureShakeSources(shooter, playerHealth);
            camera.fieldOfView = 58f;
        }

        EnsureGridFloor();
        EnsureSceneLight();
        EnsureRockShadows();
        EnsureWallMeshColliders();
        EnsureSceneAudio();
        ImpactExplosion.ConfigureAudio(LoadRicochetClip(), LoadExplosionClip());
        EnsurePhysicsBoxes(persistent);
        GameObject playerUi = EnsurePlayerHealthBar(playerHealth);
        currentPlayerUi = playerUi;
        Transform oldSelection = playerUi.transform.Find("Tank Selection Panel");
        if (oldSelection != null) { oldSelection.gameObject.SetActive(false); Object.Destroy(oldSelection.gameObject); }
        foreach (var legacy in playerUi.GetComponentsInChildren<TankSelectionMenu>(true))
            Object.Destroy(legacy);
        currentWaveAnnouncement = EnsureWaveAnnouncement(playerUi.transform);
        if (Application.isPlaying) EnsureMainMenu(tank, camera);
        if (!battleStarted)
        {
            playerUi.SetActive(false);
            SetPlayerTankControl(false);
            PlayerHealthBar.GameplayInputBlocked = true;
            SetGameplayAudioMuted(true);
        }

        if (persistent)
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(tank);
            if (camera != null)
            {
                EditorUtility.SetDirty(camera.gameObject);
            }

            GridFloor gridFloor = Object.FindFirstObjectByType<GridFloor>();
            if (gridFloor != null)
            {
                EditorUtility.SetDirty(gridFloor.gameObject);
            }

            PlayerHealthBar healthBar = Object.FindFirstObjectByType<PlayerHealthBar>();
            if (healthBar != null)
            {
                EditorUtility.SetDirty(healthBar.gameObject);
            }

            foreach (PhysicsCrateMarker crate in Object.FindObjectsByType<PhysicsCrateMarker>(FindObjectsSortMode.None))
            {
                EditorUtility.SetDirty(crate.gameObject);
            }
#endif
        }
    }

    public static void StartBattle()
    {
        StartBattle(false);
    }

    public static void StartInfiniteBattle()
    {
        StartBattle(true);
    }

    private static void StartBattle(bool infinite) => currentMainMenu?.BeginBattle(infinite);

    public static GameObject LoadGarageTank(int skin) => skin == 3 ? LoadMausTankPrefab() :
        skin == 1 ? LoadDesertTankPrefab() : skin == 2 ? LoadSnowTankPrefab() : LoadTankPrefab();
    public static Transform FindGarageTurret(Transform root) => FindTankTurret(root);
    public static AudioClip LoadGarageShot() => LoadShotClip();

    public static GameObject PrepareBattleFromGarage(int skin, bool infinite)
    {
        battleStarted = true;
        infiniteMode = infinite;
        ClearRuntimeBattleObjects();
        currentTank.SetActive(true);
        if (skin == 3) ApplyMausTank(currentTank);
        else if (skin == 1) ApplyDesertTankSkin(currentTank);
        else if (skin == 2) ApplySnowTankSkin(currentTank);
        else ApplyNormalTankSkin(currentTank);
        SetPlayerTankControl(false);
        PlayerHealthBar.GameplayInputBlocked = true;
        currentPlayerHealth.Configure(TankTeam.Player, GetPlayerMaxHealth(currentTank), false);
        var follow = currentCamera.GetComponent<TopDownCameraFollow>();
        follow.Configure(currentTank.transform, TopDownCameraFollow.DefaultOffset, TopDownCameraFollow.DefaultLookOffset);
        follow.SetFrozen(false);
        follow.enabled = false;
        currentCamera.fieldOfView = 58f;
        currentTank.GetComponent<TankController>()?.RefreshMovementPlane();
        return currentPlayerUi;
    }

    public static void FinishBattleFromGarage()
    {
        Time.timeScale = 1;
        PlayerHealthBar.GameplayInputBlocked = false;
        SetPlayerTankControl(true);
        currentCamera.GetComponent<TopDownCameraFollow>().enabled = true;
        SetGameplayAudioMuted(false);
        StartWavesAfterTankSelection();
    }

    public static void StartWavesAfterTankSelection()
    {
        if (!battleStarted || currentMissilePrefab == null || currentPlayerHealth == null)
        {
            return;
        }

        EnsureEnemyWaves(currentMissilePrefab, currentPlayerHealth, currentWaveAnnouncement, false, infiniteMode);
    }

    public static void ReturnToMainMenu() => RestartGameplayScene();

    public static void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public static void RestartGameplayScene()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        PlayerHealthBar.GameplayInputBlocked = false;
        battleStarted = false;
        infiniteMode = false;
        ResetCurrentTankToInitialPose();
        ClearRuntimeBattleObjects();
        currentTank = null;
        currentMissilePrefab = null;
        currentCamera = null;
        currentPlayerHealth = null;
        currentPlayerUi = null;

        currentWaveAnnouncement = null;
        currentMainMenu = null;
        hasInitialTankPose = false;
        lastSetupFrame = -1;

        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.name);
    }

    private static void ResetCurrentTankToInitialPose()
    {
        if (currentTank == null || !hasInitialTankPose)
        {
            return;
        }

        currentTank.transform.SetPositionAndRotation(initialTankPosition, initialTankRotation);
        Rigidbody body = currentTank.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = initialTankPosition;
            body.rotation = initialTankRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        TankController controller = currentTank.GetComponent<TankController>();
        if (controller != null)
        {
            controller.RefreshMovementPlane();
        }
    }

    private static void SetPlayerTankControl(bool isEnabled)
    {
        if (currentTank == null)
        {
            return;
        }

        TankController controller = currentTank.GetComponent<TankController>();
        if (controller != null)
        {
            controller.enabled = isEnabled;
        }

        TankShooter shooter = currentTank.GetComponent<TankShooter>();
        if (shooter != null)
        {
            shooter.enabled = isEnabled;
        }

        TankTurretAim turretAim = currentTank.GetComponent<TankTurretAim>();
        if (turretAim != null)
        {
            turretAim.enabled = isEnabled;
        }
    }

    private static int GetPlayerMaxHealth(GameObject tank)
    {
        Transform turret = tank != null ? FindTankTurret(tank.transform) : null;
        return IsMausTank(tank != null ? tank.transform : null, turret) ? MausTankMaxHealth : TankMaxHealth;
    }

    private static void ClearRuntimeBattleObjects()
    {
        foreach (EnemyWaveSpawner spawner in Object.FindObjectsByType<EnemyWaveSpawner>(FindObjectsSortMode.None))
        {
            Object.Destroy(spawner.gameObject);
        }

        foreach (TankHealth health in Object.FindObjectsByType<TankHealth>(FindObjectsSortMode.None))
        {
            if (health != null && health.Team == TankTeam.Enemy)
            {
                Object.Destroy(health.gameObject);
            }
        }

        foreach (HealthPickup pickup in Object.FindObjectsByType<HealthPickup>(FindObjectsSortMode.None))
        {
            Object.Destroy(pickup.gameObject);
        }

        foreach (ProjectileMovement projectile in Object.FindObjectsByType<ProjectileMovement>(FindObjectsSortMode.None))
        {
            Object.Destroy(projectile.gameObject);
        }
    }

    private static void SetGameplayAudioMuted(bool muted)
    {
        SceneAudioController sceneAudio = Object.FindFirstObjectByType<SceneAudioController>();
        if (sceneAudio != null)
        {
            sceneAudio.SetMutedForMenu(muted);
        }

        foreach (TankAudioController tankAudio in Object.FindObjectsByType<TankAudioController>(FindObjectsSortMode.None))
        {
            AudioSource[] sources = tankAudio.GetComponentsInChildren<AudioSource>(true);
            foreach (AudioSource source in sources)
            {
                source.mute = muted;
                if (muted)
                {
                    source.Pause();
                }
                else
                {
                    source.UnPause();
                }
            }
        }
    }

    private static void EnsurePhysicsBoxes(bool persistent)
    {
        for (int i = 0; i < DefaultBoxPositions.Length; i++)
        {
            string boxName = $"Physics Box {i + 1}";
            GameObject box = GetOrCreatePhysicsBox(boxName, out bool wasCreated);
            if (box == null)
            {
                continue;
            }

            if (wasCreated)
            {
                Vector3 boxPosition = DefaultBoxPositions[i];
                boxPosition.y = GetGroundY(boxPosition);
                box.transform.position = boxPosition;
                box.transform.rotation = Quaternion.Euler(DefaultBoxEulerAngles[i]);
            }

            ConfigurePhysicsBox(box);

            Rigidbody body = EnsureComponent<Rigidbody>(box);
            AlignPhysicsBoxToGround(box, body);

#if UNITY_EDITOR
            if (persistent)
            {
                EditorUtility.SetDirty(box);
            }
#endif
        }
    }

    private static GameObject GetOrCreatePhysicsBox(string boxName, out bool wasCreated)
    {
        wasCreated = false;
        GameObject existing = GameObject.Find(boxName);
        if (existing != null)
        {
            return existing;
        }

        GameObject sceneTemplate = FindSceneBoxTemplate(boxName);
        GameObject boxPrefab = LoadBoxPrefab();
        GameObject box = null;
        if (sceneTemplate != null)
        {
            box = Object.Instantiate(sceneTemplate);
        }
        else if (boxPrefab != null)
        {
#if UNITY_EDITOR
            box = !Application.isPlaying
                ? (GameObject)PrefabUtility.InstantiatePrefab(boxPrefab)
                : Object.Instantiate(boxPrefab);
#else
            box = Object.Instantiate(boxPrefab);
#endif
        }

        if (box == null)
        {
            box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        }

        box.name = boxName;
        wasCreated = true;
        return box;
    }

    private static void ConfigurePhysicsBox(GameObject box)
    {
        EnsureBoxCollider(box);

        Rigidbody body = EnsureComponent<Rigidbody>(box);
        body.mass = 90f;
        body.useGravity = true;
        body.isKinematic = false;
        body.linearDamping = 0.35f;
        body.angularDamping = 0.55f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        EnsureComponent<PhysicsCrateMarker>(box);
        ConfigureShadowCasters(box);
    }

    private static void EnsureBoxCollider(GameObject box)
    {
        BoxCollider boxCollider = box.GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            boxCollider.isTrigger = false;
            return;
        }

        boxCollider = box.AddComponent<BoxCollider>();
        boxCollider.isTrigger = false;
        if (!TryGetLocalRendererBounds(box, out Bounds localBounds))
        {
            return;
        }

        boxCollider.center = localBounds.center;
        boxCollider.size = localBounds.size;
    }

    private static bool HasUsableBoxCollider(GameObject box)
    {
        BoxCollider boxCollider = box != null ? box.GetComponent<BoxCollider>() : null;
        return boxCollider != null && !boxCollider.isTrigger;
    }

    private static GameObject FindSceneBoxTemplate(string excludedName)
    {
        string[] templateNames = { "Level Box", "Level Box (1)", "Box" };
        foreach (string templateName in templateNames)
        {
            if (templateName == excludedName)
            {
                continue;
            }

            GameObject template = GameObject.Find(templateName);
            if (HasUsableBoxCollider(template))
            {
                return template;
            }
        }

        return null;
    }

    private static void AlignPhysicsBoxToGround(GameObject box, Rigidbody body)
    {
        if (!TryGetColliderBounds(box, out Bounds bounds))
        {
            AlignTankBottomToGround(box, body, GetGroundY(box.transform.position) + PhysicsBoxGroundClearance);
            return;
        }

        float groundY = GetGroundYForBounds(bounds);
        Vector3 position = box.transform.position;
        position.y += groundY + PhysicsBoxGroundClearance - bounds.min.y;
        box.transform.position = position;

        if (body != null)
        {
            body.position = position;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        Physics.SyncTransforms();
    }

    private static bool TryGetColliderBounds(GameObject root, out Bounds combinedBounds)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        combinedBounds = default;
        bool hasBounds = false;

        foreach (Collider collider in colliders)
        {
            if (collider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = collider.bounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(collider.bounds);
        }

        return hasBounds;
    }

    private static bool TryGetLocalRendererBounds(GameObject root, out Bounds localBounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        localBounds = default;
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            Bounds bounds = renderer.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        Vector3 corner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        Vector3 localCorner = root.transform.InverseTransformPoint(corner);

                        if (!hasBounds)
                        {
                            localBounds = new Bounds(localCorner, Vector3.zero);
                            hasBounds = true;
                            continue;
                        }

                        localBounds.Encapsulate(localCorner);
                    }
                }
            }
        }

        return hasBounds;
    }

    private static float GetGroundYForBounds(Bounds bounds)
    {
        float groundY = GetGroundY(bounds.center);
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        groundY = Mathf.Max(groundY, GetGroundY(new Vector3(min.x, 0f, min.z)));
        groundY = Mathf.Max(groundY, GetGroundY(new Vector3(min.x, 0f, max.z)));
        groundY = Mathf.Max(groundY, GetGroundY(new Vector3(max.x, 0f, min.z)));
        groundY = Mathf.Max(groundY, GetGroundY(new Vector3(max.x, 0f, max.z)));
        return groundY;
    }

    private static void EnsureEnemyWaves(GameObject missilePrefab, TankHealth playerHealth, EnemyWaveAnnouncement waveAnnouncement, bool persistent, bool infinite = false)
    {
        GameObject tankPrefab = LoadEnemyTankPrefab();
        if (tankPrefab == null || missilePrefab == null || playerHealth == null)
        {
            return;
        }

        GameObject spawnerObject = GameObject.Find("Enemy Wave Spawner");
        if (spawnerObject == null)
        {
            spawnerObject = new GameObject("Enemy Wave Spawner");
        }

        EnemyWaveSpawner spawner = EnsureComponent<EnemyWaveSpawner>(spawnerObject);
        spawner.Configure(playerHealth, tankPrefab, LoadMausTankPrefab(), missilePrefab, waveAnnouncement, infinite);

#if UNITY_EDITOR
        if (persistent)
        {
            EditorUtility.SetDirty(spawnerObject);
        }
#endif
    }

    private static void EnsureEnemies(GameObject missilePrefab, TankHealth playerHealth, bool persistent)
    {
        GameObject tankPrefab = LoadEnemyTankPrefab();
        if (tankPrefab == null || playerHealth == null)
        {
            return;
        }

        for (int i = 0; i < DefaultEnemyPositions.Length; i++)
        {
            string enemyName = $"Enemy Tank {i + 1}";
            GameObject enemy = GameObject.Find(enemyName);
            bool wasCreated = enemy == null;
            if (enemy == null)
            {
#if UNITY_EDITOR
                enemy = persistent
                    ? (GameObject)PrefabUtility.InstantiatePrefab(tankPrefab)
                    : Object.Instantiate(tankPrefab);
#else
                enemy = Object.Instantiate(tankPrefab);
#endif
                enemy.name = enemyName;
            }

            if (wasCreated)
            {
                Vector3 enemyPosition = DefaultEnemyPositions[i];
                enemyPosition.y = GetGroundY(enemyPosition);
                Vector3 directionToPlayer = TankPlaneMath.Flatten(playerHealth.transform.position - enemyPosition);
                Quaternion enemyRotation = directionToPlayer.sqrMagnitude > 0.001f
                    ? TankPlaneMath.RotationLookingAlong(directionToPlayer, DefaultForwardAxis)
                    : Quaternion.identity;
                enemy.transform.SetPositionAndRotation(enemyPosition, enemyRotation);
            }

            ConfigureEnemy(enemy, missilePrefab, playerHealth);

#if UNITY_EDITOR
            if (persistent)
            {
                EditorUtility.SetDirty(enemy);
            }
#endif
        }
    }

    public static void ConfigureEnemy(GameObject enemy, GameObject missilePrefab, TankHealth playerHealth)
    {
        Transform turret = FindTankTurret(enemy.transform);
        turret = turret != null ? turret : enemy.transform;
        bool isMaus = IsMausTank(enemy.transform, turret);

        TankController controller = enemy.GetComponent<TankController>();
        if (controller == null)
        {
            controller = enemy.AddComponent<TankController>();
        }

        if (controller != null)
        {
            controller.enabled = true;
            controller.ConfigureModelAxis(DefaultForwardAxis);
            controller.ConfigureMovement(
                isMaus ? TankForwardSpeed : TankForwardSpeed * 0.72f,
                isMaus ? TankReverseSpeed : TankReverseSpeed * 0.55f,
                isMaus ? TankAcceleration : TankAcceleration * 0.8f);
            controller.SetExternalInput(0f, 0f);
        }

        TankTurretAim mouseAim = enemy.GetComponent<TankTurretAim>();
        if (mouseAim != null)
        {
            mouseAim.enabled = false;
        }

        TankShooter playerShooter = enemy.GetComponent<TankShooter>();
        if (playerShooter != null)
        {
            playerShooter.enabled = false;
        }

        Rigidbody body = EnsureComponent<Rigidbody>(enemy);
        body.useGravity = false;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        AlignTankBottomToGround(enemy, body, GetGroundY(enemy.transform.position));
        if (controller != null)
        {
            controller.RefreshMovementPlane();
        }

        EnsureSingleBodyMeshCollider(enemy);
        ConfigureShadowCasters(enemy);

        TankHealth enemyHealth = EnsureComponent<TankHealth>(enemy);
        enemyHealth.Configure(TankTeam.Enemy, isMaus ? MausTankMaxHealth : TankMaxHealth, false);

        TankDeathEffect deathEffect = EnsureComponent<TankDeathEffect>(enemy);
        deathEffect.Configure(enemyHealth, turret, true);

        Transform muzzlePoint = FindChildRecursive(turret, "MuzzlePoint");
        if (muzzlePoint == null)
        {
            muzzlePoint = CreateMuzzlePoint(turret);
        }
        else
        {
            PositionMuzzlePoint(turret, muzzlePoint);
        }

        StaticEnemyTank enemyTank = EnsureComponent<StaticEnemyTank>(enemy);
        enemyTank.Configure(playerHealth, turret, muzzlePoint, missilePrefab, ProjectileSpeed, isMaus ? MausProjectileDamage : ProjectileDamage, EnemyAttackRange, EnemyDetectionRange, EnemyShotCooldown, DefaultForwardAxis);
        enemyTank.ConfigureShotAudio(LoadShotClip());
        enemyTank.ConfigureLowerProjectileHitbox(isMaus);
        MuzzleShotEffect muzzleEffect = EnsureComponent<MuzzleShotEffect>(enemy);
        muzzleEffect.Configure(muzzlePoint, DefaultForwardAxis);
        enemyTank.ConfigureShotEffect(muzzleEffect);

        TankWorldHealthBar worldHealthBar = EnsureComponent<TankWorldHealthBar>(enemy);
        worldHealthBar.Configure(enemyHealth, currentCamera != null ? currentCamera : Camera.main);
    }

    public static void ApplyDesertTankSkin(GameObject tank)
    {
        RestoreOriginalTankModel(tank);
        ApplyTankMaterialsFromPrefab(tank, LoadDesertTankPrefab());
        RefreshPlayerTankRig(tank);
    }

    public static void ApplyNormalTankSkin(GameObject tank)
    {
        RestoreOriginalTankModel(tank);
        ApplyTankMaterialsFromPrefab(tank, LoadTankPrefab());
        RefreshPlayerTankRig(tank);
    }

    public static void ApplySnowTankSkin(GameObject tank)
    {
        RestoreOriginalTankModel(tank);
        ApplyTankMaterialsFromPrefab(tank, LoadSnowTankPrefab());
        RefreshPlayerTankRig(tank);
    }

    public static void ApplyMausTank(GameObject tank)
    {
        GameObject mausPrefab = LoadMausTankPrefab();
        if (tank == null || mausPrefab == null)
        {
            return;
        }

        RestoreOriginalTankModel(tank);
        SetOriginalTankRenderersEnabled(tank, false);

        GameObject runtimeRoot = new GameObject(RuntimeTankModelRootName);
        runtimeRoot.transform.SetParent(tank.transform, false);

        GameObject visual = Object.Instantiate(mausPrefab, runtimeRoot.transform);
        visual.name = "Tank_Maus Visual";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one * MausVisualScale;
        DisableNestedRuntimeComponents(visual);
        RefreshPlayerTankRig(tank);
    }

    private static void ApplyTankMaterialsFromPrefab(GameObject tank, GameObject sourcePrefab)
    {
        if (tank == null || sourcePrefab == null)
        {
            return;
        }

        Renderer[] targetRenderers = tank.GetComponentsInChildren<Renderer>(true);
        Renderer[] sourceRenderers = sourcePrefab.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer targetRenderer in targetRenderers)
        {
            Renderer sourceRenderer = FindMatchingRenderer(sourceRenderers, sourcePrefab.transform, targetRenderer.transform, tank.transform);
            if (sourceRenderer == null)
            {
                continue;
            }

            targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
        }
    }

    private static void RefreshPlayerTankRig(GameObject tank)
    {
        if (tank == null)
        {
            return;
        }

        Rigidbody body = EnsureComponent<Rigidbody>(tank);
        AlignTankBottomToGround(tank, body, GetGroundY(tank.transform.position));
        EnsureSingleBodyMeshCollider(tank);
        ConfigureShadowCasters(tank);

        TankController controller = EnsureComponent<TankController>(tank);
        controller.ConfigureModelAxis(DefaultForwardAxis);
        controller.ConfigureMovement(TankForwardSpeed, TankReverseSpeed, TankAcceleration);
        controller.RefreshMovementPlane();

        TankNitro playerNitro = EnsureComponent<TankNitro>(tank);
        playerNitro.Configure(controller);

        Transform turret = FindTankTurret(tank.transform);
        turret = turret != null ? turret : tank.transform;
        bool isMaus = IsMausTank(tank.transform, turret);

        TankHealth playerHealth = EnsureComponent<TankHealth>(tank);
        playerHealth.Configure(TankTeam.Player, isMaus ? MausTankMaxHealth : TankMaxHealth, false);
        currentPlayerHealth = playerHealth;
        TankDeathEffect playerDeathEffect = EnsureComponent<TankDeathEffect>(tank);
        playerDeathEffect.Configure(playerHealth, turret, false);

        Transform muzzlePoint = FindChildRecursive(turret, "MuzzlePoint");
        if (muzzlePoint == null)
        {
            muzzlePoint = CreateMuzzlePoint(turret);
        }
        else
        {
            PositionMuzzlePoint(turret, muzzlePoint);
        }

        TankTurretAim turretAim = EnsureComponent<TankTurretAim>(tank);
        turretAim.Configure(turret, currentCamera);
        turretAim.ConfigureAimSettings(PlayerMouseYawSensitivity, PlayerTurretRotationSpeed);

        TankShooter shooter = EnsureComponent<TankShooter>(tank);
        shooter.Configure(turret, currentMissilePrefab, muzzlePoint);
        shooter.ConfigureProjectileSpeed(ProjectileSpeed);
        shooter.ConfigureShotCooldown(PlayerShotCooldown);
        shooter.ConfigureDamage(TankTeam.Player, isMaus ? MausProjectileDamage : ProjectileDamage);
        shooter.ConfigureLowerProjectileHitbox(isMaus);

        TankCombatRewards combatRewards = EnsureComponent<TankCombatRewards>(tank);
        TankSpecialWeapon specialWeapon = EnsureComponent<TankSpecialWeapon>(tank);
        specialWeapon.Configure(muzzlePoint, currentMissilePrefab, combatRewards, currentCamera);

        TankAimLaser aimLaser = EnsureComponent<TankAimLaser>(tank);
        aimLaser.enabled = true;
        aimLaser.Configure(muzzlePoint, turret, shooter);

        TankAudioController tankAudio = EnsureComponent<TankAudioController>(tank);
        tankAudio.Configure(controller, shooter, muzzlePoint, LoadMovementClip(), LoadShotClip());

        MuzzleShotEffect muzzleEffect = EnsureComponent<MuzzleShotEffect>(tank);
        muzzleEffect.Configure(muzzlePoint, DefaultForwardAxis, shooter);

        TankTrackDust trackDust = EnsureComponent<TankTrackDust>(tank);
        trackDust.Configure(controller, DefaultForwardAxis);

        if (currentCamera != null)
        {
            TopDownCameraFollow follow = EnsureComponent<TopDownCameraFollow>(currentCamera.gameObject);
            follow.ConfigureTurretCamera(new Vector3(0.8f, 4.5f, -1.2f));
            follow.ConfigureShakeSources(shooter, playerHealth);
        }
    }

    private static void RestoreOriginalTankModel(GameObject tank)
    {
        if (tank == null)
        {
            return;
        }

        Transform runtimeRoot = tank.transform.Find(RuntimeTankModelRootName);
        if (runtimeRoot != null)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(runtimeRoot.gameObject);
            }
            else
            {
                Object.DestroyImmediate(runtimeRoot.gameObject);
            }
        }

        SetOriginalTankRenderersEnabled(tank, true);
    }

    private static void SetOriginalTankRenderersEnabled(GameObject tank, bool isEnabled)
    {
        if (tank == null)
        {
            return;
        }

        Transform runtimeRoot = tank.transform.Find(RuntimeTankModelRootName);
        Renderer[] renderers = tank.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (runtimeRoot != null && renderer.transform.IsChildOf(runtimeRoot))
            {
                continue;
            }

            renderer.enabled = isEnabled;
        }
    }

    private static void DisableNestedRuntimeComponents(GameObject visual)
    {
        if (visual == null)
        {
            return;
        }

        Rigidbody[] rigidbodies = visual.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody rigidbody in rigidbodies)
        {
            rigidbody.isKinematic = true;
            rigidbody.detectCollisions = false;
        }

        TankController[] controllers = visual.GetComponentsInChildren<TankController>(true);
        foreach (TankController controller in controllers)
        {
            controller.enabled = false;
        }

        TankShooter[] shooters = visual.GetComponentsInChildren<TankShooter>(true);
        foreach (TankShooter shooter in shooters)
        {
            shooter.enabled = false;
        }

        TankTurretAim[] aims = visual.GetComponentsInChildren<TankTurretAim>(true);
        foreach (TankTurretAim aim in aims)
        {
            aim.enabled = false;
        }
    }

    private static Renderer FindMatchingRenderer(Renderer[] renderers, Transform sourceRoot, Transform target, Transform targetRoot)
    {
        string relativePath = GetRelativePath(target, targetRoot);
        foreach (Renderer renderer in renderers)
        {
            if (GetRelativePath(renderer.transform, sourceRoot) == relativePath)
            {
                return renderer;
            }
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer.name == target.name)
            {
                return renderer;
            }
        }

        return null;
    }

    private static string GetRelativePath(Transform transform, Transform root)
    {
        if (transform == null || root == null || transform == root)
        {
            return string.Empty;
        }

        string path = transform.name;
        Transform current = transform.parent;
        while (current != null && current != root)
        {
            path = $"{current.name}/{path}";
            current = current.parent;
        }

        return path;
    }

    private static void EnsureSceneAudio()
    {
        SceneAudioController existingAudio = Object.FindFirstObjectByType<SceneAudioController>();
        GameObject audioObject = existingAudio != null ? existingAudio.gameObject : new GameObject("Scene Audio");
        SceneAudioController sceneAudio = EnsureComponent<SceneAudioController>(audioObject);
        sceneAudio.Configure(LoadAmbientClip(), LoadMusicAmbientClip());
    }

    public static GameObject EnsureGridFloor()
    {
        Terrain terrain = GetActiveTerrain();
        if (terrain != null)
        {
            RemoveGridFloors();
            return terrain.gameObject;
        }

        GridFloor existingGrid = Object.FindFirstObjectByType<GridFloor>();
        if (existingGrid != null)
        {
            existingGrid.transform.position = new Vector3(0f, GroundY, 0f);
            existingGrid.Configure(FloorSize, FloorTileSize);
            return existingGrid.gameObject;
        }

        GameObject grid = new GameObject("Grid Floor");
        grid.transform.position = new Vector3(0f, GroundY, 0f);
        grid.AddComponent<MeshFilter>();
        grid.AddComponent<MeshRenderer>();
        grid.AddComponent<BoxCollider>();
        GridFloor gridFloor = grid.AddComponent<GridFloor>();
        gridFloor.Configure(FloorSize, FloorTileSize);
        return grid;
    }

    private static Terrain GetActiveTerrain()
    {
        if (Terrain.activeTerrain != null)
        {
            return Terrain.activeTerrain;
        }

        return Object.FindFirstObjectByType<Terrain>();
    }

    public static float GetGroundY(Vector3 position)
    {
        Terrain terrain = GetActiveTerrain();
        if (terrain == null || terrain.terrainData == null)
        {
            return GroundY;
        }

        return terrain.transform.position.y + terrain.SampleHeight(position);
    }

    private static void RemoveGridFloors()
    {
        GridFloor[] gridFloors = Object.FindObjectsByType<GridFloor>(FindObjectsSortMode.None);
        foreach (GridFloor gridFloor in gridFloors)
        {
            if (gridFloor == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(gridFloor.gameObject);
            }
            else
            {
                Object.DestroyImmediate(gridFloor.gameObject);
            }
        }
    }

    private static void EnsureSceneLight()
    {
        Light directionalLight = null;
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light light in lights)
        {
            if (light.type == LightType.Directional)
            {
                directionalLight = light;
                break;
            }
        }

        if (directionalLight == null)
        {
            GameObject lightObject = new GameObject("Directional Light");
            directionalLight = lightObject.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
        }

        directionalLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        directionalLight.intensity = 1.35f;
        directionalLight.shadows = LightShadows.Soft;
        directionalLight.shadowStrength = 0.75f;
        directionalLight.shadowBias = 0.05f;
        directionalLight.shadowNormalBias = 0.35f;
    }

    private static void ConfigureShadowCasters(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private static void EnsureRockShadows()
    {
        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer is ParticleSystemRenderer)
            {
                continue;
            }

            string objectName = renderer.gameObject.name;
            string rootName = renderer.transform.root != null ? renderer.transform.root.name : string.Empty;
            if (!objectName.Contains("rock", System.StringComparison.OrdinalIgnoreCase)
                && !rootName.Contains("rock", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private static void EnsureWallMeshColliders()
    {
        GameObject wallsRoot = GameObject.Find("Walls");
        if (wallsRoot != null)
        {
            ConfigureWallMeshColliders(wallsRoot);
        }

        MeshFilter[] meshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            string objectName = meshFilter.gameObject.name;
            string rootName = meshFilter.transform.root != null ? meshFilter.transform.root.name : string.Empty;
            if (!objectName.Contains("wall", System.StringComparison.OrdinalIgnoreCase)
                && !objectName.Contains("stolb", System.StringComparison.OrdinalIgnoreCase)
                && !rootName.Contains("wall", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ConfigureWallMeshCollider(meshFilter);
        }
    }

    private static void ConfigureWallMeshColliders(GameObject root)
    {
        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            ConfigureWallMeshCollider(meshFilter);
        }
    }

    private static void ConfigureWallMeshCollider(MeshFilter meshFilter)
    {
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return;
        }

        MeshCollider meshCollider = meshFilter.GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
        }

        meshCollider.sharedMesh = meshFilter.sharedMesh;
        meshCollider.convex = false;
        meshCollider.isTrigger = false;

        Renderer renderer = meshFilter.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private static void AlignTankBottomToGround(GameObject tank, Rigidbody body, float groundY)
    {
        if (!TryGetRendererBounds(tank, out Bounds bounds))
        {
            return;
        }

        Vector3 position = tank.transform.position;
        position.y += groundY - bounds.min.y;
        tank.transform.position = position;

        if (body != null)
        {
            body.position = position;
        }
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds combinedBounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        combinedBounds = default;
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled
                || renderer is LineRenderer
                || renderer is TrailRenderer
                || renderer is ParticleSystemRenderer)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(renderer.bounds);
        }

        return hasBounds;
    }

    private static void EnsureSingleBodyMeshCollider(GameObject tank)
    {
        Transform turret = FindTankTurret(tank.transform);
        Transform colliderTarget = FindTankBody(tank.transform, turret);
        if (colliderTarget == null || colliderTarget == turret)
        {
            colliderTarget = FindLargestMeshTransform(tank.transform, turret);
        }

        if (colliderTarget == null)
        {
            return;
        }

        MeshFilter meshFilter = colliderTarget.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = colliderTarget.GetComponentInChildren<MeshFilter>();
        }

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return;
        }

        MeshCollider meshCollider = meshFilter.GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
        }

        meshCollider.sharedMesh = meshFilter.sharedMesh;
        meshCollider.convex = true;
        meshCollider.isTrigger = false;
        RemoveExtraTankColliders(tank, meshCollider);
    }

    private static Transform FindTankTurret(Transform root)
    {
        Transform runtimeRoot = root != null ? root.Find(RuntimeTankModelRootName) : null;
        Transform runtimeTurret = FindChildRecursive(runtimeRoot, "tank turret")
            ?? FindChildRecursive(runtimeRoot, "Cylinder.002")
            ?? FindChildRecursive(runtimeRoot, "cylinder.002");
        if (runtimeTurret != null)
        {
            return runtimeTurret;
        }

        return FindChildRecursive(root, "tank turret")
            ?? FindChildRecursive(root, "Cylinder.002")
            ?? FindChildRecursive(root, "cylinder.002");
    }

    private static Transform FindTankBody(Transform root, Transform turret)
    {
        Transform runtimeRoot = root != null ? root.Find(RuntimeTankModelRootName) : null;
        Transform runtimeBody = FindChildRecursive(runtimeRoot, "body")
            ?? FindChildRecursive(runtimeRoot, "Cylinder")
            ?? FindChildRecursive(runtimeRoot, "cylinder");
        if (runtimeBody != null && runtimeBody != turret)
        {
            return runtimeBody;
        }

        Transform body = FindChildRecursive(root, "body")
            ?? FindChildRecursive(root, "Cylinder")
            ?? FindChildRecursive(root, "cylinder");
        return body != turret ? body : null;
    }

    private static bool IsMausTurret(Transform turret)
    {
        return turret != null && string.Equals(turret.name, "tank turret", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMausTank(Transform root, Transform turret)
    {
        if (IsMausTurret(turret))
        {
            return true;
        }

        if (root != null && root.name.Contains("Maus", System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return FindChildRecursive(root, "tank turret") != null;
    }

    private static void EnsureMeshCollidersOnAllParts(GameObject tank)
    {
        Collider[] existingColliders = tank.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in existingColliders)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }
        }

        MeshFilter[] meshFilters = tank.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null)
            {
                continue;
            }

            MeshCollider meshCollider = meshFilter.gameObject.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
            }

            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = true;
            meshCollider.isTrigger = false;
        }
    }

    private static Transform FindLargestMeshTransform(Transform root, Transform excludedRoot)
    {
        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        Transform best = null;
        float bestSize = 0f;

        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null || (excludedRoot != null && meshFilter.transform.IsChildOf(excludedRoot)))
            {
                continue;
            }

            Renderer renderer = meshFilter.GetComponent<Renderer>();
            float size = renderer != null ? renderer.bounds.size.sqrMagnitude : meshFilter.sharedMesh.bounds.size.sqrMagnitude;
            if (size > bestSize)
            {
                best = meshFilter.transform;
                bestSize = size;
            }
        }

        return best;
    }

    private static void RemoveExtraTankColliders(GameObject tank, Collider colliderToKeep)
    {
        Collider[] colliders = tank.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (collider == colliderToKeep)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }
        }
    }

    public static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root)
        {
            if (string.Equals(child.name, childName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    public static Transform CreateMuzzlePoint(Transform turret)
    {
        GameObject muzzle = new GameObject("MuzzlePoint");
        muzzle.transform.SetParent(turret, false);
        muzzle.transform.localRotation = Quaternion.identity;
        PositionMuzzlePoint(turret, muzzle.transform);
        return muzzle.transform;
    }

    private static void PositionMuzzlePoint(Transform turret, Transform muzzlePoint)
    {
        if (IsMausTurret(turret) && TryGetMausMuzzleLocalPosition(turret, out Vector3 mausMuzzlePosition))
        {
            muzzlePoint.localPosition = mausMuzzlePosition;
            muzzlePoint.localRotation = Quaternion.identity;
            return;
        }

        muzzlePoint.localPosition = DefaultForwardAxis * EstimateMuzzleDistance(turret, DefaultForwardAxis)
            + Vector3.up * MuzzleHeightOffset;
        muzzlePoint.localRotation = Quaternion.identity;
    }

    private static bool TryGetMausMuzzleLocalPosition(Transform turret, out Vector3 localPosition)
    {
        localPosition = Vector3.zero;
        if (turret == null)
        {
            return false;
        }

        Renderer[] renderers = turret.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return false;
        }

        bool hasBounds = false;
        float farthestForward = 0f;
        float highestBarrelY = 0f;
        foreach (Renderer renderer in renderers)
        {
            Bounds bounds = renderer.bounds;
            Vector3[] corners =
            {
                new Vector3(bounds.min.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.max.z)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 localCorner = turret.InverseTransformPoint(corners[i]);
                if (!hasBounds || localCorner.z > farthestForward)
                {
                    farthestForward = localCorner.z;
                    highestBarrelY = localCorner.y;
                    hasBounds = true;
                }
                else if (Mathf.Abs(localCorner.z - farthestForward) <= 0.05f)
                {
                    highestBarrelY = Mathf.Max(highestBarrelY, localCorner.y);
                }
            }
        }

        if (!hasBounds)
        {
            return false;
        }

        localPosition = new Vector3(0f, highestBarrelY, farthestForward + 0.18f);
        return true;
    }

    private static float EstimateMuzzleDistance(Transform turret, Vector3 localForwardAxis)
    {
        Renderer[] renderers = turret.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return 2.8f;
        }

        Vector3 axis = TankPlaneMath.Flatten(turret.TransformDirection(localForwardAxis));
        float farthest = 0f;

        foreach (Renderer renderer in renderers)
        {
            Bounds bounds = renderer.bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        farthest = Mathf.Max(farthest, Vector3.Dot(corner - turret.position, axis));
                    }
                }
            }
        }

        return Mathf.Max(0.8f, farthest + 0.25f);
    }

    private static GameObject FindTankInScene()
    {
        GameObject namedTank = GameObject.Find("Tank");
        if (namedTank != null)
        {
            return namedTank;
        }

        TankController existingController = Object.FindFirstObjectByType<TankController>();
        if (existingController != null)
        {
            return existingController.gameObject;
        }

        return null;
    }

    private static GameObject LoadMissilePrefab()
    {
        return LoadProjectAsset<GameObject>(MissilePrefabPath);
    }

    private static GameObject LoadTankPrefab()
    {
        return LoadProjectAsset<GameObject>(TankPrefabPath);
    }

    private static GameObject LoadEnemyTankPrefab()
    {
        GameObject enemyTank = LoadProjectAsset<GameObject>(TankEnemyPrefabPath);
        return enemyTank != null ? enemyTank : LoadProjectAsset<GameObject>(TankPrefabPath);
    }

    private static GameObject LoadMausTankPrefab()
    {
        return LoadProjectAsset<GameObject>(TankMausPrefabPath);
    }

    private static GameObject LoadDesertTankPrefab()
    {
        return LoadProjectAsset<GameObject>(TankDesertPrefabPath);
    }

    private static GameObject LoadSnowTankPrefab()
    {
        return LoadProjectAsset<GameObject>(TankSnowPrefabPath);
    }

    private static GameObject LoadBoxPrefab()
    {
        return LoadProjectAsset<GameObject>(BoxPrefabPath);
    }

    private static AudioClip LoadAmbientClip()
    {
        return LoadAudioClip(AmbientClipPath);
    }

    private static AudioClip LoadMovementClip()
    {
        return LoadAudioClip(MovementClipPath);
    }

    private static AudioClip LoadMusicAmbientClip()
    {
        return LoadAudioClip(MusicAmbientClipPath);
    }

    private static AudioClip LoadShotClip()
    {
        return LoadAudioClip(ShotClipPath);
    }

    private static AudioClip LoadRicochetClip()
    {
        return LoadAudioClip(RicochetClipPath);
    }

    private static AudioClip LoadExplosionClip()
    {
        return LoadAudioClip(ExplosionClipPath);
    }

    private static AudioClip LoadAudioClip(string path)
    {
        return LoadProjectAsset<AudioClip>(path);
    }

private static T LoadProjectAsset<T>(string assetPath) where T : Object
    {
#if UNITY_EDITOR
        T editorAsset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (editorAsset != null)
        {
            return editorAsset;
        }
#endif
        return Resources.Load<T>(ToResourcesPath(assetPath));
    }

    private static string ToResourcesPath(string assetPath)
    {
        const string assetsPrefix = "Assets/";
        const string resourcesPrefix = "Resources/";

        string path = assetPath.Replace('\\', '/');
        if (path.StartsWith(assetsPrefix))
        {
            path = path.Substring(assetsPrefix.Length);
        }

        int resourcesIndex = path.IndexOf(resourcesPrefix, System.StringComparison.OrdinalIgnoreCase);
        if (resourcesIndex >= 0)
        {
            path = path.Substring(resourcesIndex + resourcesPrefix.Length);
        }

        int extensionIndex = path.LastIndexOf('.');
        if (extensionIndex > 0)
        {
            path = path.Substring(0, extensionIndex);
        }

        return path;
    }

    private static GameObject EnsurePlayerHealthBar(TankHealth playerHealth)
    {
        PlayerHealthBar existingBar = Object.FindFirstObjectByType<PlayerHealthBar>();
        GameObject root = existingBar != null ? existingBar.gameObject : new GameObject("Player Health UI", typeof(RectTransform));

        Canvas canvas = EnsureComponent<Canvas>(root);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler canvasScaler = EnsureComponent<CanvasScaler>(root);
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1280f, 720f);

        EnsureComponent<GraphicRaycaster>(root);
        EnsureEventSystem();

        RectTransform backgroundRect;
        Image backgroundImage = GetOrCreateImage(root.transform, "Health Bar Background", out backgroundRect);
        backgroundRect.anchorMin = new Vector2(0f, 0f);
        backgroundRect.anchorMax = new Vector2(0f, 0f);
        backgroundRect.pivot = new Vector2(0f, 0f);
        backgroundRect.anchoredPosition = new Vector2(28f, 28f);
        backgroundRect.localScale = Vector3.one;
        backgroundImage.sprite = LoadUiSprite(HealthBarSpritePath);
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.preserveAspect = true;
        backgroundImage.color = Color.white;
        backgroundImage.raycastTarget = false;
        if (backgroundImage.sprite != null)
        {
            const float displayedHealthBarHeight = 240f;
            float sourceAspect = backgroundImage.sprite.rect.width / backgroundImage.sprite.rect.height;
            backgroundRect.sizeDelta = new Vector2(displayedHealthBarHeight * sourceAspect, displayedHealthBarHeight);
        }

        RectTransform fillRect;
        Image fillImage = GetOrCreateImage(backgroundRect, "Health Bar Fill", out fillRect);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillImage.sprite = LoadUiSprite(HealthBarGreenSpritePath);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Vertical;
        fillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
        fillImage.fillAmount = 1f;
        fillImage.preserveAspect = true;
        fillImage.color = Color.white;
        fillImage.raycastTarget = false;

        RectTransform boltRect;
        Image boltImage = GetOrCreateImage(backgroundRect, "Health Bar Bolt", out boltRect);
        boltRect.anchorMin = Vector2.zero;
        boltRect.anchorMax = Vector2.one;
        boltRect.offsetMin = Vector2.zero;
        boltRect.offsetMax = Vector2.zero;
        boltRect.localScale = Vector3.one;
        boltImage.sprite = LoadUiSprite(HealthBarBoltSpritePath);
        boltImage.type = Image.Type.Simple;
        boltImage.preserveAspect = true;
        boltImage.color = Color.white;
        boltImage.raycastTarget = false;
        boltRect.SetAsLastSibling();

        GameObject gameOverPanel = EnsureGameOverPanel(root.transform);
        Button restartButton = EnsureRestartButton(gameOverPanel.transform);
        Image gameplayCursor = EnsureGameplayCursor(root.transform);
        EnsureHitMarker(root.transform, canvas);
        EnsureEnemyMarkers(root.transform, canvas);
        Image damageVignette = EnsureDamageVignette(root.transform);
        EnsureNitroBar(root.transform, playerHealth.GetComponent<TankNitro>());
        EnsureNitroSpeedEffect(root.transform, playerHealth.GetComponent<TankNitro>());
        EnsureCombatRewardsUi(
            root.transform,
            playerHealth.GetComponent<TankCombatRewards>(),
            playerHealth.GetComponent<TankSpecialWeapon>());

        PlayerHealthBar healthBar = EnsureComponent<PlayerHealthBar>(root);
        healthBar.Configure(playerHealth, fillImage, gameOverPanel, restartButton, gameplayCursor);
        PlayerDamageVignette vignette = EnsureComponent<PlayerDamageVignette>(root);
        vignette.Configure(playerHealth, damageVignette);
        return root;
    }

    private static void EnsureNitroBar(Transform parent, TankNitro nitro)
    {
        RectTransform backgroundRect;
        Image background = GetOrCreateImage(parent, "Nitro Bar Background", out backgroundRect);
        backgroundRect.anchorMin = new Vector2(1f, 0f);
        backgroundRect.anchorMax = new Vector2(1f, 0f);
        backgroundRect.pivot = new Vector2(1f, 0f);
        backgroundRect.anchoredPosition = new Vector2(-28f, 28f);
        // The source art is square, so scale both axes together without distortion.
        backgroundRect.sizeDelta = new Vector2(180f, 180f);
        background.sprite = LoadUiSprite(NitroOpacitySpritePath);
        background.type = Image.Type.Simple;
        // Both artwork layers use the same rect and aspect, so they align exactly.
        background.preserveAspect = true;
        background.color = Color.white;
        background.raycastTarget = false;

        Transform existingMask = backgroundRect.Find("Nitro Fill Mask");
        Transform maskedFill = existingMask != null ? existingMask.Find("Nitro Fill") : null;
        if (maskedFill != null)
        {
            maskedFill.SetParent(backgroundRect, false);
        }

        RectTransform fillRect;
        Image fill = GetOrCreateImage(backgroundRect, "Nitro Fill", out fillRect);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.pivot = new Vector2(0.5f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fill.sprite = LoadUiSprite(NitroSpritePath);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        fill.fillAmount = 1f;
        fill.preserveAspect = true;
        fill.color = Color.white;
        fill.raycastTarget = false;

        NitroBarDisplay display = EnsureComponent<NitroBarDisplay>(background.gameObject);
        display.Configure(nitro, fill);
    }

    private static void EnsureNitroSpeedEffect(Transform parent, TankNitro nitro)
    {
        RectTransform effectRect;
        Image effectImage = GetOrCreateImage(parent, "Nitro Speed Effect", out effectRect);
        effectRect.anchorMin = Vector2.zero;
        effectRect.anchorMax = Vector2.one;
        effectRect.pivot = new Vector2(0.5f, 0.5f);
        effectRect.anchoredPosition = Vector2.zero;
        effectRect.offsetMin = Vector2.zero;
        effectRect.offsetMax = Vector2.zero;
        effectImage.raycastTarget = false;
        effectImage.transform.SetAsFirstSibling();

        NitroSpeedVignette effect = EnsureComponent<NitroSpeedVignette>(effectImage.gameObject);
        effect.Configure(nitro, effectImage);
    }

    private static void EnsureCombatRewardsUi(
        Transform parent,
        TankCombatRewards rewards,
        TankSpecialWeapon specialWeapon)
    {
        Transform oldCounter = parent.Find("Coin Counter");
        if (oldCounter != null) { oldCounter.gameObject.SetActive(false); Object.Destroy(oldCounter.gameObject); }
        Sprite ringSprite = CreateRingSprite();
        Sprite roundedSprite = CreateRoundedPanelSprite();
        Color ink = new Color(.095f, .14f, .14f, .96f);
        Color teal = new Color(.18f, .25f, .24f, 1f);
        Color cream = new Color(.98f, .95f, .87f, 1f);
        Color muted = new Color(.67f, .73f, .69f, 1f);
        Color gold = new Color(.96f, .71f, .30f, 1f);

        RectTransform chargeBackgroundRect;
        Image chargeBackground = GetOrCreateImage(parent, "Special Charge Background", out chargeBackgroundRect);
        chargeBackgroundRect.anchorMin = new Vector2(0.5f, 0f);
        chargeBackgroundRect.anchorMax = new Vector2(0.5f, 0f);
        chargeBackgroundRect.pivot = new Vector2(0.5f, 0f);
        chargeBackgroundRect.anchoredPosition = new Vector2(0f, 24f);
        chargeBackgroundRect.sizeDelta = new Vector2(268f, 104f);
        chargeBackground.sprite = roundedSprite;
        chargeBackground.type = Image.Type.Sliced;
        chargeBackground.color = ink;
        chargeBackground.raycastTarget = false;

        RectTransform iconRect;
        Image iconBackground = GetOrCreateImage(chargeBackgroundRect, "Ultimate Icon", out iconRect);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, .5f);
        iconRect.pivot = new Vector2(0f, .5f);
        iconRect.anchoredPosition = new Vector2(12f, 0f);
        iconRect.sizeDelta = new Vector2(76f, 76f);
        iconBackground.sprite = roundedSprite;
        iconBackground.type = Image.Type.Sliced;
        iconBackground.color = teal;
        iconBackground.raycastTarget = false;

        RectTransform numeralRect;
        Text numeral = GetOrCreateText(iconRect, "Ultimate Numeral", out numeralRect);
        numeralRect.anchorMin = Vector2.zero;
        numeralRect.anchorMax = Vector2.one;
        numeralRect.offsetMin = numeralRect.offsetMax = Vector2.zero;
        numeral.text = "I";
        numeral.alignment = TextAnchor.MiddleCenter;
        numeral.fontSize = 30;
        numeral.fontStyle = FontStyle.Bold;
        numeral.color = gold;
        numeral.raycastTarget = false;

        RectTransform nameRect;
        Text ultimateName = GetOrCreateText(chargeBackgroundRect, "Ultimate Name", out nameRect);
        nameRect.anchorMin = nameRect.anchorMax = new Vector2(0f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.anchoredPosition = new Vector2(100f, -12f);
        nameRect.sizeDelta = new Vector2(150f, 24f);
        ultimateName.text = "РАКЕТА";
        ultimateName.alignment = TextAnchor.MiddleLeft;
        ultimateName.fontSize = 18;
        ultimateName.fontStyle = FontStyle.Bold;
        ultimateName.resizeTextForBestFit = true;
        ultimateName.resizeTextMinSize = 11;
        ultimateName.resizeTextMaxSize = 18;
        ultimateName.color = cream;
        ultimateName.raycastTarget = false;

        RectTransform statusRect;
        Text status = GetOrCreateText(chargeBackgroundRect, "Charge Status", out statusRect);
        statusRect.anchorMin = statusRect.anchorMax = new Vector2(0f, 1f);
        statusRect.pivot = new Vector2(0f, 1f);
        statusRect.anchoredPosition = new Vector2(100f, -38f);
        statusRect.sizeDelta = new Vector2(150f, 20f);
        status.text = "ЗАРЯД  0%";
        status.alignment = TextAnchor.MiddleLeft;
        status.fontSize = 11;
        status.fontStyle = FontStyle.Bold;
        status.color = muted;
        status.raycastTarget = false;

        RectTransform trackRect;
        Image chargeTrack = GetOrCreateImage(chargeBackgroundRect, "Charge Track", out trackRect);
        trackRect.anchorMin = trackRect.anchorMax = new Vector2(0f, 1f);
        trackRect.pivot = new Vector2(0f, 1f);
        trackRect.anchoredPosition = new Vector2(100f, -68f);
        trackRect.sizeDelta = new Vector2(110f, 16f);
        chargeTrack.sprite = roundedSprite;
        chargeTrack.type = Image.Type.Sliced;
        chargeTrack.color = new Color(.23f, .31f, .29f, 1f);
        chargeTrack.raycastTarget = false;

        Transform legacyFill = chargeBackgroundRect.Find("Special Charge Fill");
        if (legacyFill != null) legacyFill.SetParent(trackRect, false);
        RectTransform chargeFillRect;
        Image chargeFill = GetOrCreateImage(trackRect, "Special Charge Fill", out chargeFillRect);
        chargeFillRect.anchorMin = Vector2.zero;
        chargeFillRect.anchorMax = Vector2.one;
        chargeFillRect.offsetMin = Vector2.zero;
        chargeFillRect.offsetMax = Vector2.zero;
        chargeFill.sprite = roundedSprite;
        chargeFill.type = Image.Type.Filled;
        chargeFill.fillMethod = Image.FillMethod.Horizontal;
        chargeFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        chargeFill.color = gold;
        chargeFill.raycastTarget = false;

        Transform legacyHint = chargeBackgroundRect.Find("Q Hint");
        if (legacyHint != null) { legacyHint.gameObject.SetActive(false); Object.Destroy(legacyHint.gameObject); }
        RectTransform shortcutRect;
        Image shortcutBackground = GetOrCreateImage(chargeBackgroundRect, "Q Shortcut", out shortcutRect);
        shortcutRect.anchorMin = shortcutRect.anchorMax = new Vector2(1f, 0f);
        shortcutRect.pivot = new Vector2(1f, 0f);
        shortcutRect.anchoredPosition = new Vector2(-12f, 12f);
        shortcutRect.sizeDelta = new Vector2(36f, 32f);
        shortcutBackground.sprite = roundedSprite;
        shortcutBackground.type = Image.Type.Sliced;
        shortcutBackground.color = teal;
        shortcutBackground.raycastTarget = false;

        RectTransform shortcutLabelRect;
        Text shortcutLabel = GetOrCreateText(shortcutRect, "Label", out shortcutLabelRect);
        shortcutLabelRect.anchorMin = Vector2.zero;
        shortcutLabelRect.anchorMax = Vector2.one;
        shortcutLabelRect.offsetMin = shortcutLabelRect.offsetMax = Vector2.zero;
        shortcutLabel.text = "Q";
        shortcutLabel.alignment = TextAnchor.MiddleCenter;
        shortcutLabel.fontSize = 18;
        shortcutLabel.fontStyle = FontStyle.Bold;
        shortcutLabel.color = ink;
        shortcutLabel.raycastTarget = false;

        RectTransform markerRect;
        Image targetMarker = GetOrCreateImage(parent, "Special Target Marker", out markerRect);
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(96f, 96f);
        targetMarker.sprite = ringSprite;
        targetMarker.type = Image.Type.Simple;
        targetMarker.color = new Color(1f, 0.12f, 0.05f, 0.95f);
        targetMarker.raycastTarget = false;
        targetMarker.gameObject.SetActive(false);

        CombatRewardsDisplay display = EnsureComponent<CombatRewardsDisplay>(parent.gameObject);
        display.Configure(rewards, specialWeapon, null, chargeFill, targetMarker, numeral, ultimateName, status, shortcutBackground);
    }

    private static MainMenuController EnsureMainMenu(GameObject tank, Camera camera)
    {
        var old = GameObject.Find("Main Menu UI");
        if (old != null) { old.SetActive(false); Object.Destroy(old); }
        var root = new GameObject("Garage UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0;
        EnsureEventSystem();
        var panel = new GameObject("Garage Panel", typeof(RectTransform));
        var rect = panel.GetComponent<RectTransform>();
        rect.SetParent(root.transform, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var view = panel.AddComponent<GarageMenuView>();
        view.Build();
        currentMainMenu = root.AddComponent<MainMenuController>();
        currentMainMenu.Configure(view, camera, tank);
        return currentMainMenu;
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        StandaloneInputModule standaloneInput = eventSystem.GetComponent<StandaloneInputModule>();
        if (standaloneInput != null)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(standaloneInput);
            }
            else
            {
                Object.DestroyImmediate(standaloneInput);
            }
        }

        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
        {
            inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        if (inputModule.actionsAsset == null)
        {
            inputModule.AssignDefaultActions();
        }
    }

    private static GameObject EnsureGameOverPanel(Transform parent)
    {
        RectTransform panelRect;
        Image panelImage = GetOrCreateImage(parent, "Game Over Panel", out panelRect);
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelImage.color = new Color(0f, 0f, 0f, 0.58f);
        panelImage.gameObject.SetActive(false);
        return panelImage.gameObject;
    }

    private static Image EnsureGameplayCursor(Transform parent)
    {
        RectTransform cursorRect;
        Image cursorImage = GetOrCreateImage(parent, "Gameplay Cursor", out cursorRect);
        cursorRect.anchorMin = new Vector2(0.5f, 0.5f);
        cursorRect.anchorMax = new Vector2(0.5f, 0.5f);
        cursorRect.pivot = new Vector2(0.5f, 0.5f);
        cursorRect.anchoredPosition = Vector2.zero;
        cursorRect.sizeDelta = new Vector2(56f, 56f);

        cursorImage.sprite = LoadUiSprite(ScopeSpritePath);
        cursorImage.type = Image.Type.Simple;
        cursorImage.preserveAspect = true;
        cursorImage.color = Color.white;
        cursorImage.raycastTarget = false;

        RectTransform reloadRect;
        Image reloadImage = GetOrCreateImage(cursorRect, "Reload Fill", out reloadRect);
        reloadRect.anchorMin = Vector2.zero;
        reloadRect.anchorMax = Vector2.one;
        reloadRect.pivot = new Vector2(0.5f, 0.5f);
        reloadRect.anchoredPosition = Vector2.zero;
        reloadRect.offsetMin = Vector2.zero;
        reloadRect.offsetMax = Vector2.zero;
        reloadImage.sprite = cursorImage.sprite;
        reloadImage.type = Image.Type.Filled;
        reloadImage.fillMethod = Image.FillMethod.Radial360;
        reloadImage.fillOrigin = (int)Image.Origin360.Top;
        reloadImage.fillClockwise = true;
        reloadImage.fillAmount = 1f;
        reloadImage.preserveAspect = true;
        reloadImage.color = Color.white;
        reloadImage.raycastTarget = false;
        reloadImage.gameObject.SetActive(false);
        return cursorImage;
    }

    private static Image EnsureHitMarker(Transform parent, Canvas canvas)
    {
        RectTransform markerRect;
        Image markerImage = GetOrCreateImage(parent, "Hit Marker", out markerRect);
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.anchoredPosition = Vector2.zero;
        markerRect.sizeDelta = new Vector2(24f, 24f);
        markerRect.localScale = Vector3.one;

        markerImage.sprite = LoadUiSprite(HitMarkerSpritePath);
        markerImage.type = Image.Type.Simple;
        markerImage.preserveAspect = true;
        markerImage.color = Color.white;
        markerImage.raycastTarget = false;
        markerImage.gameObject.SetActive(false);

        HitMarkerDisplay markerDisplay = EnsureComponent<HitMarkerDisplay>(parent.gameObject);
        markerDisplay.Configure(markerImage, canvas);
        return markerImage;
    }

    private static Image EnsureEnemyMarkers(Transform parent, Canvas canvas)
    {
        RectTransform markerRect;
        Image markerImage = GetOrCreateImage(parent, "Enemy Marker Template", out markerRect);
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.anchoredPosition = Vector2.zero;
        markerRect.sizeDelta = new Vector2(44f, 44f);
        markerRect.localScale = Vector3.one;

        markerImage.sprite = LoadEnemyMarkerSprite();
        if (markerImage.sprite == null)
        {
            markerImage.sprite = CreateFallbackEnemyMarkerSprite();
        }
        markerImage.type = Image.Type.Simple;
        markerImage.preserveAspect = true;
        markerImage.color = Color.white;
        markerImage.raycastTarget = false;
        markerImage.gameObject.SetActive(false);

        EnemyScreenMarkerDisplay markerDisplay = EnsureComponent<EnemyScreenMarkerDisplay>(parent.gameObject);
        markerDisplay.Configure(markerImage, canvas);
        return markerImage;
    }

    private static Image EnsureDamageVignette(Transform parent)
    {
        RectTransform vignetteRect;
        Image vignetteImage = GetOrCreateImage(parent, "Player Damage Vignette", out vignetteRect);
        vignetteRect.anchorMin = Vector2.zero;
        vignetteRect.anchorMax = Vector2.one;
        vignetteRect.pivot = new Vector2(0.5f, 0.5f);
        vignetteRect.anchoredPosition = Vector2.zero;
        vignetteRect.offsetMin = Vector2.zero;
        vignetteRect.offsetMax = Vector2.zero;
        vignetteImage.raycastTarget = false;
        vignetteImage.enabled = false;
        vignetteImage.transform.SetAsFirstSibling();
        return vignetteImage;
    }

    private static EnemyWaveAnnouncement EnsureWaveAnnouncement(Transform parent)
    {
        RectTransform textRect;
        Text waveText = GetOrCreateText(parent, "Wave Announcement", out textRect);
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = new Vector2(0f, 112f);
        textRect.sizeDelta = new Vector2(520f, 92f);
        waveText.alignment = TextAnchor.MiddleCenter;
        waveText.fontSize = 54;
        waveText.fontStyle = FontStyle.Bold;
        waveText.color = Color.white;
        waveText.raycastTarget = false;

        Shadow shadow = waveText.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = waveText.gameObject.AddComponent<Shadow>();
        }

        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(3f, -3f);

        CanvasGroup canvasGroup = waveText.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = waveText.gameObject.AddComponent<CanvasGroup>();
        }

        EnemyWaveAnnouncement announcement = EnsureComponent<EnemyWaveAnnouncement>(waveText.gameObject);
        announcement.Configure(waveText, canvasGroup);
        return announcement;
    }

    private static Button EnsureRestartButton(Transform parent)
    {
        RectTransform buttonRect;
        Image buttonImage = GetOrCreateImage(parent, "Restart Button", out buttonRect);
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, -30f);
        buttonRect.sizeDelta = new Vector2(190f, 52f);
        buttonImage.color = new Color(0.12f, 0.62f, 0.15f, 1f);

        Button button = EnsureComponent<Button>(buttonImage.gameObject);

        RectTransform titleRect;
        Text title = GetOrCreateText(parent, "Game Over Text", out titleRect);
        titleRect.anchorMin = new Vector2(0.5f, 0.5f);
        titleRect.anchorMax = new Vector2(0.5f, 0.5f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.anchoredPosition = new Vector2(0f, 42f);
        titleRect.sizeDelta = new Vector2(280f, 40f);
        title.alignment = TextAnchor.MiddleCenter;
        EnsureComponent<LocalizedGameText>(title.gameObject).Configure("ПОРАЖЕНИЕ", "GAME OVER");
        title.fontSize = 28;
        title.color = Color.white;

        RectTransform buttonTextRect;
        Text buttonText = GetOrCreateText(buttonRect, "Text", out buttonTextRect);
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;
        buttonText.alignment = TextAnchor.MiddleCenter;
        EnsureComponent<LocalizedGameText>(buttonText.gameObject).Configure("ЗАНОВО", "RESTART");
        buttonText.fontSize = 24;
        buttonText.color = Color.white;

        return button;
    }

    private static Sprite LoadUiSprite(string assetPath)
    {
#if UNITY_EDITOR
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
        {
            return sprite;
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        if (texture != null)
        {
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        }
#endif
        Texture2D runtimeTexture = Resources.Load<Texture2D>(ToResourcesPath(assetPath));
        if (runtimeTexture == null)
        {
            return null;
        }

        return Sprite.Create(runtimeTexture, new Rect(0f, 0f, runtimeTexture.width, runtimeTexture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite LoadEnemyMarkerSprite()
    {
        Sprite sprite = LoadUiSprite(EnemyMarkerSpritePath);
        if (sprite != null)
        {
            return sprite;
        }

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("Enemy Marker t:Texture2D", new[] { "Assets/UI" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            sprite = LoadUiSprite(path);
            if (sprite != null)
            {
                return sprite;
            }
        }

        guids = AssetDatabase.FindAssets("Enemy_Marker t:Texture2D", new[] { "Assets/UI" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            sprite = LoadUiSprite(path);
            if (sprite != null)
            {
                return sprite;
            }
        }
#endif

        return null;
    }

    private static Sprite CreateFallbackEnemyMarkerSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(1f, 1f, 1f, 0f);
        Color white = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x / (float)(size - 1), y / (float)(size - 1));
                bool insideArrow = point.x > 0.18f
                    && Mathf.Abs(point.y - 0.5f) < Mathf.Lerp(0.08f, 0.36f, point.x);
                texture.SetPixel(x, y, insideArrow ? white : clear);
            }
        }

        texture.Apply();
        texture.name = "Fallback Enemy Marker";
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateRingSprite()
    {
        const int size = 128;
        const float outerRadius = 61f;
        const float innerRadius = 47f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Runtime Special Charge Ring";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float outerAlpha = Mathf.Clamp01(outerRadius - distance + 1f);
                float innerAlpha = Mathf.Clamp01(distance - innerRadius + 1f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, outerAlpha * innerAlpha));
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite CreateRoundedPanelSprite()
    {
        const int size = 48;
        const float radius = 11f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Runtime Rounded UI Panel",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[size * size];
        float center = (size - 1) * .5f;
        float straight = size * .5f - radius;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x - center) - straight, 0f);
                float dy = Mathf.Max(Mathf.Abs(y - center) - straight, 0f);
                float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12f, 12f, 12f, 12f));
        sprite.name = "Runtime Rounded UI Panel";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Image GetOrCreateImage(Transform parent, string objectName, out RectTransform rectTransform)
    {
        Transform existing = parent.Find(objectName);
        GameObject imageObject = existing != null ? existing.gameObject : new GameObject(objectName, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);

        rectTransform = imageObject.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogWarning($"{objectName} needs a RectTransform to be used as a health UI element.");
        }

        Image image = imageObject.GetComponent<Image>();
        if (image == null)
        {
            image = imageObject.AddComponent<Image>();
        }

        return image;
    }

    private static Text GetOrCreateText(Transform parent, string objectName, out RectTransform rectTransform)
    {
        Transform existing = parent.Find(objectName);
        GameObject textObject = existing != null ? existing.gameObject : new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        rectTransform = textObject.GetComponent<RectTransform>();
        Text text = textObject.GetComponent<Text>();
        if (text == null)
        {
            text = textObject.AddComponent<Text>();
        }

        if (text.font == null)
        {
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
            {
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
        }

        return text;
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    public static string TankAssetPath => TankPrefabPath;
    public static string SnowTankAssetPath => TankSnowPrefabPath;
    public static string EnemyTankAssetPath => TankEnemyPrefabPath;
    public static string MissileAssetPath => MissilePrefabPath;
    public static string BoxAssetPath => BoxPrefabPath;
}
