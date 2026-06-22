using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class TankSceneSetup
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string BoxPrefabPath = "Assets/Models/Box/Box.prefab";
    private const string ReadyBoxCreatedEditorPref = "TankiYandex.ReadyBoxCreated";
    private const string PhysicsBoxesReplacedEditorPref = "TankiYandex.PhysicsBoxesReplacedWithPrefab.v5";
    private const float BoxGroundClearance = 0.3f;

    [InitializeOnLoadMethod]
    private static void AutoCreateReadyBoxOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            string projectKey = $"{ReadyBoxCreatedEditorPref}.{Application.dataPath}";
            if (EditorPrefs.GetBool(projectKey, false))
            {
                return;
            }

            if (GameObject.Find("Level Box") != null)
            {
                EditorPrefs.SetBool(projectKey, true);
                return;
            }

            CreateReadyBox();
            EditorPrefs.SetBool(projectKey, true);
        };
    }

    [InitializeOnLoadMethod]
    private static void AutoReplacePhysicsBoxesOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            string projectKey = $"{PhysicsBoxesReplacedEditorPref}.{Application.dataPath}";
            if (EditorPrefs.GetBool(projectKey, false))
            {
                return;
            }

            ReplacePhysicsBoxesWithBoxPrefab();
            EditorPrefs.SetBool(projectKey, true);
        };
    }

    [MenuItem("Tools/Tanki/Setup Basic Tank Scene")]
    public static void SetupBasicTankScene()
    {
        GameObject tank = GameObject.Find("Tank");
        if (tank == null)
        {
            GameObject tankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TankiGameplayBootstrap.TankAssetPath);
            if (tankPrefab == null)
            {
                Debug.LogError($"Tank prefab was not found at {TankiGameplayBootstrap.TankAssetPath}");
                return;
            }

            tank = (GameObject)PrefabUtility.InstantiatePrefab(tankPrefab);
            tank.name = "Tank";
            tank.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Undo.RegisterCreatedObjectUndo(tank, "Create Tank");
        }

        GameObject missilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TankiGameplayBootstrap.MissileAssetPath);
        Camera camera = Camera.main;

        Undo.RegisterFullObjectHierarchyUndo(tank, "Setup Tank Mechanics");
        if (camera != null)
        {
            Undo.RegisterFullObjectHierarchyUndo(camera.gameObject, "Setup Tank Camera");
        }

        TankiGameplayBootstrap.ConfigureTank(tank, missilePrefab, camera, true);
        GameObject gridFloor = TankiGameplayBootstrap.EnsureGridFloor();

        if (camera != null)
        {
            camera.transform.position = tank.transform.position + TopDownCameraFollow.DefaultOffset;
            camera.transform.rotation = Quaternion.LookRotation(tank.transform.position + TopDownCameraFollow.DefaultLookOffset - camera.transform.position, Vector3.up);
            camera.fieldOfView = 58f;
        }

        EditorUtility.SetDirty(gridFloor);
        Selection.activeGameObject = tank;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Basic tank scene setup is ready: WASD moves the hull, mouse aims the turret, LMB fires.");
    }

    [MenuItem("Tools/Tanki/Create Ready Box")]
    public static void CreateReadyBox()
    {
        if (EditorSceneManager.GetActiveScene().path != SampleScenePath)
        {
            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        }

        GameObject sourceBox = FindSceneBoxTemplate();
        GameObject boxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoxPrefabPath);
        if (sourceBox == null && boxPrefab == null)
        {
            Debug.LogError($"Box prefab was not found at {BoxPrefabPath}");
            return;
        }

        BoxCollider sourceCollider = sourceBox != null ? sourceBox.GetComponent<BoxCollider>() : boxPrefab.GetComponent<BoxCollider>();
        GameObject box = CreateBoxFromTemplate(sourceBox, boxPrefab);

        box.name = GetUniqueBoxName();
        Undo.RegisterCreatedObjectUndo(box, "Create Ready Box");

        Vector3 position = GetBoxSpawnPosition();
        box.transform.position = position;
        ConfigureReadyBox(box);
        CopyBoxColliderSettings(sourceCollider, box);
        AlignBoxBottomToTerrain(box);

        Selection.activeGameObject = box;
        EditorUtility.SetDirty(box);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        string sourceName = sourceBox != null ? sourceBox.name : BoxPrefabPath;
        Debug.Log($"Ready box created from {sourceName}. Move it where you want, then tell me when to save the layout.");
    }

    [MenuItem("Tools/Tanki/Replace Physics Boxes With Box Prefab")]
    public static void ReplacePhysicsBoxesWithBoxPrefab()
    {
        if (EditorSceneManager.GetActiveScene().path != SampleScenePath)
        {
            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
        }

        GameObject sourceBox = FindSceneBoxTemplate();
        GameObject boxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoxPrefabPath);
        if (boxPrefab == null)
        {
            Debug.LogError($"Box prefab was not found at {BoxPrefabPath}");
            return;
        }

        BoxCollider sourceCollider = sourceBox != null ? sourceBox.GetComponent<BoxCollider>() : boxPrefab.GetComponent<BoxCollider>();

        int replaced = 0;
        for (int i = 1; i <= 6; i++)
        {
            string boxName = $"Physics Box {i}";
            GameObject oldBox = GameObject.Find(boxName);
            Vector3 position = oldBox != null ? oldBox.transform.position : new Vector3(i * 6f, 0f, 24f + i * 8f);
            Quaternion rotation = oldBox != null ? oldBox.transform.rotation : Quaternion.Euler(0f, i * 17f, 0f);

            if (oldBox != null)
            {
                Undo.DestroyObjectImmediate(oldBox);
            }

            GameObject newBox = CreateBoxFromTemplate(sourceBox, boxPrefab);

            newBox.name = boxName;
            Undo.RegisterCreatedObjectUndo(newBox, $"Create {boxName}");
            newBox.transform.SetPositionAndRotation(position, rotation);
            ConfigureReadyBox(newBox, false);
            CopyBoxColliderSettings(sourceCollider, newBox);
            AlignBoxBottomToTerrain(newBox);
            EditorUtility.SetDirty(newBox);
            replaced++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        string sourceName = sourceBox != null ? sourceBox.name : BoxPrefabPath;
        Debug.Log($"Replaced {replaced} physics boxes using {sourceName} as the collider source.");
    }

    private static void ConfigureReadyBox(GameObject box)
    {
        ConfigureReadyBox(box, true);
    }

    private static void ConfigureReadyBox(GameObject box, bool createColliderIfMissing)
    {
        EnsureBoxCollider(box, createColliderIfMissing);

        Rigidbody body = box.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = box.AddComponent<Rigidbody>();
        }

        body.mass = 90f;
        body.useGravity = true;
        body.isKinematic = false;
        body.linearDamping = 0.35f;
        body.angularDamping = 0.55f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (box.GetComponent<PhysicsCrateMarker>() == null)
        {
            box.AddComponent<PhysicsCrateMarker>();
        }

        Renderer[] renderers = box.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private static void EnsureBoxCollider(GameObject box, bool createIfMissing)
    {
        BoxCollider boxCollider = box.GetComponent<BoxCollider>();
        if (boxCollider != null)
        {
            boxCollider.isTrigger = false;
            return;
        }

        if (!createIfMissing)
        {
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

    private static void CopyBoxColliderSettings(BoxCollider source, GameObject target)
    {
        if (source == null || target == null)
        {
            return;
        }

        BoxCollider targetCollider = target.GetComponent<BoxCollider>();
        if (targetCollider == null)
        {
            targetCollider = target.AddComponent<BoxCollider>();
        }

        targetCollider.center = source.center;
        targetCollider.size = source.size;
        targetCollider.sharedMaterial = source.sharedMaterial;
        targetCollider.isTrigger = false;
    }

    private static Vector3 GetBoxSpawnPosition()
    {
        GameObject tank = GameObject.Find("Tank");
        Vector3 basePosition = tank != null ? tank.transform.position + tank.transform.forward * 14f : new Vector3(12f, 0f, 24f);
        basePosition.y = GetGroundY(basePosition);
        return basePosition;
    }

    private static float GetGroundY(Vector3 position)
    {
        Terrain terrain = Terrain.activeTerrain != null ? Terrain.activeTerrain : Object.FindFirstObjectByType<Terrain>();
        if (terrain != null && terrain.terrainData != null)
        {
            return terrain.transform.position.y + terrain.SampleHeight(position);
        }

        return 0f;
    }

    private static void AlignBoxBottomToTerrain(GameObject box)
    {
        if (!TryGetColliderBounds(box, out Bounds bounds) && !TryGetRendererBounds(box, out bounds))
        {
            return;
        }

        float groundY = GetGroundYForBounds(bounds);
        Vector3 position = box.transform.position;
        position.y += groundY + BoxGroundClearance - bounds.min.y;
        box.transform.position = position;

        Rigidbody body = box.GetComponent<Rigidbody>();
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

    private static GameObject FindSceneBoxTemplate()
    {
        string[] templateNames = { "Level Box", "Level Box (1)", "Box" };
        foreach (string templateName in templateNames)
        {
            GameObject template = GameObject.Find(templateName);
            BoxCollider boxCollider = template != null ? template.GetComponent<BoxCollider>() : null;
            if (boxCollider != null && !boxCollider.isTrigger)
            {
                return template;
            }
        }

        return null;
    }

    private static GameObject CreateBoxFromTemplate(GameObject sourceBox, GameObject boxPrefab)
    {
        if (sourceBox != null)
        {
            return Object.Instantiate(sourceBox);
        }

        GameObject box = (GameObject)PrefabUtility.InstantiatePrefab(boxPrefab);
        return box != null ? box : Object.Instantiate(boxPrefab);
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

    private static bool TryGetRendererBounds(GameObject root, out Bounds combinedBounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
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

    private static string GetUniqueBoxName()
    {
        const string baseName = "Level Box";
        if (GameObject.Find(baseName) == null)
        {
            return baseName;
        }

        int index = 2;
        while (GameObject.Find($"{baseName} {index}") != null)
        {
            index++;
        }

        return $"{baseName} {index}";
    }
}
