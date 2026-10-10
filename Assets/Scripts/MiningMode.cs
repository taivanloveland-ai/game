using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Mining mode: press R to see the row of the world the player is standing on from the side, as a 2D side-scroller.
// The next few rows further back are drawn dimmer and a little higher behind it. W and S step between rows:
// the rows slide forward or back, and the one arriving at the front brightens into the new front row.
// Press R again to go back to the top-down view.
public class MiningMode : MonoBehaviour
{
    // How many blocks of ground are shown below the surface, and how much sky the camera may show above it.
    const int Depth = 14;
    const int SkyHeight = 12;
    // How long stepping to the next row takes. Walking one block top-down takes about this long.
    const float RowStepSeconds = 0.25f;
    // Each row further back is drawn this much higher, in blocks, so rows read as being behind one another.
    const float RowOffset = 0.2f;
    // Light reaches this many blocks into the ground from open air, fading as it goes. Past that everything is black.
    const float LightDepth = 1.5f;
    // The darkness is painted at this many pixels per block.
    const int DarkPixels = 8;

    // How much dimmer each row behind the front one is, nearest first.
    static readonly Color[] BehindTints = { new Color(0.62f, 0.68f, 0.78f), new Color(0.45f, 0.5f, 0.6f), new Color(0.32f, 0.36f, 0.46f) };
    static readonly Color SnowTop = new Color32(234, 241, 247, 255), SnowDeep = new Color32(188, 205, 223, 255);
    static readonly Color IceTop = new Color32(146, 202, 232, 255), IceDeep = new Color32(92, 150, 198, 255);
    // What the game's own ice blocks are multiplied by at their deepest, taking the surface blue down to the deep blue.
    static readonly Color IceDepthTint = new Color(0.63f, 0.74f, 0.85f);
    static readonly Color Stone = new Color32(70, 80, 96, 255);

    // Drawing order, back to front: the rows behind (1 to 3, with 0 for one sliding in at the back), the front row (4),
    // its darkness (5), a row sliding in at the front (6) and its darkness (7), then things standing in the row.
    const int FrontOrder = 4;
    const int DarkOrder = 5;
    const int IncomingFrontOrder = 6;
    const int IncomingDarkOrder = 7;
    // Things standing in the row (plants, dropped items) are drawn over the ground and its darkness, under the player.
    public const int ObjectSortingOrder = 8;

    public bool Active { get; private set; }
    // The row of the world the side view is showing.
    public int Row => row;
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
    int row;
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
            behindMaps[i] = PlanetOne.MakeTilemap(grid, "Row Behind " + (i + 1), FrontOrder - 1 - i);
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

        int columns = planet.width, levels = Depth + PlanetOne.MaxTop;
        covered = new bool[columns, levels];
        darkPixels = new Color32[columns * DarkPixels * levels * DarkPixels];
        dark = MakeDarkness("Darkness", DarkOrder);
        incomingDark = MakeDarkness("Darkness Arriving", IncomingDarkOrder);

        root.SetActive(false);
    }

    SpriteRenderer MakeDarkness(string name, int sortingOrder)
    {
        var texture = new Texture2D(planet.width * DarkPixels, (Depth + PlanetOne.MaxTop) * DarkPixels) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero, DarkPixels);
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    void Update()
    {
        if (GameInput.ToggleViewPressed()) Toggle();
        if (!Active) return;
        if (stepDirection != 0) UpdateRowStep();
        else if (GameInput.RowBehindHeld()) BeginRowStep(1);
        else if (GameInput.RowInFrontHeld()) BeginRowStep(-1);
        ApplyLight();
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
        FillRow(spareMap, direction > 0 ? row + behindMaps.Length + 1 : row - 1);
        spareMap.GetComponent<TilemapRenderer>().sortingOrder = direction > 0 ? 0 : IncomingFrontOrder;
        PaintDarkness(incomingDark.sprite.texture, row + direction);
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
        dark.transform.localPosition = new Vector3(0f, -Depth - shift * RowOffset, 0f);
        dark.color = new Color(1f, 1f, 1f, 1f - amount * amount * amount);
        incomingDark.transform.localPosition = new Vector3(0f, -Depth + (stepDirection - shift) * RowOffset, 0f);
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

    public void Toggle()
    {
        Active = !Active;
        stepDirection = 0;
        spareMap.ClearAllTiles();
        if (Active)
        {
            // The row is the one the player's feet are in.
            row = Mathf.FloorToInt(player.transform.position.y - PlayerController.Height / 2f + 0.2f);
            topDownSky = cam.backgroundColor;
            planet.SetTopDownVisible(false);
            root.SetActive(true);
            Build();
            player.EnterSideView(row);
            follow.bounds = new Rect(0f, -Depth, planet.width, Depth + SkyHeight);
            PoseRows(0f);
            ApplyLight();
        }
        else
        {
            root.SetActive(false);
            planet.SetTopDownVisible(true);
            player.ExitSideView();
            // Top-down night is a wash over the whole screen, so the player goes back to their own colour.
            player.SetTint(Color.white);
            follow.bounds = new Rect(0f, 0f, planet.width, planet.height);
            cam.backgroundColor = topDownSky;
        }
    }

    // True if nothing in the row behind (+1) or in front (-1) is in the way where the player is.
    bool CanShiftRow(int direction)
    {
        Vector2 position = player.transform.position;
        int x0 = Mathf.FloorToInt(position.x - PlayerController.Width / 2f + 0.001f);
        int x1 = Mathf.FloorToInt(position.x + PlayerController.Width / 2f - 0.001f);
        for (int x = x0; x <= x1; x++)
        {
            if (planet.TopAt(x, row + direction) > player.FeetHeight + 0.05f) return false;
        }
        return true;
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

        row += direction;
        Build();
        player.SetSideRow(row);
        return true;
    }

    void Build()
    {
        FillRow(rowMap, row);
        FillRow(solidMap, row);
        for (int i = 0; i < behindMaps.Length; i++) FillRow(behindMaps[i], row + 1 + i);
        // Make the new ground solid right away instead of waiting for the end of the frame.
        tileCollider.ProcessTilemapChanges();
        composite.GenerateGeometry();
        PaintDarkness(dark.sprite.texture, row);
        RowBuilt?.Invoke();
    }

    // Paints the darkness for one row: its ground is blacked out except near open air, fully lit at the surface
    // and fading to black LightDepth blocks in. Any side of a block that touches air lets light in.
    void PaintDarkness(Texture2D texture, int forRow)
    {
        int columns = covered.GetLength(0), levels = covered.GetLength(1);
        for (int x = 0; x < columns; x++)
        {
            for (int l = 0; l < levels; l++)
            {
                // A block can only be shaded if the row in front has a block covering it. With nothing in front it is
                // in the open, so it stays fully lit and lets light into its neighbours just like air does.
                covered[x, l] = planet.SideBlockAt(x, forRow, l - Depth) != PlanetOne.SideBlock.Air &&
                    planet.SideBlockAt(x, forRow - 1, l - Depth) != PlanetOne.SideBlock.Air;
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

    void FillRow(Tilemap map, int worldRow)
    {
        map.ClearAllTiles();
        for (int x = 0; x < planet.width; x++)
        {
            for (int level = -Depth; level < PlanetOne.MaxTop; level++)
            {
                PlanetOne.SideBlock block = planet.SideBlockAt(x, worldRow, level);
                if (block != PlanetOne.SideBlock.Air) map.SetTile(new Vector3Int(x, level, 0), TileFor(block, x, worldRow, level));
            }
        }
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
            case PlanetOne.SideBlock.Ship: color = level == surface ? PlanetOne.ShipSideColor : PlanetOne.ShipColor; break;
            default: color = Stone; break;
        }
        // Slight block-to-block variation so large areas are not one flat colour.
        int hash = (x * 73856093) ^ (level * 19349663) ^ (worldRow * 83492791);
        hash = (hash ^ (hash >> 13)) * 0x5bd1e995;
        bool darker = ((hash >> 15) & 1) == 1;
        if (darker && block != PlanetOne.SideBlock.Ship) color *= 0.965f;
        color.a = 1f;

        // Snow and ice blocks use their drawn pictures when there are any, exactly as drawn: no colouring and no flipping.
        Sprite picture = block == PlanetOne.SideBlock.Snow ? planet.SnowSprite : block == PlanetOne.SideBlock.Ice ? planet.IceSprite : null;
        if (picture != null) color = Color.white;

        // Ice without a drawn picture uses the game's own finely drawn ice blocks. Most are plain; about one in three
        // carries a glint or a crack. The block is tinted toward a deeper blue the further down it is.
        int iceLook = 0;
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
        string text = "[R] Mining mode    [Space] jump";
        if (Active) text = "MINING MODE    [R] back to top-down    [Space] jump    [W] row behind    [S] row in front";
        if (Active && Time.time < blockedUntil) text = "MINING MODE    Something is in the way in that row";
        labelStyle.normal.textColor = Color.black;
        GUI.Label(new Rect(18, 14, 1400, 32), text, labelStyle);
        labelStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(16, 12, 1400, 32), text, labelStyle);
    }
}
