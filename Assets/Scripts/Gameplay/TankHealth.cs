using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TankHealth : MonoBehaviour
{
    [SerializeField] private TankTeam team;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth = 100;
    [SerializeField] private bool destroyOnDeath = true;
    private bool damageBlocked;
    private int baseMaxHealth;

    public event Action<TankHealth> Changed;
    public event Action<TankHealth, int> Damaged;
    public event Action<TankHealth> Died;

    public TankTeam Team => team;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsAlive => currentHealth > 0;
    public bool IsDamageBlocked => damageBlocked;
    public float Normalized => maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
    public int BaseMaxHealth => baseMaxHealth > 0 ? baseMaxHealth : maxHealth;

    private void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        if (baseMaxHealth <= 0) baseMaxHealth = maxHealth;
    }

    public void Configure(TankTeam newTeam, int newMaxHealth, bool shouldDestroyOnDeath)
    {
        team = newTeam;
        maxHealth = Mathf.Max(1, newMaxHealth);
        baseMaxHealth = maxHealth;
        currentHealth = maxHealth;
        damageBlocked = false;
        destroyOnDeath = shouldDestroyOnDeath;
        Changed?.Invoke(this);
    }

    public void TakeDamage(int damage, bool critical = false)
    {
        if (damage <= 0 || currentHealth <= 0 || damageBlocked)
        {
            return;
        }

        int previousHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - damage);
        int appliedDamage = previousHealth - currentHealth;
        Damaged?.Invoke(this, appliedDamage);
        if (team == TankTeam.Enemy)
        {
            EnemyDamageNumberDisplay.Show(this, appliedDamage, critical);
        }
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

    public void SetDamageBlocked(bool blocked)
    {
        damageBlocked = blocked;
    }

    public void SetBattleMaxHealthMultiplier(float multiplier)
    {
        int previousMax = maxHealth;
        maxHealth = Mathf.Max(1, Mathf.RoundToInt(BaseMaxHealth * Mathf.Max(1f, multiplier)));
        currentHealth = Mathf.Min(maxHealth, currentHealth + Mathf.Max(0, maxHealth - previousMax));
        Changed?.Invoke(this);
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
