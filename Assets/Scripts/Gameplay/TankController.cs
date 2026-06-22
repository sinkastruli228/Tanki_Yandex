using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class TankController : MonoBehaviour
{
    [Header("Model Axes")]
    [SerializeField] private Vector3 localForwardAxis = Vector3.forward;

    [Header("Movement")]
    [SerializeField] private float forwardSpeed = 24f;
    [SerializeField] private float reverseSpeed = 14f;
    [SerializeField] private float turnSpeed = 130f;
    [SerializeField] private float acceleration = 72f;
    [SerializeField] private float collisionSkin = 0.08f;
    [SerializeField] private float pushRadius = 1.15f;
    [SerializeField] private float pushForce = 18f;
    [SerializeField] private bool lockHeight = true;

    private Rigidbody body;
    private float planeY;
    private float currentSpeed;

    public Vector3 LocalForwardAxis => TankPlaneMath.SafeLocalForwardAxis(localForwardAxis);
    public Vector3 ForwardOnPlane => TankPlaneMath.Flatten(transform.TransformDirection(LocalForwardAxis));
    public float CurrentSpeed => currentSpeed;
    public float CurrentSpeedNormalized => Mathf.Clamp01(Mathf.Abs(currentSpeed) / Mathf.Max(0.01f, Mathf.Max(forwardSpeed, reverseSpeed)));

    private void Reset()
    {
        localForwardAxis = Vector3.forward;
        ConfigureRigidbody();
    }

    public void ConfigureModelAxis(Vector3 forwardAxis)
    {
        localForwardAxis = TankPlaneMath.SafeLocalForwardAxis(forwardAxis);
    }

    public void ConfigureMovement(float newForwardSpeed, float newReverseSpeed, float newAcceleration)
    {
        forwardSpeed = Mathf.Max(0f, newForwardSpeed);
        reverseSpeed = Mathf.Max(0f, newReverseSpeed);
        acceleration = Mathf.Max(0.01f, newAcceleration);
    }

    public void RefreshMovementPlane()
    {
        planeY = transform.position.y;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        planeY = transform.position.y;
        ConfigureRigidbody();
    }

    private void FixedUpdate()
    {
        float throttle = ReadThrottle();
        float turn = ReadTurn();
        if (throttle < -0.01f)
        {
            turn = -turn;
        }

        Quaternion proposedRotation = body.rotation * Quaternion.Euler(0f, turn * turnSpeed * Time.fixedDeltaTime, 0f);
        Quaternion nextRotation = WouldOverlapStaticWall(body.position, proposedRotation) ? body.rotation : proposedRotation;
        body.MoveRotation(nextRotation);

        float targetSpeed = throttle >= 0f ? throttle * forwardSpeed : throttle * reverseSpeed;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);

        Vector3 movementDirection = TankPlaneMath.Flatten(nextRotation * LocalForwardAxis);
        Vector3 movement = movementDirection * (currentSpeed * Time.fixedDeltaTime);
        movement = ClampMovementAgainstObstacles(movement);
        Vector3 nextPosition = body.position + movement;
        if (lockHeight)
        {
            nextPosition.y = planeY;
        }

        if (WouldOverlapStaticWall(nextPosition, nextRotation))
        {
            nextPosition = body.position;
            currentSpeed = 0f;
            movement = Vector3.zero;
        }

        body.MovePosition(nextPosition);
        PushDynamicBodies(movement);
    }

    private void OnValidate()
    {
        localForwardAxis = TankPlaneMath.SafeLocalForwardAxis(localForwardAxis);
        forwardSpeed = Mathf.Max(0f, forwardSpeed);
        reverseSpeed = Mathf.Max(0f, reverseSpeed);
        turnSpeed = Mathf.Max(0f, turnSpeed);
        acceleration = Mathf.Max(0.01f, acceleration);
        collisionSkin = Mathf.Max(0.01f, collisionSkin);
        pushRadius = Mathf.Max(0.05f, pushRadius);
        pushForce = Mathf.Max(0f, pushForce);
    }

    private Vector3 ClampMovementAgainstObstacles(Vector3 movement)
    {
        float distance = movement.magnitude;
        if (body == null || distance <= 0.001f)
        {
            return movement;
        }

        Vector3 direction = movement / distance;
        RaycastHit[] hits = body.SweepTestAll(direction, distance + collisionSkin, QueryTriggerInteraction.Ignore);
        float allowedDistance = distance;
        foreach (RaycastHit hit in hits)
        {
            Collider hitCollider = hit.collider;
            if (ShouldIgnoreMovementHit(hitCollider))
            {
                continue;
            }

            allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - collisionSkin));
        }

        if (allowedDistance < distance)
        {
            currentSpeed = 0f;
        }

        return direction * allowedDistance;
    }

    private bool WouldOverlapStaticWall(Vector3 rootPosition, Quaternion rootRotation)
    {
        Collider[] ownColliders = GetComponentsInChildren<Collider>();
        foreach (Collider ownCollider in ownColliders)
        {
            if (ownCollider == null || ownCollider.isTrigger)
            {
                continue;
            }

            Vector3 localColliderPosition = transform.InverseTransformPoint(ownCollider.transform.position);
            Quaternion localColliderRotation = Quaternion.Inverse(transform.rotation) * ownCollider.transform.rotation;
            Vector3 testColliderPosition = rootPosition + rootRotation * localColliderPosition;
            Quaternion testColliderRotation = rootRotation * localColliderRotation;

            Bounds bounds = ownCollider.bounds;
            Vector3 boundsOffset = ownCollider.bounds.center - ownCollider.transform.position;
            Vector3 testBoundsCenter = testColliderPosition + rootRotation * boundsOffset;
            Vector3 testExtents = bounds.extents + Vector3.one * (collisionSkin + 0.04f);
            Collider[] overlaps = Physics.OverlapBox(
                testBoundsCenter,
                testExtents,
                Quaternion.identity,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            foreach (Collider overlap in overlaps)
            {
                if (ShouldIgnoreMovementHit(overlap))
                {
                    continue;
                }

                if (Physics.ComputePenetration(
                    ownCollider,
                    testColliderPosition,
                    testColliderRotation,
                    overlap,
                    overlap.transform.position,
                    overlap.transform.rotation,
                    out _,
                    out float distance) && distance > 0.001f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool ShouldIgnoreMovementHit(Collider hitCollider)
    {
        if (hitCollider == null || hitCollider.isTrigger)
        {
            return true;
        }

        if (hitCollider is TerrainCollider)
        {
            return true;
        }

        if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
        {
            return true;
        }

        Rigidbody hitBody = hitCollider.attachedRigidbody;
        if (hitBody != null && !hitBody.isKinematic)
        {
            return true;
        }

        if (!IsStaticWallCollider(hitCollider))
        {
            return true;
        }

        return false;
    }

    private void PushDynamicBodies(Vector3 movement)
    {
        if (pushForce <= 0f || movement.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 pushDirection = TankPlaneMath.Flatten(movement);
        if (pushDirection.sqrMagnitude < 0.001f)
        {
            return;
        }

        pushDirection.Normalize();
        Vector3 pushCenter = body.position + pushDirection * pushRadius;
        Collider[] overlaps = Physics.OverlapSphere(pushCenter, pushRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider overlap in overlaps)
        {
            if (overlap == null || overlap.transform == transform || overlap.transform.IsChildOf(transform))
            {
                continue;
            }

            Rigidbody targetBody = overlap.attachedRigidbody;
            if (targetBody == null || targetBody.isKinematic)
            {
                continue;
            }

            Vector3 impulse = pushDirection * (pushForce * Mathf.Abs(currentSpeed) * Time.fixedDeltaTime);
            targetBody.AddForceAtPosition(impulse, overlap.ClosestPoint(pushCenter), ForceMode.Impulse);
        }
    }

    private static bool IsStaticWallCollider(Collider hitCollider)
    {
        Transform current = hitCollider.transform;
        while (current != null)
        {
            string objectName = current.name;
            if (objectName.Contains("wall", System.StringComparison.OrdinalIgnoreCase)
                || objectName.Contains("stolb", System.StringComparison.OrdinalIgnoreCase)
                || objectName.Contains("walls", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void ConfigureRigidbody()
    {
        body = body != null ? body : GetComponent<Rigidbody>();
        if (body == null)
        {
            return;
        }

        body.useGravity = false;
        body.isKinematic = true;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private static float ReadThrottle()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return 0f;
        }

        float throttle = 0f;
        if (keyboard.wKey.isPressed)
        {
            throttle += 1f;
        }

        if (keyboard.sKey.isPressed)
        {
            throttle -= 1f;
        }

        return Mathf.Clamp(throttle, -1f, 1f);
    }

    private static float ReadTurn()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return 0f;
        }

        float turn = 0f;
        if (keyboard.dKey.isPressed)
        {
            turn += 1f;
        }

        if (keyboard.aKey.isPressed)
        {
            turn -= 1f;
        }

        return Mathf.Clamp(turn, -1f, 1f);
    }
}
