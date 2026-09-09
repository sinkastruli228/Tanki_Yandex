using UnityEngine;

/// <summary>
/// Central kill attribution point. Future damage sources only need to pass their
/// owning tank to TankHealth.TakeDamage to receive coins, ultimate charge and XP.
/// </summary>
public static class TankKillAttribution
{
    public static void Reward(GameObject instigator, TankHealth victim, bool critical)
    {
        if (instigator == null || victim == null || victim.Team != TankTeam.Enemy)
        {
            return;
        }

        TankCombatRewards rewards = instigator.GetComponent<TankCombatRewards>();
        if (rewards == null)
        {
            rewards = instigator.GetComponentInParent<TankCombatRewards>();
        }

        rewards?.RegisterKill(critical);
    }
}
