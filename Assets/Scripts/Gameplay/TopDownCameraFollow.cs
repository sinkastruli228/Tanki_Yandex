using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class TopDownCameraFollow : MonoBehaviour
{
    public static readonly Vector3 DefaultOffset = new Vector3(0f, 39f, -48f);
    public static readonly Vector3 DefaultLookOffset = new Vector3(0f, 0.2f, 9f);
    public static readonly Vector3 DefaultCloseOffset = new Vector3(0f, 10f, -14f);
    public static readonly Vector3 DefaultCloseLookOffset = new Vector3(0f, 1.25f, 4f);

    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 39f, -48f);
    [SerializeField] private Vector3 lookOffset = new Vector3(0f, 0.2f, 9f);
    [SerializeField] private Vector3 closeOffset = new Vector3(0f, 10f, -14f);
    [SerializeField] private Vector3 closeLookOffset = new Vector3(0f, 1.25f, 4f);
    [SerializeField] private float positionSmoothTime = 0.18f;
    [SerializeField] private float rotationSmoothSpeed = 12f;
    [SerializeField] private float orbitRotationSpeed = 90f;
    [Header("Shake")]
    [SerializeField] private float shotShakeIntensity = 0.08f;
    [SerializeField] private float hitShakeIntensity = 0.16f;
    [SerializeField] private float closeCameraShakeMultiplier = 0.333f;
    [SerializeField] private float shakeFrequency = 36f;
    [SerializeField] private float shakeDecay = 4.5f;

    private Vector3 velocity;
    private TankShooter shakeShooter;
    private TankHealth shakeHealth;
    private float shakePower;
    private float shakeSeed;
    private float orbitYaw;
    private bool closeCameraActive;
    private bool isFrozen;

    public void Configure(Transform followTarget)
    {
        Configure(followTarget, DefaultOffset, DefaultLookOffset);
    }

    public void Configure(Transform followTarget, Vector3 cameraOffset, Vector3 cameraLookOffset)
    {
        target = followTarget;
        offset = cameraOffset;
        lookOffset = cameraLookOffset;
        isFrozen = false;
        SnapToTarget();
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
            shakePower = 0f;
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

        SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null || isFrozen)
        {
            return;
        }

        HandleCameraInput();

        Vector3 desiredPosition = target.position + GetOrbitRotation() * GetActiveOffset();
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, positionSmoothTime);

        Quaternion desiredRotation = GetLookRotation();
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);

        ApplyShake();
    }

    private void OnValidate()
    {
        positionSmoothTime = Mathf.Max(0.01f, positionSmoothTime);
        rotationSmoothSpeed = Mathf.Max(0f, rotationSmoothSpeed);
        orbitRotationSpeed = Mathf.Max(0f, orbitRotationSpeed);
        shotShakeIntensity = Mathf.Max(0f, shotShakeIntensity);
        hitShakeIntensity = Mathf.Max(0f, hitShakeIntensity);
        closeCameraShakeMultiplier = Mathf.Max(0f, closeCameraShakeMultiplier);
        shakeFrequency = Mathf.Max(0.01f, shakeFrequency);
        shakeDecay = Mathf.Max(0.01f, shakeDecay);
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
    }

    private Quaternion GetLookRotation()
    {
        Vector3 lookDirection = target.position + GetOrbitRotation() * GetActiveLookOffset() - transform.position;
        return Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    private void HandleCameraInput()
    {
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
        AddShake(shotShakeIntensity);
    }

    private void AddHitShake(TankHealth health, int damage)
    {
        AddShake(hitShakeIntensity);
    }

    private void AddShake(float intensity)
    {
        if (isFrozen)
        {
            return;
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

        float time = Time.time * shakeFrequency + shakeSeed;
        float activeShakePower = closeCameraActive ? shakePower * closeCameraShakeMultiplier : shakePower;
        Vector3 shakeOffset = new Vector3(
            Mathf.PerlinNoise(time, 0.13f) - 0.5f,
            Mathf.PerlinNoise(0.37f, time) - 0.5f,
            Mathf.PerlinNoise(time, time * 0.27f) - 0.5f) * activeShakePower;

        transform.position += transform.right * shakeOffset.x + transform.up * shakeOffset.y + transform.forward * shakeOffset.z * 0.2f;

        float roll = (Mathf.PerlinNoise(time * 1.31f, 0.71f) - 0.5f) * activeShakePower * 0.7f;
        transform.rotation *= Quaternion.Euler(0f, 0f, roll);
        shakePower = Mathf.MoveTowards(shakePower, 0f, shakeDecay * Time.deltaTime);
    }
}
