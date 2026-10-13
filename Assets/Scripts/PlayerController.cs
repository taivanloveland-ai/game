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

    // Squares per second at a full walk.
    public float speed = 4f;
    // Walking through snow is a little slower than full speed. 1 would be no slowdown.
    public float snowSpeedFactor = 0.9f;
    // How quickly the player can change speed on ice, in units per second each second. Lower is more slippery.
    // Made a bit more slippery on 2026-10-11 (it was 12).
    public float iceGrip = 8f;
    // On ice the player slides a little faster than full speed. 1 would be no faster.
    public float iceSpeedFactor = 1.2f;
    // In the freezing ocean round the South Pole the player swims, at this share of full speed, and cannot jump.
    public float swimSpeedFactor = 0.5f;
    // How high a jump reaches, in blocks.
    public float jumpHeight = 1.5f;
    // How strongly the player is pulled down, as a multiple of normal gravity.
    public float gravityScale = 3f;

    PlanetOne planet;
    Transform bodyVisual;
    SpriteRenderer bodyRenderer;
    Color baseColor;
    // The colour the world is being tinted for the time of day.
    Color tint = Color.white;
    Transform shadow;
    Transform reflection;
    SortingGroup sorting;
    Rigidbody2D body;
    Collider2D box;
    readonly RaycastHit2D[] groundHits = new RaycastHit2D[4];

    Vector2 move;
    bool jumpQueued;
    // Movement asked for by the game itself (walking to something the player clicked) instead of by the keyboard.
    bool steering;
    Vector2 steerMove;
    bool steerJump;
    bool sideView;
    // The row of the world the side view shows, or the column if it is looking along a column.
    int sideSlice;
    bool sideColumn;
    // Top-down only: walking speed, height of the player's feet in blocks, and how fast that height is changing.
    Vector2 velocity;
    float altitude;
    float verticalSpeed;

    float Gravity => -Physics2D.gravity.y * gravityScale;

    // While true the player stands still and the keys do nothing, as while the screen fades going into a cave.
    public bool Frozen;

    // True while the player is swimming in the ocean.
    public bool Swimming { get; private set; }

    // True on any frame the player is pressing a movement key or jump.
    public bool HasManualInput { get; private set; }

    // How far up the screen the ground the player stands on is drawn in the top-down view, for the camera to follow.
    public float CameraLift { get; private set; }

    // The player's feet height in blocks, in either view.
    public float FeetHeight => sideView ? body.position.y - Height / 2f : altitude;

    // Where the player stands in the world as seen from above. In the side view that is their place along the row
    // or column on screen: left to right is west to east along a row, and south to north along a column.
    public Vector2 GroundPosition => !sideView
        ? new Vector2(transform.position.x, transform.position.y - Height / 2f + FeetDepth / 2f)
        : sideColumn ? new Vector2(sideSlice + 0.5f, transform.position.x) : new Vector2(transform.position.x, sideSlice + 0.5f);

    public void Setup(PlanetOne planet, Transform bodyVisual, Transform shadow, Transform reflection, SortingGroup sorting)
    {
        this.planet = planet;
        this.bodyVisual = bodyVisual;
        this.shadow = shadow;
        this.reflection = reflection;
        this.sorting = sorting;
        bodyRenderer = bodyVisual.GetComponent<SpriteRenderer>();
        baseColor = bodyRenderer.color;
        altitude = SurfaceUnder(transform.position);
    }

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        box = GetComponent<Collider2D>();
    }

    // Walks the player by itself for one frame, as if these keys were held. Call it every frame while auto-walking.
    // Any real key press takes over straight away.
    public void Steer(Vector2 direction, bool jump)
    {
        steerMove = direction;
        steerJump = jump;
        steering = true;
    }

    void Update()
    {
        Vector2 keys = GameInput.Move();
        bool jumpKey = GameInput.JumpPressed();
        HasManualInput = keys != Vector2.zero || jumpKey;

        bool useSteering = steering && !HasManualInput;
        move = Frozen ? Vector2.zero : useSteering ? steerMove : keys;
        // Keep diagonal movement the same speed as straight movement.
        if (move.sqrMagnitude > 1f) move.Normalize();
        if (!Frozen && (useSteering ? steerJump : jumpKey)) jumpQueued = true;
        steering = false;
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
        // Down a hole or a tunnel the player is drawn as far down the screen as they really are, and the ground in
        // front of them turns see-through so they can still be seen (the land does that part).
        float ground = sideView ? 0f : FloorUnder(transform.position, altitude);
        float lift = sideView ? 0f : altitude * PlanetOne.HeightScale;
        Vector2Int square = Vector2Int.FloorToInt(GroundPosition);
        bool underRoof = !sideView && planet.IsTunnel(square.x, square.y, Mathf.FloorToInt(altitude + 0.01f));
        // The camera follows the ground the player is on, not each jump, down into holes as well as up mountains.
        float floorLift = ground * PlanetOne.HeightScale;
        CameraLift = sideView ? 0f : floorLift;
        bodyVisual.localPosition = new Vector3(0f, Height / 2f + lift, 0f);
        shadow.localPosition = new Vector3(0f, 0.1f + floorLift, 0f);
        shadow.gameObject.SetActive(!sideView && altitude > ground + 0.05f);

        // The reflection hangs upside down from the ground under the player, as far below it as they are above it.
        // It only shows where the ground is ice. Under a roof, or down in the ground, there is none.
        reflection.gameObject.SetActive(!sideView && !underRoof && altitude > -0.5f);
        float gap = Mathf.Max(0f, altitude - ground) * PlanetOne.HeightScale;
        reflection.localPosition = new Vector3(0f, -Height / 2f + floorLift - gap - Height * PlanetOne.ReflectionSquash / 2f, 0f);

        bodyRenderer.color = new Color(baseColor.r * tint.r, baseColor.g * tint.g, baseColor.b * tint.b, baseColor.a);
    }

    void MoveTopDown()
    {
        float dt = Time.fixedDeltaTime;
        Vector2 position = body.position;
        float floor = FloorUnder(position, altitude);
        bool grounded = altitude <= floor + 0.001f;

        // On ice the player speeds up, turns and stops gradually. Everywhere else movement is instant.
        Vector2 feetAt = new Vector2(position.x, position.y - Height / 2f + FeetDepth / 2f);
        bool onIce = grounded && planet.IsIce(feetAt);
        Swimming = grounded && planet.IsWater(feetAt);
        Vector2 wanted = move * (Swimming ? speed * swimSpeedFactor : onIce ? speed * iceSpeedFactor : speed * snowSpeedFactor);
        velocity = onIce ? Vector2.MoveTowards(velocity, wanted, iceGrip * dt) : wanted;

        if (jumpQueued && grounded && !Swimming)
        {
            verticalSpeed = JumpSpeed();
            grounded = false;
        }
        if (!grounded)
        {
            verticalSpeed -= Gravity * dt;
            float risen = altitude + verticalSpeed * dt;
            if (risen <= floor)
            {
                // Landed.
                altitude = floor;
                verticalSpeed = 0f;
            }
            else if (verticalSpeed > 0f && !Fits(position, risen))
            {
                // Bumped their head on the roof of a tunnel.
                verticalSpeed = 0f;
            }
            else
            {
                altitude = risen;
            }
        }

        // Try each direction on its own so the player slides along edges instead of stopping dead.
        var next = new Vector2(position.x + velocity.x * dt, position.y);
        if (Fits(next, altitude)) position = next;
        else velocity.x = 0f;
        next = new Vector2(position.x, position.y + velocity.y * dt);
        if (Fits(next, altitude)) position = next;
        else velocity.y = 0f;

        // Stepping onto ground a hair higher than the feet lifts the player onto it.
        floor = FloorUnder(position, altitude);
        if (altitude <= floor)
        {
            altitude = floor;
            verticalSpeed = 0f;
        }

        // Walking over a pole comes out on the far side of the planet, half way round, now heading the other way on
        // the map: over the North Pole you come down it heading south. North stays up the screen.
        Vector2 feet = new Vector2(position.x, position.y - Height / 2f + FeetDepth / 2f);
        // In a cave there are no poles, and nothing to go round.
        if (!planet.InCave && planet.OverPole(feet, out Vector2 across))
        {
            velocity.y = -velocity.y;
            Teleport(new Vector2(across.x, across.y + Height / 2f - FeetDepth / 2f));
            return;
        }
        // Far round the planet to the east or west, the player is moved back by a whole planet width, which looks
        // exactly the same, so positions never grow big enough to lose their exactness.
        if (!planet.InCave && (feet.x < -planet.width || feet.x > planet.width * 2f))
        {
            Teleport(new Vector2(position.x - Mathf.Floor(feet.x / planet.width) * planet.width, position.y));
            return;
        }
        body.MovePosition(position);
    }

    // The squares the player's feet are over at a top-down position.
    RectInt FeetSquares(Vector2 position)
    {
        int x0 = Mathf.FloorToInt(position.x - Width / 2f + 0.001f), x1 = Mathf.FloorToInt(position.x + Width / 2f - 0.001f);
        float feetBottom = position.y - Height / 2f;
        int y0 = Mathf.FloorToInt(feetBottom + 0.001f), y1 = Mathf.FloorToInt(feetBottom + FeetDepth - 0.001f);
        return new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    // The player can be somewhere at a height if no block is in the space their body would take up there. This works
    // the same on the surface, in a hole, and in a tunnel with ground overhead.
    bool Fits(Vector2 position, float feet)
    {
        int bottom = Mathf.FloorToInt(feet + 0.01f), top = Mathf.FloorToInt(feet + Height - 0.01f);
        foreach (Vector2Int square in FeetSquares(position).allPositionsWithin)
        {
            for (int level = bottom; level <= top; level++)
            {
                if (planet.SolidAt(square.x, square.y, level)) return false;
            }
        }
        return true;
    }

    // The height of the floor under the player's feet: the top of the highest block at or below them.
    float FloorUnder(Vector2 position, float feet)
    {
        int highest = -PlanetOne.GroundDepth;
        foreach (Vector2Int square in FeetSquares(position).allPositionsWithin)
        {
            int level = Mathf.FloorToInt(feet + 0.01f) - 1;
            while (level >= -PlanetOne.GroundDepth && !planet.SolidAt(square.x, square.y, level)) level--;
            highest = Mathf.Max(highest, level + 1);
        }
        return highest;
    }

    // The top of the land under the player's feet, counting anything standing on it.
    int SurfaceUnder(Vector2 position)
    {
        int tallest = int.MinValue;
        foreach (Vector2Int square in FeetSquares(position).allPositionsWithin)
        {
            int surface = planet.TopAt(square.x, square.y);
            // A fern or ice shard standing there is stood on, not in.
            if (planet.SolidAt(square.x, square.y, surface)) surface++;
            tallest = Mathf.Max(tallest, surface);
        }
        return tallest;
    }

    void MoveSideView()
    {
        bool grounded = IsGrounded();
        bool onIce = grounded && planet.IsIce(GroundPosition);
        Swimming = grounded && planet.IsWater(GroundPosition);
        float wanted = move.x * (Swimming ? speed * swimSpeedFactor : onIce ? speed * iceSpeedFactor : speed * snowSpeedFactor);

        Vector2 v = body.linearVelocity;
        v.x = onIce ? Mathf.MoveTowards(v.x, wanted, iceGrip * Time.fixedDeltaTime) : wanted;
        if (jumpQueued && grounded && !Swimming) v.y = JumpSpeed();
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

    // Switches to the side view of one row or column of the world, keeping the player's place along it and their height.
    public void EnterSideView(int slice, bool column)
    {
        float feet = altitude;
        float along = column ? GroundPosition.y : body.position.x;
        if (column)
        {
            // Seen along a column the player's body is wider than their feet were from above. If that would put an
            // edge of it inside a block, they shift over just enough to stand clear.
            int square = Mathf.FloorToInt(along);
            if (Mathf.FloorToInt(along - Width / 2f) < square && !Fits(slice, square - 1, feet)) along = square + Width / 2f + 0.001f;
            else if (Mathf.FloorToInt(along + Width / 2f) > square && !Fits(slice, square + 1, feet)) along = square + 1 - Width / 2f - 0.001f;
        }
        sideView = true;
        sideSlice = slice;
        sideColumn = column;
        // In the side view the player is drawn over everything, and real physics does the falling and bumping.
        sorting.sortingOrder = 60;
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = gravityScale;
        Teleport(new Vector2(along, feet + Height / 2f + 0.02f));
    }

    // True if the player's body, with its feet at a height, would fit in one square of the world.
    bool Fits(int x, int y, float feet)
    {
        int bottom = Mathf.FloorToInt(feet + 0.01f), top = Mathf.FloorToInt(feet + Height - 0.01f);
        for (int level = bottom; level <= top; level++)
        {
            if (planet.SolidAt(x, y, level)) return false;
        }
        return true;
    }

    // True if the player's body is in a block-sized space of the world: a square, at a level.
    public bool Occupies(Vector2Int square, int level)
    {
        float feet = FeetHeight;
        if (level + 1 <= feet + 0.01f || level >= feet + Height - 0.01f) return false;
        if (!sideView) return FeetSquares(body.position).Contains(square);
        int along = sideColumn ? square.y : square.x, slice = sideColumn ? square.x : square.y;
        return slice == sideSlice && along + 1 > body.position.x - Width / 2f && along < body.position.x + Width / 2f;
    }

    // Puts the player somewhere else in the top-down view, standing on the ground there: waking up at the ship.
    public void Respawn(Vector2 position)
    {
        Teleport(position);
        velocity = Vector2.zero;
        verticalSpeed = 0f;
        altitude = SurfaceUnder(position);
    }

    // Darkens or restores the player's colour; white means their normal colour.
    public void SetTint(Color tint)
    {
        this.tint = tint;
    }

    // Moves the side view to a different row or column without moving the player.
    public void SetSideRow(int slice)
    {
        sideSlice = slice;
    }

    public void ExitSideView()
    {
        float feet = body.position.y - Height / 2f;
        sideView = false;
        sorting.sortingOrder = 0;
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        // Stand in the middle of the row or column so the feet are not poking into the ones on either side.
        var position = sideColumn
            ? new Vector2(sideSlice + 0.5f, body.position.x + Height / 2f - FeetDepth / 2f)
            : new Vector2(body.position.x, sideSlice + Height / 2f + (1f - FeetDepth) / 2f);
        Teleport(position);
        velocity = Vector2.zero;
        verticalSpeed = 0f;
        // Stay at the same height, so a player down a hole or in a tunnel is still down there in the top-down view.
        // Only if they would not fit there (which moving to the middle of the row can cause) are they put on the surface.
        altitude = Fits(position, feet) ? Mathf.Max(feet, FloorUnder(position, feet)) : SurfaceUnder(position);
    }

    void Teleport(Vector2 position)
    {
        body.linearVelocity = Vector2.zero;
        body.position = position;
        transform.position = position;
    }
}
