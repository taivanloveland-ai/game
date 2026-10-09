using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Top-down walking with WASD or the arrow keys.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public float speed = 5f;
    // How quickly the player can change speed on ice, in units per second each second. Lower is more slippery.
    public float iceGrip = 12f;
    public PlanetOne planet;

    Rigidbody2D body;
    Vector2 move;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        move = ReadMove();
        // Keep diagonal movement the same speed as straight movement.
        if (move.sqrMagnitude > 1f) move.Normalize();
    }

    void FixedUpdate()
    {
        Vector2 wanted = move * speed;
        // On ice the player speeds up, turns and stops gradually. Everywhere else movement is instant.
        bool onIce = planet != null && planet.IsIce(body.position);
        body.linearVelocity = onIce ? Vector2.MoveTowards(body.linearVelocity, wanted, iceGrip * Time.fixedDeltaTime) : wanted;
    }

    // Works whichever of Unity's two input systems the project has switched on.
    static Vector2 ReadMove()
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
}
