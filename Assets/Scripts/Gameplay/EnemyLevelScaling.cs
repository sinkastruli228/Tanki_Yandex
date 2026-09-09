using UnityEngine;

/// <summary>
/// Keeps player-level scaling identical for enemies that are already alive and
/// enemies spawned later in the same battle.
/// </summary>
public static class EnemyLevelScaling
{
    public const float GrowthPerLevel = .15f;

    public static int PlayerLevel { get; private set; } = 1;
    public static float CurrentMultiplier => Mathf.Pow(1f + GrowthPerLevel, Mathf.Max(0, PlayerLevel - 1));

    public static void ResetForBattle()
    {
        PlayerLevel = 1;
        ApplyToActiveEnemies();
    }

    public static void SetPlayerLevel(int level)
    {
        PlayerLevel = Mathf.Max(1, level);
        ApplyToActiveEnemies();
    }

    public static void ApplyToEnemy(GameObject enemy)
    {
        if (enemy == null)
        {
            return;
        }

        TankHealth health = enemy.GetComponent<TankHealth>();
        if (health == null || health.Team != TankTeam.Enemy)
        {
            return;
        }

        health.SetMaxHealthMultiplierPreservingRatio(CurrentMultiplier);
        // Enemy movement keeps its configured archetype speed at every player level.
        enemy.GetComponent<TankController>()?.SetBattleSpeedMultiplier(1f);
        enemy.GetComponent<StaticEnemyTank>()?.SetLevelDamageMultiplier(CurrentMultiplier);
    }

    private static void ApplyToActiveEnemies()
    {
        TankHealth[] tanks = Object.FindObjectsByType<TankHealth>(FindObjectsInactive.Include);
        foreach (TankHealth tank in tanks)
        {
            if (tank != null && tank.Team == TankTeam.Enemy)
            {
                ApplyToEnemy(tank.gameObject);
            }
        }
    }
}
