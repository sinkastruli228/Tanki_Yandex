using UnityEngine;

[DisallowMultipleComponent]
public sealed class TankShieldPlate : MonoBehaviour
{
    private TankTeam protectedTeam = TankTeam.Player;

    public void Configure(TankTeam team)
    {
        protectedTeam = team;
    }

    public bool Blocks(TankTeam projectileTeam)
    {
        return projectileTeam != protectedTeam;
    }
}
