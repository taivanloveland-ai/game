using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Mining mode: press F to see the row of the world the player is standing on from the side, as a 2D side-scroller.
// The next few rows further back are drawn dimmer and a little higher behind it. W and S step between rows:
// the rows slide forward or back, and the one arriving at the front brightens into the new front row.
// Press F again to go back to the top-down view.
// R does the same along a column of the world instead of a row: the player looks west, with north on the right.
// Pressing the other key while in mining mode turns the view on the spot.
public class MiningMode : MonoBehaviour
{
    // How many blocks of ground are shown below the surface, and how much sky the camera may show above it.
    const int Depth = PlanetOne.GroundDepth;
    const int SkyHeight = 56;
    // Only this many columns of the row are drawn at a time, centred on the player. The row is redrawn further along
    // when the player gets within the margin of either end. A whole row of the planet is far too long to draw at once.
    public const int WindowColumns = 96;
    const int WindowMargin = 26;
    // How long stepping to the next row takes. Walking one block top-down takes about this long.
    const float RowStepSeconds = 0.25f;
    // How many rows behind the front one are drawn. Many rows, each only a sliver higher and a touch dimmer than the
    // one in front of it, blend into a soft gradient going back into the distance, where a few would show as hard bands.
    const int BehindRows = 10;
    // Each row further back is drawn this much higher, in blocks, so rows read as being behind one another.
    const float RowOffset = 0.06f;
    // Light reaches this many blocks into the ground from open air, fading as it goes. Past that everything is black.
    const float LightDepth = 1.5f;
    // The darkness is painted at this many pixels per block.
    const int DarkPixels = 8;

    // How much dimmer each row behind the front one is, nearest first: an even fade from just under full brightness
    // to a dim blue at the furthest row.
    static readonly Color NearestTint = new Color(0.9f, 0.92f, 0.95f), FurthestTint = new Color(0.3f, 0.34f, 0.44f);
    static readonly Color[] BehindTints = MakeBehindTints();

    static Color[] MakeBehindTints()
    {
        var tints = new Color[BehindRows];
        for (int i = 0; i < tints.Length; i++) tints[i] = Color.Lerp(NearestTint, FurthestTint, i / (float)(tints.Length - 1));
        return tints;
    }
    static readonly Color SnowTop = new Color32(234, 241, 247, 255), SnowDeep = new Color32(188, 205, 223, 255);
    static readonly Color IceTop = new Color32(146, 202, 232, 255), IceDeep = new Color32(92, 150, 198, 255);
    // What the game's own ice blocks are multiplied by at their deepest, taking the surface blue down to the deep blue.
    static readonly Color IceDepthTint = new Color(0.63f, 0.74f, 0.85f);
    static readonly Color Stone = new Color32(70, 80, 96, 255);
    static readonly Color WaterDeep = new Color32(14, 34, 62, 255);

    // Drawing order, back to front: the rows behind (furthest first, with 0 for one sliding in at the back), each with
    // the things standing on it just above it, then the front row, its darkness, a row sliding in at the front and
    // its darkness, then things standing in the front row.
    const int FrontOrder = 2 * (BehindRows + 1);

    // The drawing order of a row, given as how many rows behind the front one it is. Rows are two apart, so the
    // things standing on a row can go between it and the row in front of it.
    static int BehindOrder(int back)
    {
        return FrontOrder - 2 * back;
    }

    // How many rows are drawn behind the front one.
    public const int RowsBehind = BehindRows;

    // How much higher a row behind the front one is drawn, in blocks, and the colour it is shaded with now.
    public static float BehindLift(int back)
    {
        return back * RowOffset;
    }

    public Color BehindShade(int back)
    {
        Color shade = BehindTints[Mathf.Clamp(back, 1, BehindTints.Length) - 1], day = dayNight.WorldTint;
        return new Color(shade.r * day.r, shade.g * day.g, shade.b * day.b, 1f);
    }

    // The drawing order for things standing on a row behind the front one (back is 1 or more).
    public static int BehindThingOrder(int back)
    {
        return BehindOrder(back) + 1;
    }
    const int DarkOrder = FrontOrder + 1;
    const int IncomingFrontOrder = FrontOrder + 2;
    const int IncomingDarkOrder = FrontOrder + 3;
    // Things standing in the row (plants, dropped items) are drawn over the ground and its darkness, under the player.
    public const int ObjectSortingOrder = FrontOrder + 4;

    public bool Active { get; private set; }
    // False when the side view shows a row of the world (looking north), true when it shows a column (looking west).
    public bool Column { get; private set; }
    // The row or column of the world the side view is showing.
    public int Slice => slice;
    // The first block along the screen of the stretch being drawn.
    public int WindowStart => windowStart;
    // True while the rows are sliding from one row to the next.
    public bool Stepping => stepDirection != 0;
    // Everything that belongs to the side view hangs under this.
    public Transform Root => root.transform;
    // Raised each time the side view is redrawn for a row.
    public event System.Action RowBuilt;

    PlanetOne planet;
    PlayerController player;
    CameraFollow follow;
    Camera cam;
    DayNight dayNight;
    Color topDownSky;
    Color appliedTint;

    GameObject root;
    Tilemap rowMap;
    Tilemap solidMap;
    Tilemap[] behindMaps;
    // An extra row used only during a step: the one coming into view at the back, or arriving at the front.
    Tilemap spareMap;
    TilemapCollider2D tileCollider;
    CompositeCollider2D composite;
    Tile rockTopTile;
    readonly Dictionary<int, Tile> tiles = new Dictionary<int, Tile>();
    // Black pictures laid over a row, see-through where light reaches: one for the front row,
    // and one for the row that is becoming the front row during a step.
    SpriteRenderer dark, incomingDark;
    Color32[] darkPixels;
    // Which blocks of a row have a block in front of them in the next row forward. Only these can be dark.
    bool[,] covered;
    int slice;
    int windowStart;
    TileBase[] fillBuffer;
    float blockedUntil;
    // A step between rows in progress: +1 toward the row behind, -1 toward the row in front, 0 for none.
    int stepDirection;
    float stepStartedAt;
    GUIStyle labelStyle;

    public void Setup(PlanetOne planet, PlayerController player, CameraFollow follow, Camera cam, DayNight dayNight)
    {
        this.planet = planet;
        this.player = player;
        this.follow = follow;
        this.cam = cam;
        this.dayNight = dayNight;

        var grid = new GameObject("Mining View").AddComponent<Grid>();
        grid.transform.SetParent(transform, false);
        root = grid.gameObject;

        behindMaps = new Tilemap[BehindTints.Length];
        for (int i = 0; i < behindMaps.Length; i++)
        {
            behindMaps[i] = PlanetOne.MakeTilemap(grid, "Row Behind " + (i + 1), BehindOrder(i + 1));
        }
        rowMap = PlanetOne.MakeTilemap(grid, "Row", FrontOrder);
        spareMap = PlanetOne.MakeTilemap(grid, "Row Arriving", 0);

        // The solid ground is a separate, unseen copy of the front row. It never moves, so the pictures of the rows
        // can slide around during a step without carrying the player along.
        solidMap = PlanetOne.MakeTilemap(grid, "Row Solid", 0);
        solidMap.GetComponent<TilemapRenderer>().enabled = false;
        var body = solidMap.gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        tileCollider = solidMap.gameObject.AddComponent<TilemapCollider2D>();
        composite = solidMap.gameObject.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

        rockTopTile = PlanetOne.MakeTile(Color.white, PlanetOne.PaintSprite((x, y) => PlanetOne.PaintRockTop(x, y, 1)), true);

        int columns = WindowColumns, levels = Depth + PlanetOne.BuildLimit;
        covered = new bool[columns, levels];
        darkPixels = new Color32[columns * DarkPixels * levels * DarkPixels];
        dark = MakeDarkness("Darkness", DarkOrder);
        incomingDark = MakeDarkness("Darkness Arriving", IncomingDarkOrder);

        root.SetActive(false);
    }

    SpriteRenderer MakeDarkness(string name, int sortingOrder)
    {
        var texture = new Texture2D(WindowColumns * DarkPixels, (Depth + PlanetOne.BuildLimit) * DarkPixels) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero, DarkPixels);
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    void Update()
    {
        if (GameInput.ToggleViewPressed()) Show(false);
        else if (GameInput.ToggleColumnPressed()) Show(true);
        if (!Active) return;
        if (stepDirection != 0) UpdateRowStep();
        else if (GameInput.RowBehindHeld()) BeginRowStep(1);
        else if (GameInput.RowInFrontHeld()) BeginRowStep(-1);
        else if (NearWindowEnd())
        {
            CentreWindow();
            Build();
            PoseRows(0f);
        }
        ApplyLight();
    }

    // The square of the world, as seen from above, that is `along` blocks along the screen and `back` rows behind the
    // front one. A row runs east; the rows behind it are to the north. A column runs north; behind it is west.
    public Vector2Int Cell(int along, int back = 0)
    {
        return Column ? new Vector2Int(slice - back, along) : new Vector2Int(along, slice + back);
    }

    // True if a square of the world is in the row or column on screen.
    public bool Shows(Vector2Int cell)
    {
        return Column ? cell.x == slice : cell.y == slice;
    }

    // How far along the screen a place in the world is.
    public float Along(Vector2 ground)
    {
        return Column ? ground.y : ground.x;
    }

    PlanetOne.SideBlock BlockAt(int along, int back, int level)
    {
        Vector2Int cell = Cell(along, back);
        return planet.SideBlockAt(cell.x, cell.y, level);
    }

    // Puts the player in the middle of the stretch of row that is drawn.
    void CentreWindow()
    {
        windowStart = Mathf.FloorToInt(player.transform.position.x) - WindowColumns / 2;
    }

    bool NearWindowEnd()
    {
        float x = player.transform.position.x;
        return x < windowStart + WindowMargin || x > windowStart + WindowColumns - WindowMargin;
    }

    // Starts sliding the rows one place forward (stepping back, +1) or one place back (stepping forward, -1).
    // It takes about as long as walking one block does in the top-down view.
    void BeginRowStep(int direction)
    {
        if (!CanShiftRow(direction))
        {
            blockedUntil = Time.time + 0.7f;
            return;
        }
        stepDirection = direction;
        stepStartedAt = Time.time;
        // Stepping back, a new row comes into view at the very back. Stepping forward, a new row arrives in front.
        FillRow(spareMap, direction > 0 ? behindMaps.Length + 1 : -1);
        spareMap.GetComponent<TilemapRenderer>().sortingOrder = direction > 0 ? 0 : IncomingFrontOrder;
        PaintDarkness(incomingDark.sprite.texture, direction);
        PoseRows(0f);
    }

    void UpdateRowStep()
    {
        float progress = Mathf.Clamp01((Time.time - stepStartedAt) / RowStepSeconds);
        PoseRows(stepDirection * Mathf.SmoothStep(0f, 1f, progress));
        if (progress < 1f) return;

        // The slide is done: make the row that arrived the real front row, and put everything back in its usual place.
        int direction = stepDirection;
        stepDirection = 0;
        spareMap.ClearAllTiles();
        ShiftRow(direction);
        PoseRows(0f);
    }

    // Places and colours every row. Shift is how far through a step we are: 0 at rest, +1 when every row has moved
    // one place forward, -1 when every row has moved one place back.
    void PoseRows(float shift)
    {
        Color tint = dayNight.WorldTint;
        appliedTint = tint;
        PoseRow(rowMap.transform, rowMap, 0f - shift, tint);
        for (int i = 0; i < behindMaps.Length; i++) PoseRow(behindMaps[i].transform, behindMaps[i], i + 1 - shift, tint);
        if (stepDirection != 0)
        {
            float sparePlace = stepDirection > 0 ? behindMaps.Length + 1 : -1;
            PoseRow(spareMap.transform, spareMap, sparePlace - shift, tint);
        }

        // The darkness belongs to the front row. During a step the old front row's darkness fades away with it,
        // and the darkness of the row becoming the front fades in.
        // Both stay nearly solid through the middle of the step, so the hidden underground never flashes into view.
        float amount = Mathf.Abs(shift), left = 1f - amount;
        dark.transform.localPosition = new Vector3(windowStart, -Depth - shift * RowOffset, 0f);
        dark.color = new Color(1f, 1f, 1f, 1f - amount * amount * amount);
        incomingDark.transform.localPosition = new Vector3(windowStart, -Depth + (stepDirection - shift) * RowOffset, 0f);
        incomingDark.color = new Color(1f, 1f, 1f, stepDirection != 0 ? 1f - left * left * left : 0f);
        player.SetTint(tint);
    }

    // Place 0 is the front row; 1, 2 and 3 are the rows behind it. A row past either end is see-through.
    static void PoseRow(Transform transform, Tilemap map, float place, Color tint)
    {
        transform.localPosition = new Vector3(0f, place * RowOffset, 0f);
        Color color;
        int last = BehindTints.Length;
        if (place <= 0f)
        {
            color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f + place));
        }
        else if (place >= last)
        {
            color = BehindTints[last - 1];
            color.a = Mathf.Clamp01(last + 1 - place);
        }
        else
        {
            int nearer = Mathf.FloorToInt(place);
            color = Color.Lerp(nearer == 0 ? Color.white : BehindTints[nearer - 1], BehindTints[nearer], place - nearer);
        }
        map.color = new Color(color.r * tint.r, color.g * tint.g, color.b * tint.b, color.a);
    }

    // Matches the sky and the brightness of the ground to the time of day.
    void ApplyLight()
    {
        cam.backgroundColor = dayNight.SkyColor;
        if (stepDirection == 0 && dayNight.WorldTint != appliedTint) PoseRows(0f);
    }

    // Leaves mining mode, or enters it showing the row the player is on.
    public void Toggle()
    {
        Show(Active && Column);
    }

    // Shows the row (false) or column (true) the player is on. Asking for the view already showing goes back to
    // top-down; asking for the other one turns the view on the spot.
    public void Show(bool column)
    {
        bool leaving = Active && Column == column;
        if (Active) Leave();
        if (leaving) return;
        Column = column;
        Enter();
    }

    void Enter()
    {
        Active = true;
        stepDirection = 0;
        spareMap.ClearAllTiles();
        // The row or column is the one the player's feet are in.
        Vector2 position = player.transform.position;
        slice = Column ? Mathf.FloorToInt(position.x) : Mathf.FloorToInt(position.y - PlayerController.Height / 2f + 0.2f);
        topDownSky = cam.backgroundColor;
        planet.SetTopDownVisible(false);
        root.SetActive(true);
        // The player goes to their place along the screen first, so the right stretch of ground is drawn round them.
        player.EnterSideView(slice, Column);
        CentreWindow();
        Build();
        follow.bounds = new Rect(PlanetOne.Unbounded.x, -Depth, PlanetOne.Unbounded.width, Depth + SkyHeight);
        PoseRows(0f);
        ApplyLight();
    }

    void Leave()
    {
        Active = false;
        stepDirection = 0;
        spareMap.ClearAllTiles();
        root.SetActive(false);
        planet.SetTopDownVisible(true);
        player.ExitSideView();
        // Top-down night is a wash over the whole screen, so the player goes back to their own colour.
        player.SetTint(Color.white);
        follow.bounds = PlanetOne.Unbounded;
        cam.backgroundColor = topDownSky;
    }

    // True if nothing in the row behind (+1) or in front (-1) is in the way where the player is.
    bool CanShiftRow(int direction)
    {
        Vector2 position = player.transform.position;
        int x0 = Mathf.FloorToInt(position.x - PlayerController.Width / 2f + 0.001f);
        int x1 = Mathf.FloorToInt(position.x + PlayerController.Width / 2f - 0.001f);
        // The player can step across if every block their body would take up in that row is empty. Underground,
        // that means the row has been dug out there.
        int l0 = Mathf.FloorToInt(player.FeetHeight + 0.05f), l1 = Mathf.FloorToInt(player.FeetHeight + PlayerController.Height - 0.05f);
        for (int x = x0; x <= x1; x++)
        {
            for (int level = l0; level <= l1; level++)
            {
                Vector2Int cell = Cell(x, direction);
                if (planet.SolidAt(cell.x, cell.y, level)) return false;
            }
        }
        return true;
    }

    // Redraws one block of the front row after it has been mined away or a block has been placed there: its picture,
    // whether it is solid, and the light around it.
    public void RedrawBlock(int x, int level)
    {
        var cell = new Vector3Int(x, level, 0);
        Vector2Int square = Cell(x);
        PlanetOne.SideBlock block = planet.SideBlockAt(square.x, square.y, level);
        Tile tile = block != PlanetOne.SideBlock.Air ? TileFor(block, square.x, square.y, level) : null;
        rowMap.SetTile(cell, tile);
        solidMap.SetTile(cell, tile);
        tileCollider.ProcessTilemapChanges();
        composite.GenerateGeometry();
        PaintDarkness(dark.sprite.texture, 0);
    }

    // Steps the player one row back (+1) or forward (-1) at once, with no slide.
    // Returns false and does nothing when the space is taken.
    public bool ShiftRow(int direction)
    {
        if (!CanShiftRow(direction))
        {
            blockedUntil = Time.time + 0.7f;
            return false;
        }

        // The row behind is one to the north. The column behind is one to the west.
        slice += Column ? -direction : direction;
        Build();
        player.SetSideRow(slice);
        return true;
    }

    void Build()
    {
        FillRow(rowMap, 0);
        FillRow(solidMap, 0);
        for (int i = 0; i < behindMaps.Length; i++) FillRow(behindMaps[i], 1 + i);
        // Make the new ground solid right away instead of waiting for the end of the frame.
        tileCollider.ProcessTilemapChanges();
        composite.GenerateGeometry();
        PaintDarkness(dark.sprite.texture, 0);
        RowBuilt?.Invoke();
    }

    // Paints the darkness for one row, given as how many rows behind the front one it is: its ground is blacked out
    // except near open air, fully lit at the surface and fading to black LightDepth blocks in. Any side of a block
    // that touches air lets light in.
    void PaintDarkness(Texture2D texture, int back)
    {
        int columns = covered.GetLength(0), levels = covered.GetLength(1);
        for (int x = 0; x < columns; x++)
        {
            for (int l = 0; l < levels; l++)
            {
                // A block can only be shaded if the row in front has a block covering it. With nothing in front it is
                // in the open, so it stays fully lit and lets light into its neighbours just like air does.
                covered[x, l] = BlockAt(windowStart + x, back, l - Depth) != PlanetOne.SideBlock.Air &&
                    BlockAt(windowStart + x, back - 1, l - Depth) != PlanetOne.SideBlock.Air;
            }
        }

        int reach = Mathf.CeilToInt(LightDepth);
        int stride = texture.width;
        for (int x = 0; x < columns; x++)
        {
            for (int l = 0; l < levels; l++)
            {
                bool nearAir = false;
                if (covered[x, l])
                {
                    for (int dx = -reach; dx <= reach && !nearAir; dx++)
                    {
                        for (int dl = -reach; dl <= reach && !nearAir; dl++) nearAir = IsAir(x + dx, l + dl);
                    }
                }

                for (int py = 0; py < DarkPixels; py++)
                {
                    for (int px = 0; px < DarkPixels; px++)
                    {
                        byte alpha = 0;
                        if (covered[x, l])
                        {
                            alpha = 255;
                            if (nearAir)
                            {
                                float distance = DistanceToAir(x + (px + 0.5f) / DarkPixels, l + (py + 0.5f) / DarkPixels, reach);
                                alpha = (byte)(Mathf.Clamp01(distance / LightDepth) * 255f);
                            }
                        }
                        darkPixels[(l * DarkPixels + py) * stride + x * DarkPixels + px] = new Color32(0, 0, 0, alpha);
                    }
                }
            }
        }
        texture.SetPixels32(darkPixels);
        texture.Apply(false);
    }

    // True for anything light passes through: air, and blocks with nothing in front of them.
    // Open sky above the drawn area counts too; past the sides and below the bottom does not.
    bool IsAir(int x, int l)
    {
        if (l >= covered.GetLength(1)) return true;
        if (x < 0 || l < 0 || x >= covered.GetLength(0)) return false;
        return !covered[x, l];
    }

    // Distance in blocks from a point to the nearest air block, looking only at blocks within reach.
    float DistanceToAir(float fx, float fl, int reach)
    {
        int cx = Mathf.FloorToInt(fx), cl = Mathf.FloorToInt(fl);
        float best = float.MaxValue;
        for (int x = cx - reach; x <= cx + reach; x++)
        {
            for (int l = cl - reach; l <= cl + reach; l++)
            {
                if (!IsAir(x, l)) continue;
                float dx = Mathf.Max(0f, Mathf.Max(x - fx, fx - (x + 1)));
                float dl = Mathf.Max(0f, Mathf.Max(l - fl, fl - (l + 1)));
                best = Mathf.Min(best, Mathf.Sqrt(dx * dx + dl * dl));
            }
        }
        return best;
    }

    // Draws one row into a tilemap, given as how many rows behind the front one it is.
    void FillRow(Tilemap map, int back)
    {
        map.ClearAllTiles();
        // All the row's tiles are handed over in one go, which is much quicker than placing them one at a time.
        int levels = Depth + PlanetOne.BuildLimit;
        if (fillBuffer == null) fillBuffer = new TileBase[WindowColumns * levels];
        for (int l = 0; l < levels; l++)
        {
            for (int i = 0; i < WindowColumns; i++)
            {
                int level = l - Depth;
                Vector2Int cell = Cell(windowStart + i, back);
                PlanetOne.SideBlock block = planet.SideBlockAt(cell.x, cell.y, level);
                fillBuffer[l * WindowColumns + i] = block != PlanetOne.SideBlock.Air ? TileFor(block, cell.x, cell.y, level) : null;
            }
        }
        map.SetTilesBlock(new BoundsInt(windowStart, -Depth, 0, WindowColumns, levels, 1), fillBuffer);
    }

    Tile TileFor(PlanetOne.SideBlock block, int x, int worldRow, int level)
    {
        if (block == PlanetOne.SideBlock.RockTop) return rockTopTile;

        Color color;
        int surface = planet.GroundAt(x, worldRow);
        // How many blocks below the surface this one is; the top block of the ground is 1.
        int depth = surface - level;
        switch (block)
        {
            // Snow and ice get darker the deeper they are, so the layers read as layers.
            case PlanetOne.SideBlock.Snow: color = Color.Lerp(SnowTop, SnowDeep, (depth - 1) / 4f); break;
            case PlanetOne.SideBlock.Ice: color = Color.Lerp(IceTop, IceDeep, (depth - 1) / 3f); break;
            case PlanetOne.SideBlock.Rock: color = PlanetOne.RockColors[0]; break;
            // The ocean gets darker toward its bed.
            case PlanetOne.SideBlock.Water: color = Color.Lerp(PlanetOne.WaterColor, WaterDeep, (depth - 1) / 3f); break;
            case PlanetOne.SideBlock.Ship: color = level == surface ? PlanetOne.ShipSideColor : PlanetOne.ShipColor; break;
            default: color = Stone; break;
        }
        // Slight block-to-block variation so large areas are not one flat colour.
        Vector2Int spot = planet.Wrap(x, worldRow);
        int hash = (spot.x * 73856093) ^ (level * 19349663) ^ (spot.y * 83492791);
        hash = (hash ^ (hash >> 13)) * 0x5bd1e995;
        bool darker = ((hash >> 15) & 1) == 1;
        if (darker && block != PlanetOne.SideBlock.Ship) color *= 0.965f;
        color.a = 1f;

        // Snow and ice blocks use their drawn pictures when there are any, exactly as drawn: no colouring and no flipping.
        // Snow has three looks, picked block by block: Taivan's three snow pictures.
        Sprite picture = block == PlanetOne.SideBlock.Ice ? planet.IceSprite : null;
        int iceLook = 0;
        if (block == PlanetOne.SideBlock.Snow && planet.SnowSprite != null)
        {
            int look = planet.SnowLookAt(x, worldRow, level);
            picture = planet.SnowLookSprite(look) ?? planet.Square;
            // Kept apart from one another in the key below by their look.
            iceLook = look + 1;
        }
        if (picture != null) color = Color.white;

        // Ice without a drawn picture uses the game's own finely drawn ice blocks. Most are plain; about one in three
        // carries a glint or a crack. The block is tinted toward a deeper blue the further down it is.
        if (picture == null && block == PlanetOne.SideBlock.Ice)
        {
            int roll = (hash >> 17) & 15;
            int marked = planet.IceSideSprites.Length - 1;
            iceLook = 1 + (roll < 5 && marked > 0 ? 1 + roll % marked : 0);
            picture = planet.IceSideSprites[iceLook - 1];
            color = Color.Lerp(Color.white, IceDepthTint, (depth - 1) / 3f);
        }

        Color32 c = color;
        // Pictured blocks are mostly drawn white, so the kind of block and its look are part of the key to keep them apart.
        int key = (c.r << 16) | (c.g << 8) | c.b | (picture != null ? (int)block << 24 : 0) | (iceLook << 27);
        if (!tiles.TryGetValue(key, out Tile tile))
        {
            tile = PlanetOne.MakeTile(color, picture != null ? picture : planet.Square, true);
            tiles[key] = tile;
        }
        return tile;
    }

    void OnGUI()
    {
        if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
        string text = "[F] Mining mode, along the row    [R] Mining mode, along the column    [Space] jump";
        if (Active && !Column) text = "MINING MODE, ROW    [F] back to top-down    [R] turn to the column    [Space] jump    [W] row behind    [S] row in front    [Left click] mine";
        if (Active && Column) text = "MINING MODE, COLUMN    [R] back to top-down    [F] turn to the row    [Space] jump    [W] column behind    [S] column in front    [Left click] mine";
        if (Active && Time.time < blockedUntil) text = "MINING MODE    Something is in the way there";
        labelStyle.normal.textColor = Color.black;
        GUI.Label(new Rect(18, 14, 1400, 32), text, labelStyle);
        labelStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(16, 12, 1400, 32), text, labelStyle);
    }
}
