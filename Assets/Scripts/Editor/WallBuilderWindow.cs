using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class WallBuilderWindow : EditorWindow
{
    private const string StraightPrefabPath = "Assets/Tiny Teacup Studio/Low Poly Desert Environment/Prefabs/Cliff_01.prefab";
    private const string LeftCornerPrefabPath = "Assets/Tiny Teacup Studio/Low Poly Desert Environment/Prefabs/CliffCorner_01.prefab";
    private const string RightCornerPrefabPath = "Assets/Tiny Teacup Studio/Low Poly Desert Environment/Prefabs/CliffCorner_02.prefab";

    private readonly List<Vector3> points = new List<Vector3>();

    private GameObject straightPrefab;
    private GameObject leftCornerPrefab;
    private GameObject rightCornerPrefab;
    private Transform parentRoot;
    private bool isDrawing;
    private bool snapToGrid = true;
    private bool addMeshColliders = true;
    private bool addCorners = true;
    private float gridSize = 1f;
    private float segmentLength = 4f;
    private float straightYawOffset = 90f;
    private float cornerYawOffset = 90f;
    private float groundY;

    [MenuItem("Tools/Wall Builder")]
    public static void Open()
    {
        GetWindow<WallBuilderWindow>("Wall Builder");
    }

    private void OnEnable()
    {
        LoadDefaultPrefabs();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Prefabs", EditorStyles.boldLabel);
        straightPrefab = (GameObject)EditorGUILayout.ObjectField("Straight", straightPrefab, typeof(GameObject), false);
        leftCornerPrefab = (GameObject)EditorGUILayout.ObjectField("Left Corner", leftCornerPrefab, typeof(GameObject), false);
        rightCornerPrefab = (GameObject)EditorGUILayout.ObjectField("Right Corner", rightCornerPrefab, typeof(GameObject), false);
        parentRoot = (Transform)EditorGUILayout.ObjectField("Parent", parentRoot, typeof(Transform), true);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Build Settings", EditorStyles.boldLabel);
        segmentLength = EditorGUILayout.FloatField("Segment Length", segmentLength);
        gridSize = EditorGUILayout.FloatField("Grid Size", gridSize);
        groundY = EditorGUILayout.FloatField("Fallback Ground Y", groundY);
        straightYawOffset = EditorGUILayout.FloatField("Straight Yaw Offset", straightYawOffset);
        cornerYawOffset = EditorGUILayout.FloatField("Corner Yaw Offset", cornerYawOffset);
        snapToGrid = EditorGUILayout.Toggle("Snap To Grid", snapToGrid);
        addCorners = EditorGUILayout.Toggle("Add Corners", addCorners);
        addMeshColliders = EditorGUILayout.Toggle("Add Mesh Colliders", addMeshColliders);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField($"Points: {points.Count}");
        EditorGUILayout.HelpBox("Scene View: Left Click adds a point. Backspace removes last point. Enter builds. Esc stops drawing.", MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = isDrawing ? new Color(1f, 0.72f, 0.28f) : Color.white;
        if (GUILayout.Button(isDrawing ? "Stop Drawing" : "Start Drawing"))
        {
            isDrawing = !isDrawing;
            SceneView.RepaintAll();
        }

        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("Build Walls"))
        {
            BuildWalls();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Undo Last Point"))
        {
            UndoLastPoint();
        }

        if (GUILayout.Button("Clear Points"))
        {
            points.Clear();
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Auto Length From Prefab"))
        {
            segmentLength = EstimatePrefabLength(straightPrefab);
        }

        if (GUILayout.Button("Reload Default Prefabs"))
        {
            LoadDefaultPrefabs();
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        DrawPoints();

        if (!isDrawing)
        {
            return;
        }

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        Event currentEvent = Event.current;

        if (currentEvent.type == EventType.KeyDown)
        {
            if (currentEvent.keyCode == KeyCode.Backspace)
            {
                UndoLastPoint();
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter)
            {
                BuildWalls();
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.Escape)
            {
                isDrawing = false;
                currentEvent.Use();
            }
        }

        if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0 || currentEvent.alt)
        {
            return;
        }

        if (TryGetMousePoint(currentEvent.mousePosition, out Vector3 point))
        {
            Undo.RecordObject(this, "Add Wall Point");
            points.Add(point);
            Repaint();
            SceneView.RepaintAll();
            currentEvent.Use();
        }
    }

    private void DrawPoints()
    {
        Handles.color = new Color(1f, 0.75f, 0.16f, 1f);
        for (int i = 0; i < points.Count; i++)
        {
            Handles.SphereHandleCap(0, points[i], Quaternion.identity, 0.7f, EventType.Repaint);
            Handles.Label(points[i] + Vector3.up * 0.7f, $"{i + 1}");

            if (i > 0)
            {
                Handles.DrawAAPolyLine(5f, points[i - 1], points[i]);
            }
        }
    }

    private bool TryGetMousePoint(Vector2 mousePosition, out Vector3 point)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 5000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            point = hit.point;
        }
        else
        {
            Plane plane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
            if (!plane.Raycast(ray, out float distance))
            {
                point = Vector3.zero;
                return false;
            }

            point = ray.GetPoint(distance);
        }

        if (snapToGrid && gridSize > 0.001f)
        {
            point.x = Mathf.Round(point.x / gridSize) * gridSize;
            point.z = Mathf.Round(point.z / gridSize) * gridSize;
        }

        return true;
    }

    private void BuildWalls()
    {
        if (straightPrefab == null)
        {
            Debug.LogWarning("Wall Builder: Straight prefab is not assigned.");
            return;
        }

        if (points.Count < 2)
        {
            Debug.LogWarning("Wall Builder: Add at least two points.");
            return;
        }

        Transform root = GetOrCreateRoot();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = 0; i < points.Count - 1; i++)
        {
            BuildSegment(points[i], points[i + 1], root);
        }

        if (addCorners)
        {
            BuildCorners(root);
        }

        Undo.CollapseUndoOperations(undoGroup);
    }

    private void BuildSegment(Vector3 start, Vector3 end, Transform root)
    {
        Vector3 delta = end - start;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance < 0.01f)
        {
            return;
        }

        Vector3 direction = delta / distance;
        float length = Mathf.Max(0.01f, segmentLength);
        int count = Mathf.Max(1, Mathf.RoundToInt(distance / length));
        float spacing = distance / count;

        for (int i = 0; i < count; i++)
        {
            Vector3 position = start + direction * (spacing * (i + 0.5f));
            position.y = GetGroundY(position);
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, straightYawOffset, 0f);
            GameObject wall = InstantiatePrefab(straightPrefab, root, position, rotation, "Wall Segment");
            ConfigureGeneratedWall(wall);
        }
    }

    private void BuildCorners(Transform root)
    {
        for (int i = 1; i < points.Count - 1; i++)
        {
            Vector3 previous = points[i] - points[i - 1];
            Vector3 next = points[i + 1] - points[i];
            previous.y = 0f;
            next.y = 0f;
            if (previous.sqrMagnitude < 0.001f || next.sqrMagnitude < 0.001f)
            {
                continue;
            }

            previous.Normalize();
            next.Normalize();
            float signedAngle = Vector3.SignedAngle(previous, next, Vector3.up);
            GameObject prefab = signedAngle >= 0f ? leftCornerPrefab : rightCornerPrefab;
            if (prefab == null)
            {
                continue;
            }

            Vector3 position = points[i];
            position.y = GetGroundY(position);
            Quaternion rotation = Quaternion.LookRotation(next, Vector3.up) * Quaternion.Euler(0f, cornerYawOffset, 0f);
            GameObject corner = InstantiatePrefab(prefab, root, position, rotation, "Wall Corner");
            ConfigureGeneratedWall(corner);
        }
    }

    private Transform GetOrCreateRoot()
    {
        if (parentRoot != null)
        {
            return parentRoot;
        }

        GameObject existing = GameObject.Find("Generated Walls");
        if (existing != null)
        {
            parentRoot = existing.transform;
            return parentRoot;
        }

        GameObject root = new GameObject("Generated Walls");
        Undo.RegisterCreatedObjectUndo(root, "Create Generated Walls Root");
        parentRoot = root.transform;
        return parentRoot;
    }

    private GameObject InstantiatePrefab(GameObject prefab, Transform root, Vector3 position, Quaternion rotation, string undoName)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (instance == null)
        {
            instance = Instantiate(prefab);
        }

        Undo.RegisterCreatedObjectUndo(instance, undoName);
        instance.transform.SetParent(root, true);
        instance.transform.SetPositionAndRotation(position, rotation);
        return instance;
    }

    private void ConfigureGeneratedWall(GameObject wall)
    {
        if (wall == null)
        {
            return;
        }

        Renderer[] renderers = wall.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        if (!addMeshColliders)
        {
            return;
        }

        MeshFilter[] meshFilters = wall.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null)
            {
                continue;
            }

            MeshCollider meshCollider = meshFilter.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = Undo.AddComponent<MeshCollider>(meshFilter.gameObject);
            }

            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = false;
            meshCollider.isTrigger = false;
        }
    }

    private float GetGroundY(Vector3 position)
    {
        Terrain terrain = Terrain.activeTerrain != null ? Terrain.activeTerrain : FindFirstObjectByType<Terrain>();
        if (terrain != null && terrain.terrainData != null)
        {
            return terrain.transform.position.y + terrain.SampleHeight(position);
        }

        return groundY;
    }

    private void UndoLastPoint()
    {
        if (points.Count == 0)
        {
            return;
        }

        points.RemoveAt(points.Count - 1);
        Repaint();
        SceneView.RepaintAll();
    }

    private void LoadDefaultPrefabs()
    {
        straightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StraightPrefabPath);
        leftCornerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LeftCornerPrefabPath);
        rightCornerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RightCornerPrefabPath);
        if (segmentLength <= 0.01f && straightPrefab != null)
        {
            segmentLength = EstimatePrefabLength(straightPrefab);
        }
    }

    private static float EstimatePrefabLength(GameObject prefab)
    {
        if (prefab == null)
        {
            return 4f;
        }

        GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (temp == null)
        {
            temp = Instantiate(prefab);
        }

        temp.hideFlags = HideFlags.HideAndDontSave;
        Renderer[] renderers = temp.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = default;
        foreach (Renderer renderer in renderers)
        {
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        DestroyImmediate(temp);
        if (!hasBounds)
        {
            return 4f;
        }

        return Mathf.Max(0.1f, Mathf.Max(bounds.size.x, bounds.size.z));
    }
}
