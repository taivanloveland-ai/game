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

    // Set by the on-screen inventory: true while the mouse is over it, so clicks there do not also land on the world.
    public static System.Func<bool> PointerOverInventory;
    // Set by the maps: true while the mouse is over the mini map, or dragging it.
    public static System.Func<bool> PointerOverMap;

    // True while the mouse is over something on screen that takes its own clicks and scrolling.
    static bool PointerOverPanel()
    {
        return (PointerOverInventory != null && PointerOverInventory()) || (PointerOverMap != null && PointerOverMap());
    }

    // Left mouse button, on the frame it is pressed. Pointing at a fern or ice shard, it collects it; pointing at a
    // block in the side-scroller, it is one hit with the pickaxe; with a block equipped, it places the block.
    public static bool LeftClickPressed()
    {
        if (PointerOverPanel()) return false;
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    // Left mouse button, for as long as it is held down: keeps the pickaxe swinging.
    public static bool LeftClickHeld()
    {
        if (PointerOverPanel()) return false;
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    // Right mouse button, on the frame it is pressed: use what the mouse is pointing at, such as the crafting nook.
    public static bool RightClickPressed()
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

    // The number keys pick a hotbar slot: 1 to 9 for the first nine, and 0 for the tenth. Returns the slot counting
    // from 0, or -1 if none was pressed this frame.
    public static int HotbarSlotPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        if (k == null) return -1;
        if (k.digit1Key.wasPressedThisFrame) return 0;
        if (k.digit2Key.wasPressedThisFrame) return 1;
        if (k.digit3Key.wasPressedThisFrame) return 2;
        if (k.digit4Key.wasPressedThisFrame) return 3;
        if (k.digit5Key.wasPressedThisFrame) return 4;
        if (k.digit6Key.wasPressedThisFrame) return 5;
        if (k.digit7Key.wasPressedThisFrame) return 6;
        if (k.digit8Key.wasPressedThisFrame) return 7;
        if (k.digit9Key.wasPressedThisFrame) return 8;
        if (k.digit0Key.wasPressedThisFrame) return 9;
        return -1;
#else
        for (int slot = 0; slot < 9; slot++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + slot)) return slot;
        }
        return Input.GetKeyDown(KeyCode.Alpha0) ? 9 : -1;
#endif
    }

    // The mouse wheel, as whole steps this frame: +1 for a notch scrolled down (toward the player), -1 for a notch up.
    // Over the mini map or the inventory the wheel is theirs, so it does not also move along the hotbar.
    public static int ScrollSteps()
    {
#if ENABLE_INPUT_SYSTEM
        float wheel = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
        float wheel = Input.mouseScrollDelta.y;
#endif
        if (PointerOverPanel()) return 0;
        return wheel < 0f ? 1 : wheel > 0f ? -1 : 0;
    }

    // M opens and closes the world map.
    public static bool MapPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        return k != null && k.mKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.M);
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

    // G switches to mining mode looking along a column of the world instead of a row.
    public static bool ToggleColumnPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard k = Keyboard.current;
        return k != null && k.gKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.G);
#endif
    }
}
