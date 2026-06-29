using UnityEngine;

[DisallowMultipleComponent]
public sealed class HealthPickup : MonoBehaviour
{
    [SerializeField] private int healAmount = 25;
    [SerializeField] private int expireAtWaveIndex = int.MaxValue;
    [SerializeField] private float pickupRadius = 4.8f;
    [SerializeField] private float bobHeight = 0.25f;
    [SerializeField] private float bobSpeed = 2.2f;
    [SerializeField] private float spinSpeed = 85f;

    private Vector3 basePosition;

    public int ExpireAtWaveIndex => expireAtWaveIndex;

    public void Configure(int amount, int waveToExpire)
    {
        healAmount = Mathf.Max(1, amount);
        expireAtWaveIndex = waveToExpire;
        pickupRadius = Mathf.Max(0.1f, pickupRadius);
        basePosition = transform.position;
    }

    private void Awake()
    {
        basePosition = transform.position;
    }

    private void Update()
    {
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = basePosition + Vector3.up * bob;
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        TryHealPlayerTank();
    }

    private void TryHealPlayerTank()
    {
        TankHealth[] tanks = FindObjectsByType<TankHealth>(FindObjectsSortMode.None);
        foreach (TankHealth tank in tanks)
        {
            if (tank == null || tank.Team != TankTeam.Player || !tank.IsAlive)
            {
                continue;
            }

            if (tank.CurrentHealth >= tank.MaxHealth)
            {
                continue;
            }

            Vector3 toTank = tank.transform.position - transform.position;
            toTank.y = 0f;
            if (toTank.sqrMagnitude > pickupRadius * pickupRadius)
            {
                continue;
            }

            tank.Heal(healAmount);
            Destroy(gameObject);
            return;
        }
    }

    private void OnValidate()
    {
        healAmount = Mathf.Max(1, healAmount);
        pickupRadius = Mathf.Max(0.1f, pickupRadius);
        bobHeight = Mathf.Max(0f, bobHeight);
        bobSpeed = Mathf.Max(0f, bobSpeed);
        spinSpeed = Mathf.Max(0f, spinSpeed);
    }
}
