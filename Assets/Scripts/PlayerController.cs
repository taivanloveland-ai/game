using UnityEngine;
using UnityEngine.Rendering;

// Moves the player. Top-down: walk in any direction and jump onto ground one block higher.
// Mining mode (side view): walk left and right, fall, and jump.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public const float Width = 0.6f;
    public const float Height = 0.9f;
    // In the top-down view only the player's feet touch the ground: the bottom part of the rectangle.
    const float FeetDepth = 0.4f;

    public float speed = 5f;
    // Walking through snow is a little slower than full speed. 1 would be no slowdown.
    public float snowSpeedFactor = 0.9f;
    // How quickly the player can change speed on ice, in units per second each second. Lower is more slippery.
    public float iceGrip = 12f;
    // How high a jump reaches, in blocks.
    public float jumpHeight = 1.5f;
    // How strongly the player is pulled down, as a multiple of normal gravity.
    public float gravityScale = 3f;

    PlanetOne planet;
    Transform bodyVisual;
    Transform shadow;
    SortingGroup sorting;
    Rigidbody2D body;
    Collider2D box;
    readonly RaycastHit2D[] groundHits = new RaycastHit2D[4];

    Vector2 move;
    bool jumpQueued;
    bool sideView;
    int sideRow;
    // Top-down only: walking speed, height of the player's feet in blocks, and how fast that height is changing.
    Vector2 velocity;
    float altitude;
    float verticalSpeed;

    float Gravity => -Physics2D.gravity.y * gravityScale;

    // The player's feet height in blocks, in either view.
    public float FeetHeight => sideView ? body.position.y - Height / 2f : altitude;

    public void Setup(PlanetOne planet, Transform bodyVisual, Transform shadow, SortingGroup sorting)
    {
        this.planet = planet;
        this.bodyVisual = bodyVisual;
        this.shadow = shadow;
        this.sorting = sorting;
        altitude = GroundUnder(transform.position);
    }

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        box = GetComponent<Collider2D>();
    }

    void Update()
    {
        move = GameInput.Move();
        // Keep diagonal movement the same speed as straight movement.
        if (move.sqrMagnitude > 1f) move.Normalize();
        if (GameInput.JumpPressed()) jumpQueued = true;
    }

    void FixedUpdate()
    {
        if (sideView) MoveSideView();
        else MoveTopDown();
        jumpQueued = false;
    }

    void LateUpdate()
    {
        // Top-down, height is shown by drawing the player further up the screen, with a shadow left on the ground.
        float ground = sideView ? 0f : GroundUnder(transform.position);
        float lift = sideView ? 0f : altitude * PlanetOne.HeightScale;
        bodyVisual.localPosition = new Vector3(0f, Height / 2f + lift, 0f);
        shadow.localPosition = new Vector3(0f, 0.1f + ground * PlanetOne.HeightScale, 0f);
        shadow.gameObject.SetActive(!sideView && altitude > ground + 0.05f);
    }

    void MoveTopDown()
    {
        float dt = Time.fixedDeltaTime;
        Vector2 position = body.position;
        bool grounded = altitude <= GroundUnder(position) + 0.001f;

        // On ice the player speeds up, turns and stops gradually. Everywhere else movement is instant.
        bool onIce = grounded && planet.IsIce(new Vector2(position.x, position.y - Height / 2f + FeetDepth / 2f));
        Vector2 wanted = move * (onIce ? speed : speed * snowSpeedFactor);
        velocity = onIce ? Vector2.MoveTowards(velocity, wanted, iceGrip * dt) : wanted;

        if (jumpQueued && grounded)
        {
            verticalSpeed = JumpSpeed();
            grounded = false;
        }
        if (!grounded)
        {
            verticalSpeed -= Gravity * dt;
            altitude += verticalSpeed * dt;
        }

        // Try each direction on its own so the player slides along edges instead of stopping dead.
        var next = new Vector2(position.x + velocity.x * dt, position.y);
        if (CanStand(next)) position = next;
        else velocity.x = 0f;
        next = new Vector2(position.x, position.y + velocity.y * dt);
        if (CanStand(next)) position = next;
        else velocity.y = 0f;

        float ground = GroundUnder(position);
        if (altitude <= ground)
        {
            altitude = ground;
            verticalSpeed = 0f;
        }
        body.MovePosition(position);
    }

    // The player can be somewhere if nothing under their feet is taller than their feet are high.
    bool CanStand(Vector2 position)
    {
        return GroundUnder(position) <= altitude + 0.01f;
    }

    // The tallest thing under the player's feet at a top-down position.
    int GroundUnder(Vector2 position)
    {
        int x0 = Mathf.FloorToInt(position.x - Width / 2f + 0.001f), x1 = Mathf.FloorToInt(position.x + Width / 2f - 0.001f);
        float feetBottom = position.y - Height / 2f;
        int y0 = Mathf.FloorToInt(feetBottom + 0.001f), y1 = Mathf.FloorToInt(feetBottom + FeetDepth - 0.001f);
        int tallest = 0;
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++) tallest = Mathf.Max(tallest, planet.TopAt(x, y));
        }
        return tallest;
    }

    void MoveSideView()
    {
        bool grounded = IsGrounded();
        bool onIce = grounded && planet.IsIce(new Vector2(body.position.x, sideRow + 0.5f));
        float wanted = move.x * (onIce ? speed : speed * snowSpeedFactor);

        Vector2 v = body.linearVelocity;
        v.x = onIce ? Mathf.MoveTowards(v.x, wanted, iceGrip * Time.fixedDeltaTime) : wanted;
        if (jumpQueued && grounded) v.y = JumpSpeed();
        body.linearVelocity = v;
    }

    // Launch speed that carries the player jumpHeight blocks up before gravity wins.
    // The extra half step makes up for movement happening in small steps, which would otherwise fall a little short.
    float JumpSpeed()
    {
        float halfStep = Gravity * Time.fixedDeltaTime * 0.5f;
        return halfStep + Mathf.Sqrt(halfStep * halfStep + 2f * Gravity * jumpHeight);
    }

    // Side view: true when something solid is directly under the player's feet.
    bool IsGrounded()
    {
        int count = box.Cast(Vector2.down, groundHits, 0.05f);
        for (int i = 0; i < count; i++)
        {
            // Only surfaces facing up count, so brushing against a wall is not standing on it.
            if (groundHits[i].normal.y > 0.5f) return true;
        }
        return false;
    }

    // Switches to the side view of one row of the world, keeping the player's place along the row and their height.
    public void EnterSideView(int row)
    {
        float feet = altitude;
        sideView = true;
        sideRow = row;
        // In the side view the player is drawn over everything, and real physics does the falling and bumping.
        sorting.sortingOrder = 20;
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = gravityScale;
        Teleport(new Vector2(body.position.x, feet + Height / 2f + 0.02f));
    }

    // Moves the side view to a different row without moving the player.
    public void SetSideRow(int row)
    {
        sideRow = row;
    }

    public void ExitSideView()
    {
        float feet = body.position.y - Height / 2f;
        sideView = false;
        sorting.sortingOrder = 0;
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        // Stand in the middle of the row so the feet are not poking into the rows on either side.
        var position = new Vector2(body.position.x, sideRow + Height / 2f + (1f - FeetDepth) / 2f);
        Teleport(position);
        velocity = Vector2.zero;
        verticalSpeed = 0f;
        altitude = Mathf.Max(feet, GroundUnder(position));
    }

    void Teleport(Vector2 position)
    {
        body.linearVelocity = Vector2.zero;
        body.position = position;
        transform.position = position;
    }
}
