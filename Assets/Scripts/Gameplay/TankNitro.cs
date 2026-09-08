using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class TankNitro : MonoBehaviour
{
    [SerializeField] private TankController controller;
    [SerializeField] private float capacity = 100f;
    [SerializeField] private float drainPerSecond = 28f;
    [SerializeField] private float rechargePerSecond = 18f;
    [SerializeField] private float rechargeDelay = 1.5f;
    [SerializeField] private float speedMultiplier = 1.7f;

    private float amount;
    private float baseCapacity;
    private float battleCapacityMultiplier = 1f;
    private float lastUsedAt = -999f;

    public float Normalized => capacity <= 0f ? 0f : Mathf.Clamp01(amount / capacity);
    public float Capacity => capacity;
    public float BaseCapacity => baseCapacity > 0f ? baseCapacity : capacity;
    public float BattleCapacityMultiplier => battleCapacityMultiplier;
    public bool IsBoosting { get; private set; }

    public void Configure(TankController tankController)
    {
        controller = tankController;
        if (baseCapacity <= 0f) baseCapacity = capacity;
        amount = capacity;
        lastUsedAt = -999f;
        ApplySpeedMultiplier(false);
    }

    private void Awake()
    {
        if (baseCapacity <= 0f) baseCapacity = capacity;
        amount = capacity;
    }

    public void SetBattleCapacityMultiplier(float multiplier)
    {
        float previousCapacity = capacity;
        battleCapacityMultiplier = Mathf.Max(1f, multiplier);
        capacity = Mathf.Max(.01f, BaseCapacity * battleCapacityMultiplier);
        amount = Mathf.Min(capacity, amount + Mathf.Max(0f, capacity - previousCapacity));
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        bool wantsBoost = keyboard != null && keyboard.eKey.isPressed && controller != null && controller.enabled;
        IsBoosting = wantsBoost && amount > 0.001f;

        if (IsBoosting)
        {
            amount = Mathf.Max(0f, amount - drainPerSecond * Time.deltaTime);
            lastUsedAt = Time.time;
        }
        else if (Time.time >= lastUsedAt + rechargeDelay)
        {
            amount = Mathf.Min(capacity, amount + rechargePerSecond * Time.deltaTime);
        }

        ApplySpeedMultiplier(IsBoosting);
    }

    private void OnDisable()
    {
        IsBoosting = false;
        ApplySpeedMultiplier(false);
    }

    private void OnValidate()
    {
        capacity = Mathf.Max(0.01f, capacity);
        drainPerSecond = Mathf.Max(0f, drainPerSecond);
        rechargePerSecond = Mathf.Max(0f, rechargePerSecond);
        rechargeDelay = Mathf.Max(0f, rechargeDelay);
        speedMultiplier = Mathf.Max(1f, speedMultiplier);
        battleCapacityMultiplier = Mathf.Max(1f, battleCapacityMultiplier);
    }

    private void ApplySpeedMultiplier(bool boosted)
    {
        if (controller != null)
        {
            controller.SetSpeedMultiplier(boosted ? speedMultiplier : 1f);
        }
    }
}
