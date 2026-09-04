using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class TankTurretAim : MonoBehaviour
{
    [SerializeField] private Transform turret;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private Vector3 localForwardAxis = Vector3.forward;
    [SerializeField] private float rotationSpeed = 420f;
    [SerializeField] private float mouseYawSensitivity = 0.65f;

    public Transform Turret => turret != null ? turret : transform;

    private float targetYaw;
    private bool targetYawInitialized;

    public void Configure(Transform turretTransform, Camera cameraOverride)
    {
        turret = turretTransform;
        aimCamera = cameraOverride;
        localForwardAxis = Vector3.forward;
    }

    public void ConfigureMouseSensitivity(float sensitivity)
    {
        mouseYawSensitivity = Mathf.Max(0.01f, sensitivity);
    }

    public void ConfigureAimSettings(float sensitivity, float newRotationSpeed)
    {
        ConfigureMouseSensitivity(sensitivity);
        rotationSpeed = Mathf.Max(0f, newRotationSpeed);
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

        targetTurret.rotation = TankPlaneMath.RotationLookingAlong(desiredDirection, localForwardAxis);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnValidate()
    {
        localForwardAxis = TankPlaneMath.SafeLocalForwardAxis(localForwardAxis);
        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        mouseYawSensitivity = Mathf.Max(0.01f, mouseYawSensitivity);
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
