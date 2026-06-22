using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class TankTurretAim : MonoBehaviour
{
    [SerializeField] private Transform turret;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Vector3 localForwardAxis = Vector3.forward;
    [SerializeField] private float rotationSpeed = 420f;
    [SerializeField] private float minRotationSpeed = 35f;
    [SerializeField] private float slowdownAngle = 35f;
    [SerializeField] private float stopAngle = 0.25f;

    public void Configure(Transform turretTransform, Camera cameraOverride)
    {
        turret = turretTransform;
        aimCamera = cameraOverride;
        localForwardAxis = Vector3.forward;
    }

    private void Reset()
    {
        localForwardAxis = Vector3.forward;
    }

    private void LateUpdate()
    {
        Transform targetTurret = turret != null ? turret : transform;
        Camera cameraToUse = aimCamera != null ? aimCamera : Camera.main;

        if (cameraToUse == null || !TryGetMousePointOnPlane(cameraToUse, targetTurret.position.y, out Vector3 mousePoint))
        {
            return;
        }

        Vector3 desiredDirection = mousePoint - targetTurret.position;
        desiredDirection.y = 0f;

        if (desiredDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = TankPlaneMath.RotationLookingAlong(desiredDirection, localForwardAxis);
        float angle = Quaternion.Angle(targetTurret.rotation, targetRotation);
        if (angle <= stopAngle)
        {
            targetTurret.rotation = targetRotation;
            return;
        }

        float slowdownT = Mathf.Clamp01(angle / slowdownAngle);
        float easedT = 1f - (1f - slowdownT) * (1f - slowdownT);
        float currentRotationSpeed = Mathf.Lerp(minRotationSpeed, rotationSpeed, easedT);
        targetTurret.rotation = Quaternion.RotateTowards(targetTurret.rotation, targetRotation, currentRotationSpeed * Time.deltaTime);
    }

    private void OnValidate()
    {
        localForwardAxis = TankPlaneMath.SafeLocalForwardAxis(localForwardAxis);
        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        minRotationSpeed = Mathf.Clamp(minRotationSpeed, 0f, rotationSpeed);
        slowdownAngle = Mathf.Max(0.01f, slowdownAngle);
        stopAngle = Mathf.Max(0f, stopAngle);
    }

    private static bool TryGetMousePointOnPlane(Camera cameraToUse, float planeY, out Vector3 point)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            point = default;
            return false;
        }

        Ray ray = cameraToUse.ScreenPointToRay(mouse.position.ReadValue());
        Plane plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));

        if (plane.Raycast(ray, out float enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }

        point = default;
        return false;
    }
}
