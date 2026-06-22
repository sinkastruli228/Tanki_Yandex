using UnityEngine;

public static class TankPlaneMath
{
    private const float MinDirectionSqrMagnitude = 0.0001f;

    public static Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value.sqrMagnitude > MinDirectionSqrMagnitude ? value.normalized : Vector3.forward;
    }

    public static Vector3 SafeLocalForwardAxis(Vector3 axis)
    {
        axis.y = 0f;
        return axis.sqrMagnitude > MinDirectionSqrMagnitude ? axis.normalized : Vector3.forward;
    }

    public static Quaternion RotationLookingAlong(Vector3 worldDirection, Vector3 localForwardAxis)
    {
        Vector3 flatDirection = Flatten(worldDirection);
        Vector3 flatLocalAxis = SafeLocalForwardAxis(localForwardAxis);

        Quaternion worldForwardRotation = Quaternion.LookRotation(flatDirection, Vector3.up);
        Quaternion localAxisCorrection = Quaternion.Inverse(Quaternion.LookRotation(flatLocalAxis, Vector3.up));
        return worldForwardRotation * localAxisCorrection;
    }
}
