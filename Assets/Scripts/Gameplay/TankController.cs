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
    [SerializeField] private bool lockHeight = true;

    private Rigidbody body;
    private float planeY;
    private float currentSpeed;

    public Vector3 LocalForwardAxis => TankPlaneMath.SafeLocalForwardAxis(localForwardAxis);
    public Vector3 ForwardOnPlane => TankPlaneMath.Flatten(transform.TransformDirection(LocalForwardAxis));

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

        Quaternion nextRotation = body.rotation * Quaternion.Euler(0f, turn * turnSpeed * Time.fixedDeltaTime, 0f);
        body.MoveRotation(nextRotation);

        float targetSpeed = throttle >= 0f ? throttle * forwardSpeed : throttle * reverseSpeed;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);

        Vector3 movementDirection = TankPlaneMath.Flatten(nextRotation * LocalForwardAxis);
        Vector3 nextPosition = body.position + movementDirection * (currentSpeed * Time.fixedDeltaTime);
        if (lockHeight)
        {
            nextPosition.y = planeY;
        }

        body.MovePosition(nextPosition);
    }

    private void OnValidate()
    {
        localForwardAxis = TankPlaneMath.SafeLocalForwardAxis(localForwardAxis);
        forwardSpeed = Mathf.Max(0f, forwardSpeed);
        reverseSpeed = Mathf.Max(0f, reverseSpeed);
        turnSpeed = Mathf.Max(0f, turnSpeed);
        acceleration = Mathf.Max(0.01f, acceleration);
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
