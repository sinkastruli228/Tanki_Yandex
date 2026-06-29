using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TankHealth : MonoBehaviour
{
    [SerializeField] private TankTeam team;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth = 100;
    [SerializeField] private bool destroyOnDeath = true;

    public event Action<TankHealth> Changed;
    public event Action<TankHealth, int> Damaged;
    public event Action<TankHealth> Died;

    public TankTeam Team => team;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsAlive => currentHealth > 0;
    public float Normalized => maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;

    private void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    public void Configure(TankTeam newTeam, int newMaxHealth, bool shouldDestroyOnDeath)
    {
        team = newTeam;
        maxHealth = Mathf.Max(1, newMaxHealth);
        currentHealth = maxHealth;
        destroyOnDeath = shouldDestroyOnDeath;
        Changed?.Invoke(this);
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || currentHealth <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);
        Damaged?.Invoke(this, damage);
        Changed?.Invoke(this);

        if (currentHealth == 0)
        {
            Died?.Invoke(this);
            if (destroyOnDeath)
            {
                Destroy(gameObject);
            }
        }
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || currentHealth <= 0 || currentHealth >= maxHealth)
        {
            return;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        Changed?.Invoke(this);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }
}
