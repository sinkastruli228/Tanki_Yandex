using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TankBombardmentUltimate : MonoBehaviour
{
    private const float MapWidth = 800f;
    private const float MapHeight = 600f;
    private const float BrushDiameter = 46f;
    private const float StampSpacing = 14f;
    private const int MaximumStamps = 165;
    private const int MaximumImpacts = 48;
    private const float ImpactInterval = .13f;
    private const float BlastRadius = 13.5f;
    private const int BlastDamage = 48;

    private static readonly Color Ink = new Color(.095f, .14f, .14f, .98f);
    private static readonly Color Teal = new Color(.18f, .25f, .24f, 1f);
    private static readonly Color Cream = new Color(.98f, .95f, .87f, 1f);
    private static readonly Color Muted = new Color(.67f, .73f, .69f, 1f);
    private static readonly Color Gold = new Color(.96f, .71f, .30f, 1f);
    private static readonly Color StrikeRed = new Color(.96f, .16f, .07f, .66f);

    [SerializeField] private TankCombatRewards rewards;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Camera gameplayCamera;

    private readonly List<Vector3> strokeWorldPoints = new List<Vector3>(MaximumStamps);
    private readonly List<GameObject> strokeVisuals = new List<GameObject>(MaximumStamps);
    private readonly List<RectTransform> enemyMarkers = new List<RectTransform>();
    private readonly HashSet<int> rewardedKills = new HashSet<int>();

    private Canvas plannerCanvas;
    private CanvasGroup plannerGroup;
    private RectTransform plannerPanel;
    private RectTransform mapViewport;
    private RectTransform strokeLayer;
    private RectTransform markerLayer;
    private RawImage mapImage;
    private Image inkFill;
    private Text inkCounter;
    private Text plannerTitle;
    private Text plannerHint;
    private Text inkTitle;
    private Text inkHint;
    private Text confirmLabel;
    private Text clearLabel;
    private Text cancelLabel;
    private Button confirmButton;
    private Camera mapCamera;
    private RenderTexture mapTexture;
    private Sprite roundedSprite;
    private Sprite circleSprite;
    private Bounds arenaBounds;
    private Terrain arenaTerrain;
    private TankController controller;
    private TankShooter shooter;
    private TankTurretAim turretAim;
    private TopDownCameraFollow cameraFollow;
    private bool movementWasLocked;
    private bool shooterWasEnabled;
    private bool aimWasEnabled;
    private bool cameraFollowWasEnabled;
    private bool inputWasBlocked;
    private bool controlsCaptured;
    private bool drawing;
    private Vector2 lastDrawLocal;
    private float nextMarkerRefresh;
    private float openingProgress;
    private Coroutine strikeRoutine;

    public bool IsPlanning { get; private set; }
    public bool IsActive { get; private set; }
    public int StrokePointCount => strokeWorldPoints.Count;
    public int EnemyMarkerCount { get; private set; }
    public int ResolvedImpactCount { get; private set; }
    public int LastStrikeImpactCount { get; private set; }
    public Bounds ArenaBounds => arenaBounds;
    public Camera MapCamera => mapCamera;
    public RenderTexture MapTexture => mapTexture;
    public GameObject PlannerRoot => plannerCanvas != null ? plannerCanvas.gameObject : null;

    public void Configure(TankCombatRewards combatRewards, GameObject shellPrefab, Camera mainCamera)
    {
        rewards = combatRewards;
        projectilePrefab = shellPrefab;
        gameplayCamera = mainCamera;
    }

    private void OnEnable()
    {
        GameLanguage.Changed += RefreshLocalizedCopy;
    }

    private void OnDisable()
    {
        GameLanguage.Changed -= RefreshLocalizedCopy;
        if (IsPlanning)
        {
            ClosePlanner(true);
        }
        if (strikeRoutine != null)
        {
            StopCoroutine(strikeRoutine);
            strikeRoutine = null;
        }
        IsActive = false;
    }

    private void Update()
    {
        if (!IsPlanning)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            CancelPlanning();
            return;
        }

        openingProgress = Mathf.MoveTowards(openingProgress, 1f, Time.unscaledDeltaTime / .24f);
        if (plannerGroup != null)
        {
            plannerGroup.alpha = openingProgress;
        }
        if (plannerPanel != null)
        {
            plannerPanel.localScale = Vector3.one * Mathf.LerpUnclamped(.9f, 1f, OutBack(openingProgress));
        }

        if (Time.unscaledTime >= nextMarkerRefresh)
        {
            RefreshEnemyMarkers();
            nextMarkerRefresh = Time.unscaledTime + .15f;
        }

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 5.5f) * .07f;
        foreach (RectTransform marker in enemyMarkers)
        {
            if (marker != null && marker.gameObject.activeSelf) marker.localScale = Vector3.one * pulse;
        }
    }

    public bool OpenPlanner()
    {
        if (IsPlanning || IsActive || rewards == null || !rewards.IsSpecialArmed)
        {
            return false;
        }

        ResolveArenaBounds();
        EnsurePlannerUi();
        ConfigureMapCamera();
        ClearStroke();
        RefreshLocalizedCopy();
        CapturePlanningControls();

        IsPlanning = true;
        openingProgress = 0f;
        plannerGroup.alpha = 0f;
        plannerPanel.localScale = Vector3.one * .9f;
        plannerCanvas.gameObject.SetActive(true);
        plannerCanvas.transform.SetAsLastSibling();
        nextMarkerRefresh = 0f;
        RefreshEnemyMarkers();
        return true;
    }

    public void CancelPlanning()
    {
        if (!IsPlanning)
        {
            return;
        }

        ClosePlanner(true);
    }

    public bool SubmitStrike()
    {
        if (!IsPlanning || strokeWorldPoints.Count == 0 || rewards == null)
        {
            return false;
        }

        List<Vector3> impactPoints = BuildImpactPoints();
        if (impactPoints.Count == 0 || !rewards.ConsumeArmedShot())
        {
            return false;
        }

        LastStrikeImpactCount = impactPoints.Count;
        ResolvedImpactCount = 0;
        rewardedKills.Clear();
        ClosePlanner(false);
        IsActive = true;
        strikeRoutine = StartCoroutine(RunBombardment(impactPoints));
        return true;
    }

    public void DrawTestStroke(Vector2 normalizedStart, Vector2 normalizedEnd, int steps = 8)
    {
        if (!IsPlanning)
        {
            return;
        }

        int count = Mathf.Max(2, steps);
        for (int i = 0; i < count; i++)
        {
            Vector2 normalized = Vector2.Lerp(normalizedStart, normalizedEnd, i / (float)(count - 1));
            Vector2 local = new Vector2((normalized.x - .5f) * MapWidth, (normalized.y - .5f) * MapHeight);
            AddStamp(local);
        }
        UpdateInkUi();
    }

    public Vector2 WorldToMapNormalized(Vector3 worldPosition)
    {
        return new Vector2(
            Mathf.InverseLerp(arenaBounds.min.x, arenaBounds.max.x, worldPosition.x),
            Mathf.InverseLerp(arenaBounds.min.z, arenaBounds.max.z, worldPosition.z));
    }

    public Vector3 MapNormalizedToWorld(Vector2 normalized)
    {
        normalized.x = Mathf.Clamp01(normalized.x);
        normalized.y = Mathf.Clamp01(normalized.y);
        Vector3 point = new Vector3(
            Mathf.Lerp(arenaBounds.min.x, arenaBounds.max.x, normalized.x),
            arenaBounds.center.y,
            Mathf.Lerp(arenaBounds.min.z, arenaBounds.max.z, normalized.y));
        return FindImpactSurface(point);
    }

    public void RefreshMarkersForTests()
    {
        RefreshEnemyMarkers();
    }

    public void ResolveImpact(Vector3 position)
    {
        ResolvedImpactCount++;
        ImpactExplosion.SpawnBombardment(position);
        TopDownCameraFollow.ShakeAllExplosions(1.4f);

        // Damage is measured across the ground plane. A shell can hit a roof, a
        // prop or uneven terrain, so a 3D overlap sphere could otherwise miss a
        // tank that is clearly inside the painted blast circle.
        TankHealth[] tanks = FindObjectsByType<TankHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (TankHealth health in tanks)
        {
            if (health == null || health.Team != TankTeam.Enemy || !health.IsAlive)
            {
                continue;
            }

            Vector3 targetPoint = TankSpecialWeapon.GetTargetPoint(health);
            float distance = Vector2.Distance(
                new Vector2(position.x, position.z),
                new Vector2(targetPoint.x, targetPoint.z));
            Collider targetCollider = health.GetComponentInChildren<Collider>();
            float targetRadius = targetCollider != null
                ? Mathf.Max(targetCollider.bounds.extents.x, targetCollider.bounds.extents.z)
                : 0f;
            float effectiveRadius = BlastRadius + targetRadius;
            if (distance > effectiveRadius)
            {
                continue;
            }

            float falloff = Mathf.InverseLerp(effectiveRadius, 0f, distance);
            int damage = Mathf.RoundToInt(Mathf.Lerp(BlastDamage * .45f, BlastDamage, falloff));
            health.TakeDamage(damage);
            bool fatal = !health.IsAlive;
            ProjectileMovement.NotifyTankDamaged(targetPoint, fatal);
            if (fatal && rewardedKills.Add(health.GetInstanceID()))
            {
                rewards?.RegisterKill();
            }
        }
    }

    internal void BeginDraw(Vector2 screenPosition, Camera eventCamera)
    {
        if (!IsPlanning || !TryGetMapLocal(screenPosition, eventCamera, out Vector2 local))
        {
            return;
        }

        drawing = true;
        lastDrawLocal = local;
        AddStamp(local);
        UpdateInkUi();
    }

    internal void ContinueDraw(Vector2 screenPosition, Camera eventCamera)
    {
        if (!drawing || !IsPlanning || !TryGetMapLocal(screenPosition, eventCamera, out Vector2 local))
        {
            return;
        }

        float distance = Vector2.Distance(lastDrawLocal, local);
        while (distance >= StampSpacing && strokeWorldPoints.Count < MaximumStamps)
        {
            lastDrawLocal = Vector2.MoveTowards(lastDrawLocal, local, StampSpacing);
            AddStamp(lastDrawLocal);
            distance = Vector2.Distance(lastDrawLocal, local);
        }
        UpdateInkUi();
    }

    internal void EndDraw()
    {
        drawing = false;
    }

    private bool TryGetMapLocal(Vector2 screenPosition, Camera eventCamera, out Vector2 local)
    {
        if (mapViewport == null
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(mapViewport, screenPosition, eventCamera, out local))
        {
            local = default;
            return false;
        }
        return mapViewport.rect.Contains(local);
    }

    private void AddStamp(Vector2 local)
    {
        if (strokeWorldPoints.Count >= MaximumStamps)
        {
            return;
        }

        Vector2 normalized = new Vector2(local.x / MapWidth + .5f, local.y / MapHeight + .5f);
        strokeWorldPoints.Add(MapNormalizedToWorld(normalized));

        RectTransform stampRect = CreateRect(strokeLayer, "Strike Brush " + strokeWorldPoints.Count, new Vector2(.5f, .5f), local, new Vector2(BrushDiameter, BrushDiameter), new Vector2(.5f, .5f));
        Image stamp = stampRect.gameObject.AddComponent<Image>();
        stamp.sprite = circleSprite;
        stamp.color = StrikeRed;
        stamp.raycastTarget = false;
        strokeVisuals.Add(stamp.gameObject);
    }

    private void ClearStroke()
    {
        drawing = false;
        strokeWorldPoints.Clear();
        foreach (GameObject visual in strokeVisuals)
        {
            if (visual != null)
            {
                visual.SetActive(false);
                Destroy(visual);
            }
        }
        strokeVisuals.Clear();
        UpdateInkUi();
    }

    private void UpdateInkUi()
    {
        float remaining = 1f - strokeWorldPoints.Count / (float)MaximumStamps;
        if (inkFill != null) inkFill.fillAmount = Mathf.Clamp01(remaining);
        if (inkCounter != null) inkCounter.text = Mathf.RoundToInt(remaining * 100f) + "%";
        if (confirmButton != null) confirmButton.interactable = strokeWorldPoints.Count > 0;
    }

    private List<Vector3> BuildImpactPoints()
    {
        List<Vector3> impacts = new List<Vector3>(MaximumImpacts);
        float brushWorldRadius = Mathf.Max(arenaBounds.size.x / MapWidth, arenaBounds.size.z / MapHeight) * BrushDiameter * .5f;
        float spacing = Mathf.Max(7.5f, brushWorldRadius * .72f);
        float spacingSqr = spacing * spacing;

        foreach (Vector3 strokePoint in strokeWorldPoints)
        {
            bool tooClose = false;
            foreach (Vector3 existing in impacts)
            {
                Vector2 delta = new Vector2(existing.x - strokePoint.x, existing.z - strokePoint.z);
                if (delta.sqrMagnitude < spacingSqr)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            Vector2 jitter = Random.insideUnitCircle * brushWorldRadius * .24f;
            Vector3 impact = strokePoint + new Vector3(jitter.x, 0f, jitter.y);
            impact.x = Mathf.Clamp(impact.x, arenaBounds.min.x, arenaBounds.max.x);
            impact.z = Mathf.Clamp(impact.z, arenaBounds.min.z, arenaBounds.max.z);
            impacts.Add(FindImpactSurface(impact));
            if (impacts.Count >= MaximumImpacts) break;
        }

        if (impacts.Count > 0)
        {
            Vector3 center = impacts[0];
            while (impacts.Count < 5)
            {
                float angle = impacts.Count * Mathf.PI * 2f / 5f;
                Vector3 point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Mathf.Max(5f, brushWorldRadius * .55f);
                point.x = Mathf.Clamp(point.x, arenaBounds.min.x, arenaBounds.max.x);
                point.z = Mathf.Clamp(point.z, arenaBounds.min.z, arenaBounds.max.z);
                impacts.Add(FindImpactSurface(point));
            }
        }

        return impacts;
    }

    private IEnumerator RunBombardment(List<Vector3> impactPoints)
    {
        foreach (Vector3 impactPoint in impactPoints)
        {
            SpawnShell(impactPoint);
            yield return new WaitForSeconds(ImpactInterval);
        }

        yield return new WaitForSeconds(1.6f);
        IsActive = false;
        strikeRoutine = null;
    }

    private void SpawnShell(Vector3 impactPoint)
    {
        Vector2 side = Random.insideUnitCircle.normalized * Random.Range(10f, 23f);
        Vector3 start = impactPoint + new Vector3(side.x, Random.Range(72f, 94f), side.y);
        GameObject shell;
        if (projectilePrefab != null)
        {
            shell = Instantiate(projectilePrefab, start, Quaternion.identity);
            shell.transform.localScale *= 4f;
        }
        else
        {
            shell = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            shell.transform.position = start;
            shell.transform.localScale = new Vector3(1.25f, 2.4f, 1.25f);
        }

        shell.name = "Bombardment Heavy Shell";
        Vector3 direction = impactPoint - start;
        if (direction.sqrMagnitude > .001f) shell.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        BombardmentShell flight = shell.GetComponent<BombardmentShell>();
        if (flight == null) flight = shell.AddComponent<BombardmentShell>();
        flight.Launch(this, impactPoint, Random.Range(.72f, .9f));
    }

    private void ClosePlanner(bool restoreCharge)
    {
        IsPlanning = false;
        drawing = false;
        if (plannerCanvas != null) plannerCanvas.gameObject.SetActive(false);
        if (mapCamera != null) mapCamera.enabled = false;
        RestorePlanningControls();
        if (restoreCharge) rewards?.CancelSpecialActivation();
    }

    private void CapturePlanningControls()
    {
        if (controlsCaptured) return;
        controller = GetComponent<TankController>();
        shooter = GetComponent<TankShooter>();
        turretAim = GetComponent<TankTurretAim>();
        cameraFollow = gameplayCamera != null ? gameplayCamera.GetComponent<TopDownCameraFollow>() : null;
        movementWasLocked = controller != null && controller.MovementLocked;
        shooterWasEnabled = shooter != null && shooter.enabled;
        aimWasEnabled = turretAim != null && turretAim.enabled;
        cameraFollowWasEnabled = cameraFollow != null && cameraFollow.enabled;
        inputWasBlocked = PlayerHealthBar.GameplayInputBlocked;
        controlsCaptured = true;

        controller?.SetMovementLocked(true);
        if (shooter != null) shooter.enabled = false;
        if (turretAim != null) turretAim.enabled = false;
        if (cameraFollow != null) cameraFollow.enabled = false;
        PlayerHealthBar.GameplayInputBlocked = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void RestorePlanningControls()
    {
        if (!controlsCaptured) return;
        if (controller != null) controller.SetMovementLocked(movementWasLocked);
        if (shooter != null) shooter.enabled = shooterWasEnabled;
        if (turretAim != null) turretAim.enabled = aimWasEnabled;
        if (cameraFollow != null) cameraFollow.enabled = cameraFollowWasEnabled;
        TankHealth health = GetComponent<TankHealth>();
        PlayerHealthBar.GameplayInputBlocked = health != null && !health.IsAlive ? true : inputWasBlocked;
        controlsCaptured = false;
    }

    private void ResolveArenaBounds()
    {
        arenaTerrain = Terrain.activeTerrain != null ? Terrain.activeTerrain : FindFirstObjectByType<Terrain>();
        if (arenaTerrain != null && arenaTerrain.terrainData != null)
        {
            Vector3 size = arenaTerrain.terrainData.size;
            arenaBounds = new Bounds(arenaTerrain.transform.position + size * .5f, size);
            return;
        }

        Renderer[] walls = GameObject.Find("Generated Walls")?.GetComponentsInChildren<Renderer>(true);
        if (walls != null && walls.Length > 0)
        {
            arenaBounds = walls[0].bounds;
            for (int i = 1; i < walls.Length; i++) arenaBounds.Encapsulate(walls[i].bounds);
            arenaBounds.Expand(new Vector3(-12f, 0f, -12f));
            return;
        }

        arenaBounds = new Bounds(transform.position, new Vector3(320f, 80f, 320f));
    }

    private Vector3 FindImpactSurface(Vector3 worldPoint)
    {
        float y;
        if (arenaTerrain != null && arenaTerrain.terrainData != null)
        {
            y = arenaTerrain.SampleHeight(worldPoint) + arenaTerrain.transform.position.y;
        }
        else
        {
            y = arenaBounds.min.y;
        }

        Vector3 origin = new Vector3(worldPoint.x, arenaBounds.max.y + 260f, worldPoint.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 600f, ~0, QueryTriggerInteraction.Ignore))
        {
            y = hit.point.y;
        }
        return new Vector3(worldPoint.x, y + .05f, worldPoint.z);
    }

    private void ConfigureMapCamera()
    {
        if (mapCamera == null)
        {
            GameObject cameraObject = new GameObject("Bombardment Map Camera");
            mapCamera = cameraObject.AddComponent<Camera>();
            mapCamera.orthographic = true;
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = new Color(.48f, .38f, .23f, 1f);
            mapCamera.allowHDR = false;
            mapCamera.allowMSAA = true;
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) mapCamera.cullingMask &= ~(1 << uiLayer);
        }
        if (mapTexture == null)
        {
            mapTexture = new RenderTexture(1024, 768, 24, RenderTextureFormat.Default)
            {
                name = "Bombardment Live Map",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            mapTexture.Create();
        }

        float aspect = 4f / 3f;
        mapCamera.aspect = aspect;
        mapCamera.orthographicSize = Mathf.Max(arenaBounds.extents.z * 1.035f, arenaBounds.extents.x * 1.035f / aspect);
        mapCamera.nearClipPlane = .3f;
        mapCamera.farClipPlane = Mathf.Max(500f, arenaBounds.size.y + 420f);
        mapCamera.transform.SetPositionAndRotation(
            new Vector3(arenaBounds.center.x, arenaBounds.max.y + 220f, arenaBounds.center.z),
            Quaternion.Euler(90f, 0f, 0f));
        mapCamera.targetTexture = mapTexture;
        mapCamera.enabled = true;
        if (mapImage != null) mapImage.texture = mapTexture;
    }

    private void EnsurePlannerUi()
    {
        if (plannerCanvas != null)
        {
            return;
        }

        roundedSprite = CreateRoundedSprite();
        circleSprite = CreateCircleSprite();
        GameObject root = new GameObject("Bombardment Planner UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        plannerCanvas = root.GetComponent<Canvas>();
        plannerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        plannerCanvas.sortingOrder = 320;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = .5f;
        plannerGroup = root.GetComponent<CanvasGroup>();

        RectTransform dim = CreateStretchRect(root.transform, "Tactical Dim");
        Image dimImage = dim.gameObject.AddComponent<Image>();
        dimImage.color = new Color(.025f, .045f, .043f, .9f);
        dimImage.raycastTarget = true;

        plannerPanel = CreateRect(root.transform, "Bombardment Console", new Vector2(.5f, .5f), Vector2.zero, new Vector2(1160f, 700f), new Vector2(.5f, .5f));
        AddPanel(plannerPanel, new Color(.095f, .14f, .14f, .995f));

        RectTransform headerIcon = CreateRect(plannerPanel, "Ultimate Badge", new Vector2(0f, 1f), new Vector2(20f, -16f), new Vector2(48f, 44f), new Vector2(0f, 1f));
        AddPanel(headerIcon, Gold);
        AddText(headerIcon, "Numeral", "III", 20, Ink, Vector2.zero, headerIcon.sizeDelta, TextAnchor.MiddleCenter, true, new Vector2(.5f, .5f));
        plannerTitle = AddText(plannerPanel, "Title", string.Empty, 24, Cream, new Vector2(82f, -14f), new Vector2(360f, 30f), TextAnchor.MiddleLeft, true);
        plannerHint = AddText(plannerPanel, "Hint", string.Empty, 13, Muted, new Vector2(82f, -40f), new Vector2(690f, 22f), TextAnchor.MiddleLeft);
        Text live = AddText(plannerPanel, "Live Badge", "LIVE  •  ORTHO", 12, Gold, new Vector2(949f, -24f), new Vector2(175f, 24f), TextAnchor.MiddleCenter, true);

        RectTransform mapFrame = CreateRect(plannerPanel, "Map Frame", new Vector2(0f, 1f), new Vector2(20f, -70f), new Vector2(820f, 620f), new Vector2(0f, 1f));
        AddPanel(mapFrame, Teal);
        mapViewport = CreateRect(mapFrame, "Map Viewport", new Vector2(.5f, .5f), Vector2.zero, new Vector2(MapWidth, MapHeight), new Vector2(.5f, .5f));
        mapImage = mapViewport.gameObject.AddComponent<RawImage>();
        mapImage.color = Color.white;
        mapImage.raycastTarget = false;

        strokeLayer = CreateStretchRect(mapViewport, "Strike Paint");
        markerLayer = CreateStretchRect(mapViewport, "Enemy Markers");
        RectTransform inputRect = CreateStretchRect(mapViewport, "Map Drawing Surface");
        Image inputImage = inputRect.gameObject.AddComponent<Image>();
        inputImage.color = new Color(1f, 1f, 1f, .001f);
        inputImage.raycastTarget = true;
        inputRect.gameObject.AddComponent<BombardmentMapInput>().Configure(this);

        RectTransform side = CreateRect(plannerPanel, "Route Status", new Vector2(0f, 1f), new Vector2(858f, -70f), new Vector2(282f, 620f), new Vector2(0f, 1f));
        AddPanel(side, new Color(.13f, .19f, .18f, 1f));
        inkTitle = AddText(side, "Ink Title", string.Empty, 16, Cream, new Vector2(22f, -20f), new Vector2(238f, 28f), TextAnchor.MiddleCenter, true);

        RectTransform barTrack = CreateRect(side, "Ink Track", new Vector2(.5f, 1f), new Vector2(0f, -65f), new Vector2(54f, 310f), new Vector2(.5f, 1f));
        AddPanel(barTrack, new Color(.095f, .14f, .14f, 1f));
        RectTransform fillRect = CreateStretchRect(barTrack, "Ink Fill");
        fillRect.offsetMin = new Vector2(8f, 8f);
        fillRect.offsetMax = new Vector2(-8f, -8f);
        inkFill = fillRect.gameObject.AddComponent<Image>();
        inkFill.sprite = roundedSprite;
        inkFill.type = Image.Type.Filled;
        inkFill.fillMethod = Image.FillMethod.Vertical;
        inkFill.fillOrigin = (int)Image.OriginVertical.Bottom;
        inkFill.color = Gold;
        inkFill.raycastTarget = false;

        inkCounter = AddText(side, "Ink Counter", "100%", 25, Gold, new Vector2(22f, -388f), new Vector2(238f, 34f), TextAnchor.MiddleCenter, true);
        inkHint = AddText(side, "Ink Hint", string.Empty, 12, Muted, new Vector2(24f, -429f), new Vector2(234f, 45f), TextAnchor.UpperCenter);

        confirmButton = AddButton(side, "Confirm Strike", string.Empty, new Vector2(22f, -490f), new Vector2(238f, 48f), Gold, Ink, () => SubmitStrike(), out confirmLabel);
        AddButton(side, "Clear Route", string.Empty, new Vector2(22f, -546f), new Vector2(114f, 42f), Teal, Cream, ClearStroke, out clearLabel);
        AddButton(side, "Cancel", string.Empty, new Vector2(146f, -546f), new Vector2(114f, 42f), Teal, Cream, CancelPlanning, out cancelLabel);
        root.SetActive(false);
        RefreshLocalizedCopy();
    }

    private void RefreshLocalizedCopy()
    {
        if (plannerTitle == null) return;
        plannerTitle.text = GameLanguage.Text("БОМБАРДИРОВКА", "BOMBARDMENT");
        plannerHint.text = GameLanguage.Text("Зажми ЛКМ и нарисуй зону удара", "Hold LMB and paint the strike zone");
        inkTitle.text = GameLanguage.Text("ЛИМИТ МАРШРУТА", "ROUTE LIMIT");
        inkHint.text = GameLanguage.Text("Красная полоса — зона падения снарядов", "The red trail marks the shell impact zone");
        confirmLabel.text = GameLanguage.Text("НАЧАТЬ УДАР", "CALL STRIKE");
        clearLabel.text = GameLanguage.Text("ОЧИСТИТЬ", "CLEAR");
        cancelLabel.text = GameLanguage.Text("ESC  НАЗАД", "ESC  BACK");
    }

    private void RefreshEnemyMarkers()
    {
        if (markerLayer == null) return;
        TankHealth[] tanks = FindObjectsByType<TankHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int markerIndex = 0;
        foreach (TankHealth tank in tanks)
        {
            if (tank == null || !tank.IsAlive || tank.Team != TankTeam.Enemy) continue;
            RectTransform marker = GetEnemyMarker(markerIndex++);
            marker.anchoredPosition = NormalizedToMapLocal(WorldToMapNormalized(tank.transform.position));
            marker.gameObject.SetActive(true);
        }
        for (int i = markerIndex; i < enemyMarkers.Count; i++) enemyMarkers[i].gameObject.SetActive(false);
        EnemyMarkerCount = markerIndex;
    }

    private RectTransform GetEnemyMarker(int index)
    {
        if (index < enemyMarkers.Count) return enemyMarkers[index];
        RectTransform marker = CreateRect(markerLayer, "Enemy Target " + (index + 1), new Vector2(.5f, .5f), Vector2.zero, new Vector2(25f, 25f), new Vector2(.5f, .5f));
        Image outer = marker.gameObject.AddComponent<Image>();
        outer.sprite = circleSprite;
        outer.color = new Color(1f, .08f, .025f, .96f);
        outer.raycastTarget = false;
        RectTransform center = CreateRect(marker, "Core", new Vector2(.5f, .5f), Vector2.zero, new Vector2(10f, 10f), new Vector2(.5f, .5f));
        Image centerImage = center.gameObject.AddComponent<Image>();
        centerImage.sprite = circleSprite;
        centerImage.color = Ink;
        centerImage.raycastTarget = false;
        enemyMarkers.Add(marker);
        return marker;
    }

    private static Vector2 NormalizedToMapLocal(Vector2 normalized)
    {
        return new Vector2((Mathf.Clamp01(normalized.x) - .5f) * MapWidth, (Mathf.Clamp01(normalized.y) - .5f) * MapHeight);
    }

    private void OnDestroy()
    {
        if (mapCamera != null) Destroy(mapCamera.gameObject);
        if (mapTexture != null)
        {
            mapTexture.Release();
            Destroy(mapTexture);
        }
        if (plannerCanvas != null) Destroy(plannerCanvas.gameObject);
    }

    private static float OutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float value = Mathf.Clamp01(t) - 1f;
        return 1f + c3 * value * value * value + c1 * value * value;
    }

    private Sprite CreateRoundedSprite()
    {
        const int size = 48;
        const float radius = 11f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Bombardment Rounded Panel",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Mathf.Max(Mathf.Abs(x - (size - 1) * .5f) - ((size - 1) * .5f - radius), 0f);
            float dy = Mathf.Max(Mathf.Abs(y - (size - 1) * .5f) - ((size - 1) * .5f - radius), 0f);
            float alpha = Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy));
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(12f, 12f, 12f, 12f));
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private Sprite CreateCircleSprite()
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Bombardment Circle",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Vector2 center = Vector2.one * (size - 1) * .5f;
        float radius = (size - 1) * .5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float alpha = Mathf.Clamp01(radius + .5f - Vector2.Distance(new Vector2(x, y), center));
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private Image AddPanel(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Text AddText(Transform parent, string objectName, string value, int size, Color color, Vector2 position, Vector2 bounds, TextAnchor alignment, bool bold = false, Vector2? anchor = null)
    {
        Vector2 usedAnchor = anchor ?? new Vector2(0f, 1f);
        RectTransform rect = CreateRect(parent, objectName, usedAnchor, position, bounds, usedAnchor);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private Button AddButton(Transform parent, string objectName, string value, Vector2 position, Vector2 size, Color backgroundColor, Color textColor, UnityEngine.Events.UnityAction action, out Text label)
    {
        RectTransform rect = CreateRect(parent, objectName, new Vector2(0f, 1f), position, size, new Vector2(0f, 1f));
        Image background = AddPanel(rect, backgroundColor);
        background.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(.86f, .86f, .86f, 1f);
        colors.disabledColor = new Color(.5f, .5f, .5f, .55f);
        colors.fadeDuration = .08f;
        button.colors = colors;
        button.onClick.AddListener(action);
        label = AddText(rect, "Label", value, 13, textColor, Vector2.zero, size, TextAnchor.MiddleCenter, true, new Vector2(.5f, .5f));
        return button;
    }

    private static RectTransform CreateRect(Transform parent, string objectName, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform CreateStretchRect(Transform parent, string objectName)
    {
        RectTransform rect = CreateRect(parent, objectName, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(.5f, .5f));
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }
}

public sealed class BombardmentMapInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private TankBombardmentUltimate owner;

    public void Configure(TankBombardmentUltimate bombardment)
    {
        owner = bombardment;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            owner?.BeginDraw(eventData.position, eventData.pressEventCamera);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            owner?.ContinueDraw(eventData.position, eventData.pressEventCamera);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            owner?.EndDraw();
        }
    }
}
