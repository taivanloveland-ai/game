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

    // Mining mode: W or the up arrow steps to the row behind.
    public static bool RowBehindPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        return k != null && (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
#endif
    }

    // Mining mode: S or the down arrow steps to the row in front.
    public static bool RowInFrontPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        return k != null && (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame);
#else
        return Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
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
