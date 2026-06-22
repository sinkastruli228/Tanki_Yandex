using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class TankiGameplayBootstrap
{
    private const string TankPrefabPath = "Assets/Models/Tank/Tank.prefab";
    private const string MissilePrefabPath = "Assets/Models/Missle/Missile.prefab";
    private const string BoxPrefabPath = "Assets/Models/Box/Box.prefab";
    private const string ScopeSpritePath = "Assets/UI/Scope.png";
    private const string HitMarkerSpritePath = "Assets/UI/Hit_Marker.png";
    private const float GroundY = 0f;
    private const float FloorSize = 960f;
    private const float FloorTileSize = 8f;
    private const float TankForwardSpeed = 28.8f;
    private const float TankReverseSpeed = 16.8f;
    private const float TankAcceleration = 86.4f;
    private const float ProjectileSpeed = 140.4f;
    private const float MuzzleHeightOffset = 1.425f;
    private const float EnemyAttackRange = 85f;
    private const float PhysicsBoxGroundClearance = 0.3f;
    private const int TankMaxHealth = 100;
    private const int ProjectileDamage = 25;
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void SetupSceneOnPlay()
    {
        GameObject tank = FindTankInScene();
        if (tank == null)
        {
            return;
        }

        ConfigureTank(tank, LoadMissilePrefab(), Camera.main, false);
    }

    public static void ConfigureTank(GameObject tank, GameObject missilePrefab, Camera camera, bool persistent)
    {
        if (tank == null)
        {
            return;
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
        EnsureSingleBodyMeshCollider(tank);
        ConfigureShadowCasters(tank);

        TankHealth playerHealth = EnsureComponent<TankHealth>(tank);
        playerHealth.Configure(TankTeam.Player, TankMaxHealth, false);

        Transform turret = FindChildRecursive(tank.transform, "Cylinder.002") ?? FindChildRecursive(tank.transform, "cylinder.002");
        turret = turret != null ? turret : tank.transform;

        TankTurretAim turretAim = EnsureComponent<TankTurretAim>(tank);
        turretAim.enabled = true;
        turretAim.Configure(turret, camera);

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
        shooter.ConfigureDamage(TankTeam.Player, ProjectileDamage);

        if (camera != null)
        {
            TopDownCameraFollow follow = EnsureComponent<TopDownCameraFollow>(camera.gameObject);
            follow.enabled = true;
            follow.Configure(tank.transform, TopDownCameraFollow.DefaultOffset, TopDownCameraFollow.DefaultLookOffset);
            follow.ConfigureShakeSources(shooter, playerHealth);
            camera.fieldOfView = 58f;
        }

        EnsureGridFloor();
        EnsureSceneLight();
        EnsurePhysicsBoxes(persistent);
        EnsureEnemies(missilePrefab, playerHealth, persistent);
        EnsurePlayerHealthBar(playerHealth);

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

    private static void EnsureEnemies(GameObject missilePrefab, TankHealth playerHealth, bool persistent)
    {
        GameObject tankPrefab = LoadTankPrefab();
        if (tankPrefab == null || playerHealth == null)
        {
            return;
        }

        for (int i = 0; i < DefaultEnemyPositions.Length; i++)
        {
            string enemyName = $"Enemy Tank {i + 1}";
            GameObject enemy = GameObject.Find(enemyName);
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

            Vector3 enemyPosition = DefaultEnemyPositions[i];
            enemyPosition.y = GetGroundY(enemyPosition);
            enemy.transform.SetPositionAndRotation(enemyPosition, Quaternion.identity);
            ConfigureEnemy(enemy, missilePrefab, playerHealth);

#if UNITY_EDITOR
            if (persistent)
            {
                EditorUtility.SetDirty(enemy);
            }
#endif
        }
    }

    private static void ConfigureEnemy(GameObject enemy, GameObject missilePrefab, TankHealth playerHealth)
    {
        TankController controller = enemy.GetComponent<TankController>();
        if (controller != null)
        {
            controller.enabled = false;
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
        EnsureSingleBodyMeshCollider(enemy);
        ConfigureShadowCasters(enemy);

        TankHealth enemyHealth = EnsureComponent<TankHealth>(enemy);
        enemyHealth.Configure(TankTeam.Enemy, TankMaxHealth, true);

        Transform turret = FindChildRecursive(enemy.transform, "Cylinder.002") ?? FindChildRecursive(enemy.transform, "cylinder.002");
        turret = turret != null ? turret : enemy.transform;

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
        enemyTank.Configure(playerHealth, turret, muzzlePoint, missilePrefab, ProjectileSpeed, ProjectileDamage, EnemyAttackRange, DefaultForwardAxis);
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

    private static float GetGroundY(Vector3 position)
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
        Transform turret = FindChildRecursive(tank.transform, "Cylinder.002") ?? FindChildRecursive(tank.transform, "cylinder.002");
        Transform colliderTarget = FindChildRecursive(tank.transform, "Cylinder") ?? FindChildRecursive(tank.transform, "cylinder");
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
        muzzlePoint.localPosition = DefaultForwardAxis * EstimateMuzzleDistance(turret, DefaultForwardAxis)
            + Vector3.up * MuzzleHeightOffset;
        muzzlePoint.localRotation = Quaternion.identity;
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
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(MissilePrefabPath);
#else
        return null;
#endif
    }

    private static GameObject LoadTankPrefab()
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(TankPrefabPath);
#else
        return null;
#endif
    }

    private static GameObject LoadBoxPrefab()
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(BoxPrefabPath);
#else
        return null;
#endif
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
        backgroundRect.anchorMin = new Vector2(0.5f, 1f);
        backgroundRect.anchorMax = new Vector2(0.5f, 1f);
        backgroundRect.pivot = new Vector2(0.5f, 1f);
        backgroundRect.anchoredPosition = new Vector2(0f, -20f);
        backgroundRect.sizeDelta = new Vector2(360f, 24f);
        backgroundImage.color = new Color(0f, 0f, 0f, 0.65f);

        RectTransform fillRect;
        Image fillImage = GetOrCreateImage(backgroundRect, "Health Bar Fill", out fillRect);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);
        fillImage.type = Image.Type.Simple;
        fillImage.color = new Color(0.15f, 0.85f, 0.15f, 1f);

        GameObject gameOverPanel = EnsureGameOverPanel(root.transform);
        Button restartButton = EnsureRestartButton(gameOverPanel.transform);
        Image gameplayCursor = EnsureGameplayCursor(root.transform);
        EnsureHitMarker(root.transform, canvas);

        PlayerHealthBar healthBar = EnsureComponent<PlayerHealthBar>(root);
        healthBar.Configure(playerHealth, fillImage, gameOverPanel, restartButton, gameplayCursor);
        return root;
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
        title.text = "GAME OVER";
        title.fontSize = 28;
        title.color = Color.white;

        RectTransform buttonTextRect;
        Text buttonText = GetOrCreateText(buttonRect, "Text", out buttonTextRect);
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.text = "Restart";
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
        return null;
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
    public static string MissileAssetPath => MissilePrefabPath;
    public static string BoxAssetPath => BoxPrefabPath;
}
