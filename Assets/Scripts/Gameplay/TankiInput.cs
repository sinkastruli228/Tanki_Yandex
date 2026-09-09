using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One input boundary for gameplay. Keyboard/mouse remain the default, while
/// common controller actions work without coupling every system to a device.
/// </summary>
public static class TankiInput
{
    private const float StickDeadZone = .2f;

    public static Vector2 Move
    {
        get
        {
            Vector2 value = Vector2.zero;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) value.x -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) value.x += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) value.y -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) value.y += 1f;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                if (stick.sqrMagnitude >= StickDeadZone * StickDeadZone)
                {
                    value += stick;
                }
            }

            return Vector2.ClampMagnitude(value, 1f);
        }
    }

    public static bool FireHeld =>
        (Mouse.current != null && Mouse.current.leftButton.isPressed)
        || (Gamepad.current != null && (Gamepad.current.rightTrigger.ReadValue() > .25f || Gamepad.current.buttonSouth.isPressed));

    public static bool BoostHeld =>
        (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
        || (Gamepad.current != null && Gamepad.current.leftShoulder.isPressed);

    public static bool SpecialPressed =>
        (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        || (Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame);

    public static bool DebugChargePressed =>
        Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame;

    public static bool CancelPressed =>
        (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        || (Gamepad.current != null && (Gamepad.current.buttonEast.wasPressedThisFrame || Gamepad.current.startButton.wasPressedThisFrame));

    public static bool TurretViewTogglePressed =>
        (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        || (Gamepad.current != null && Gamepad.current.rightStickButton.wasPressedThisFrame);

    public static bool TryGetGamepadAimDirection(Camera camera, out Vector3 direction)
    {
        Gamepad gamepad = Gamepad.current;
        Vector2 stick = gamepad != null ? gamepad.rightStick.ReadValue() : Vector2.zero;
        if (camera == null || stick.sqrMagnitude < StickDeadZone * StickDeadZone)
        {
            direction = Vector3.zero;
            return false;
        }

        Vector3 forward = TankPlaneMath.Flatten(camera.transform.forward);
        Vector3 right = TankPlaneMath.Flatten(camera.transform.right);
        direction = TankPlaneMath.Flatten(right * stick.x + forward * stick.y);
        return direction.sqrMagnitude > .001f;
    }
}
