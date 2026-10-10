using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Keyboard controls in one place. Works whichever of Unity's two input systems the project has switched on.
public static class GameInput
{
    // WASD or the arrow keys, as a direction with x and y each -1, 0 or 1.
    public static Vector2 Move()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        if (k == null) return Vector2.zero;
        bool left = k.aKey.isPressed || k.leftArrowKey.isPressed;
        bool right = k.dKey.isPressed || k.rightArrowKey.isPressed;
        bool down = k.sKey.isPressed || k.downArrowKey.isPressed;
        bool up = k.wKey.isPressed || k.upArrowKey.isPressed;
#else
        bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        bool down = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        bool up = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
#endif
        return new Vector2((right ? 1f : 0f) - (left ? 1f : 0f), (up ? 1f : 0f) - (down ? 1f : 0f));
    }

    // Space, on the frame it is pressed.
    public static bool JumpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        return k != null && k.spaceKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Space);
#endif
    }

    // Mining mode: holding W or the up arrow steps to the row behind, one row after another.
    public static bool RowBehindHeld()
    {
        return Move().y > 0f;
    }

    // Mining mode: holding S or the down arrow steps to the row in front, one row after another.
    public static bool RowInFrontHeld()
    {
        return Move().y < 0f;
    }

    // Right mouse button, on the frame it is pressed: harvest what the mouse is pointing at.
    public static bool HarvestPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(1);
#endif
    }

    // Where the mouse is on screen, in pixels.
    public static Vector2 PointerPosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    // Tab opens and closes the list of carried items.
    public static bool ItemsPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        return k != null && k.tabKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Tab);
#endif
    }

    // Escape closes whatever menu is open.
    public static bool CancelPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        return k != null && k.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    // R switches between the top-down view and mining mode.
    public static bool ToggleViewPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        return k != null && k.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }
}
