using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class TopDownCameraFollow : MonoBehaviour
{
    public static readonly Vector3 DefaultOffset = new Vector3(0f, 39f, -48f);
    public static readonly Vector3 DefaultLookOffset = new Vector3(0f, 0.2f, 18f);
    public static readonly Vector3 DefaultCloseOffset = new Vector3(0f, 10f, -14f);
    public static readonly Vector3 DefaultCloseLookOffset = new Vector3(0f, 1.25f, 7f);

    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 39f, -48f);
    [SerializeField] private Vector3 lookOffset = new Vector3(0f, 0.2f, 9f);
    [SerializeField] private Vector3 closeOffset = new Vector3(0f, 10f, -14f);
    [SerializeField] private Vector3 closeLookOffset = new Vector3(0f, 1.25f, 4f);
    [SerializeField] private float positionSmoothTime = 0.18f;
    [SerializeField] private float rotationSmoothSpeed = 12f;
    [SerializeField] private float orbitRotationSpeed = 90f;
    [SerializeField] private float turretOrbitSmoothTime = 0.45f;
    [Header("Turret Camera")]
    [SerializeField] private Vector3 turretCameraLocalOffset = new Vector3(0.8f, 4.5f, -1.2f);
    [SerializeField] private float turretCameraTransitionTime = 0.38f;
    [SerializeField] private float turretCameraRotationSmoothSpeed = 9f;
    [Header("Cursor Follow")]
    [SerializeField] private float cursorFollowDistance = 7f;
    [Header("Shake")]
    [SerializeField] private float shotShakeIntensity = 0.08f;
    [SerializeField] private float hitShakeIntensity = 0.16f;
    [SerializeField] private float closeCameraShakeMultiplier = 0.333f;
    [SerializeField] private float shakeFrequency = 36f;
    [SerializeField] private float shakeDecay = 4.5f;
    [SerializeField] private float explosionShakeDecayMultiplier = 0.32f;

    private Vector3 velocity;
    private TankShooter shakeShooter;
    private TankHealth shakeHealth;
    private float shakePower;
    private float activeShakeDecayMultiplier = 1f;
    private float shakeSeed;
    private float orbitYaw;
    private float orbitYawVelocity;
    private Transform aimTurret;
    private Camera turretCamera;
    private bool turretCameraActive;
    private bool rightButtonWasHeld;
    private float turretCameraActivatedAt;
    private Vector3 turretTransitionStartPosition;
    private Quaternion turretTransitionStartRotation;
    private bool closeCameraActive;
    private bool isFrozen;
    private bool hasConfiguredPose;
    private Vector3 frozenPosition;
    private Quaternion frozenRotation;

    public void Configure(Transform followTarget)
    {
        Configure(followTarget, DefaultOffset, DefaultLookOffset);
    }

    public void Configure(Transform followTarget, Vector3 cameraOffset, Vector3 cameraLookOffset)
    {
        hasConfiguredPose = true;
        target = followTarget;
        offset = cameraOffset;
        lookOffset = cameraLookOffset;
        isFrozen = false;
        CacheAimTurret();
        SnapToTarget();
    }

    public void ConfigureTurretCamera(Vector3 localOffset)
    {
        turretCameraLocalOffset = localOffset;
        turretCamera = null;
        CacheAimTurret();
    }

    public void ConfigureShakeSources(TankShooter playerShooter, TankHealth playerHealth)
    {
        UnsubscribeShakeSources();

        shakeShooter = playerShooter;
        shakeHealth = playerHealth;

        if (shakeShooter != null)
        {
            shakeShooter.Shot += AddShotShake;
        }

        if (shakeHealth != null)
        {
            shakeHealth.Damaged += AddHitShake;
        }
    }

    public void SetFrozen(bool frozen)
    {
        isFrozen = frozen;
        velocity = Vector3.zero;
        if (isFrozen)
        {
            frozenPosition = transform.position;
            frozenRotation = transform.rotation;
        }
    }

    public void AddExplosionShake(float intensity)
    {
        AddShake(intensity, true, explosionShakeDecayMultiplier);
    }

    public static void ShakeAllExplosions(float intensity)
    {
        TopDownCameraFollow[] cameras = FindObjectsByType<TopDownCameraFollow>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (TopDownCameraFollow cameraFollow in cameras)
        {
            cameraFollow.AddExplosionShake(intensity);
        }

        if (cameras.Length > 0 || Camera.main == null)
        {
            return;
        }

        TopDownCameraFollow mainCameraFollow = Camera.main.GetComponent<TopDownCameraFollow>();
        if (mainCameraFollow != null)
        {
            mainCameraFollow.AddExplosionShake(intensity);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeShakeSources();
    }

    private void Start()
    {
        if (target == null)
        {
            TankController tank = FindFirstObjectByType<TankController>();
            target = tank != null ? tank.transform : null;
        }

        CacheAimTurret();

        if (!hasConfiguredPose) SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        if (isFrozen)
        {
            transform.position = frozenPosition;
            transform.rotation = frozenRotation;
            ApplyShake();
            return;
        }

        HandleCameraInput();

        if (turretCameraActive && turretCamera != null)
        {
            UpdateTurretCamera();
        }
        else
        {
            UpdateOrbitCamera();
        }

        if (!turretCameraActive)
        {
            ApplyShake();
        }
    }

    private void OnPreCull()
    {
        if (turretCameraActive && turretCamera != null && aimTurret != null)
        {
            UpdateTurretCamera();
        }
    }

    private void Update()
    {
        if (isFrozen || target == null)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        bool rightButtonHeld = mouse != null && mouse.rightButton.isPressed;
        if (!rightButtonHeld || rightButtonWasHeld)
        {
            rightButtonWasHeld = rightButtonHeld;
            return;
        }

        CacheAimTurret();
        turretCameraActive = !turretCameraActive && turretCamera != null;
        turretCameraActivatedAt = Time.unscaledTime;
        turretTransitionStartPosition = transform.position;
        turretTransitionStartRotation = transform.rotation;
        velocity = Vector3.zero;
        orbitYawVelocity = 0f;
        rightButtonWasHeld = true;
    }

    private void OnValidate()
    {
        positionSmoothTime = Mathf.Max(0.01f, positionSmoothTime);
        rotationSmoothSpeed = Mathf.Max(0f, rotationSmoothSpeed);
        orbitRotationSpeed = Mathf.Max(0f, orbitRotationSpeed);
        turretOrbitSmoothTime = Mathf.Max(0.01f, turretOrbitSmoothTime);
        turretCameraTransitionTime = Mathf.Max(0.01f, turretCameraTransitionTime);
        turretCameraRotationSmoothSpeed = Mathf.Max(0f, turretCameraRotationSmoothSpeed);
        cursorFollowDistance = Mathf.Max(0f, cursorFollowDistance);
        shotShakeIntensity = Mathf.Max(0f, shotShakeIntensity);
        hitShakeIntensity = Mathf.Max(0f, hitShakeIntensity);
        closeCameraShakeMultiplier = Mathf.Max(0f, closeCameraShakeMultiplier);
        shakeFrequency = Mathf.Max(0.01f, shakeFrequency);
        shakeDecay = Mathf.Max(0.01f, shakeDecay);
        explosionShakeDecayMultiplier = Mathf.Max(0.01f, explosionShakeDecayMultiplier);
    }

    private void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        transform.position = target.position + GetOrbitRotation() * GetActiveOffset();
        transform.rotation = GetLookRotation();
        velocity = Vector3.zero;
        orbitYawVelocity = 0f;
    }

    private Quaternion GetLookRotation()
    {
        Vector3 lookDirection = target.position + GetCursorFollowOffset() + GetOrbitRotation() * GetActiveLookOffset() - transform.position;
        return Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    private void UpdateOrbitCamera()
    {
        Vector3 desiredPosition = target.position + GetCursorFollowOffset() + GetOrbitRotation() * GetActiveOffset();
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, positionSmoothTime);

        Quaternion desiredRotation = GetLookRotation();
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);
    }

    private void UpdateTurretCamera()
    {
        // The view is fixed over the turret in tank-local space, while its look direction follows the barrel.
        turretCamera.transform.rotation = aimTurret.rotation * Quaternion.Euler(20f, 0f, 0f);
        Vector3 desiredPosition = turretCamera.transform.position;
        Quaternion desiredRotation = turretCamera.transform.rotation;
        float transitionT = Mathf.Clamp01((Time.unscaledTime - turretCameraActivatedAt) / turretCameraTransitionTime);
        if (transitionT >= 1f)
        {
            transform.SetPositionAndRotation(desiredPosition, desiredRotation);
            return;
        }

        float easedT = transitionT * transitionT * (3f - 2f * transitionT);
        transform.SetPositionAndRotation(
            Vector3.Lerp(turretTransitionStartPosition, desiredPosition, easedT),
            Quaternion.Slerp(turretTransitionStartRotation, desiredRotation, easedT));
    }

    private Vector3 GetCursorFollowOffset()
    {
        if (PlayerHealthBar.GameplayInputBlocked) return Vector3.zero;
        Mouse mouse = Mouse.current;
        if (mouse == null || cursorFollowDistance <= 0f)
        {
            return Vector3.zero;
        }

        Ray ray = GetComponent<Camera>().ScreenPointToRay(mouse.position.ReadValue());
        Plane movementPlane = new Plane(Vector3.up, target.position);
        if (!movementPlane.Raycast(ray, out float enter))
        {
            return Vector3.zero;
        }

        Vector3 offset = ray.GetPoint(enter) - target.position;
        offset.y = 0f;
        return Vector3.ClampMagnitude(offset, cursorFollowDistance);
    }

    private void HandleCameraInput()
    {
        if (aimTurret == null)
        {
            CacheAimTurret();
        }

        if (aimTurret != null)
        {
            if (turretCameraActive)
            {
                return;
            }

            orbitYaw = Mathf.SmoothDampAngle(orbitYaw, aimTurret.eulerAngles.y, ref orbitYawVelocity, turretOrbitSmoothTime);
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        float input = 0f;
        if (keyboard.qKey.isPressed)
        {
            input -= 1f;
        }

        if (keyboard.eKey.isPressed)
        {
            input += 1f;
        }

        orbitYaw += input * orbitRotationSpeed * Time.deltaTime;

        if (keyboard.vKey.wasPressedThisFrame)
        {
            closeCameraActive = !closeCameraActive;
            velocity = Vector3.zero;
        }
    }

    private void CacheAimTurret()
    {
        if (target == null)
        {
            aimTurret = null;
            turretCamera = null;
            return;
        }

        TankTurretAim turretAim = target.GetComponent<TankTurretAim>();
        aimTurret = turretAim != null ? turretAim.Turret : null;
        if (turretCamera != null && turretCamera.transform.parent != aimTurret)
        {
            turretCamera = null;
        }
        EnsureTurretCamera();
    }

    private void EnsureTurretCamera()
    {
        if (aimTurret == null || turretCamera != null)
        {
            return;
        }

        Transform existing = aimTurret.Find("Turret Camera");
        if (existing != null)
        {
            turretCamera = existing.GetComponent<Camera>();
        }

        if (turretCamera == null)
        {
            GameObject cameraObject = new GameObject("Turret Camera");
            cameraObject.transform.SetParent(aimTurret, false);
            turretCamera = cameraObject.AddComponent<Camera>();
        }

        turretCamera.transform.localPosition = turretCameraLocalOffset;
        turretCamera.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
        Camera mainCamera = GetComponent<Camera>();
        turretCamera.fieldOfView = mainCamera != null ? mainCamera.fieldOfView : 58f;
        turretCamera.enabled = false;
    }

    private Quaternion GetOrbitRotation()
    {
        return Quaternion.Euler(0f, orbitYaw, 0f);
    }

    private Vector3 GetActiveOffset()
    {
        return closeCameraActive ? closeOffset : offset;
    }

    private Vector3 GetActiveLookOffset()
    {
        return closeCameraActive ? closeLookOffset : lookOffset;
    }

    private void AddShotShake()
    {
        AddShake(shotShakeIntensity, false, 1f);
    }

    private void AddHitShake(TankHealth health, int damage)
    {
        AddShake(hitShakeIntensity, false, 1f);
    }

    private void AddShake(float intensity, bool ignoreFrozen, float decayMultiplier)
    {
        if (isFrozen && !ignoreFrozen)
        {
            return;
        }

        if (intensity >= shakePower)
        {
            activeShakeDecayMultiplier = Mathf.Max(0.01f, decayMultiplier);
        }

        shakePower = Mathf.Max(shakePower, intensity);
        shakeSeed = UnityEngine.Random.value * 1000f;
    }

    private void UnsubscribeShakeSources()
    {
        if (shakeShooter != null)
        {
            shakeShooter.Shot -= AddShotShake;
        }

        if (shakeHealth != null)
        {
            shakeHealth.Damaged -= AddHitShake;
        }

        shakeShooter = null;
        shakeHealth = null;
    }

    private void ApplyShake()
    {
        if (shakePower <= 0.001f)
        {
            shakePower = 0f;
            return;
        }

        float time = Time.unscaledTime * shakeFrequency + shakeSeed;
        float activeShakePower = closeCameraActive ? shakePower * closeCameraShakeMultiplier : shakePower;
        Vector3 shakeOffset = new Vector3(
            Mathf.PerlinNoise(time, 0.13f) - 0.5f,
            Mathf.PerlinNoise(0.37f, time) - 0.5f,
            Mathf.PerlinNoise(time, time * 0.27f) - 0.5f) * activeShakePower;

        transform.position += transform.right * shakeOffset.x + transform.up * shakeOffset.y + transform.forward * shakeOffset.z * 0.2f;

        float roll = (Mathf.PerlinNoise(time * 1.31f, 0.71f) - 0.5f) * activeShakePower * 0.7f;
        transform.rotation *= Quaternion.Euler(0f, 0f, roll);
        shakePower = Mathf.MoveTowards(shakePower, 0f, shakeDecay * activeShakeDecayMultiplier * Time.unscaledDeltaTime);
        if (shakePower <= 0.001f)
        {
            activeShakeDecayMultiplier = 1f;
        }
    }
}
