using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Mining mode: press R to see the row of the world the player is standing on from the side, as a 2D side-scroller.
// The next few rows further back are drawn faded behind it. Press R again to go back to the top-down view.
public class MiningMode : MonoBehaviour
{
    // How many blocks of ground are shown below the surface, and how much sky the camera may show above it.
    const int Depth = 14;
    const int SkyHeight = 12;

    static readonly Color MiningSky = new Color32(24, 36, 58, 255);
    static readonly Color[] BehindTints = { new Color(0.62f, 0.68f, 0.78f), new Color(0.45f, 0.5f, 0.6f), new Color(0.32f, 0.36f, 0.46f) };
    static readonly Color SnowTop = new Color32(234, 241, 247, 255), SnowDeep = new Color32(188, 205, 223, 255);
    static readonly Color IceTop = new Color32(170, 209, 230, 255), IceDeep = new Color32(108, 158, 194, 255);
    static readonly Color Stone = new Color32(70, 80, 96, 255);

    public bool Active { get; private set; }

    PlanetOne planet;
    PlayerController player;
    CameraFollow follow;
    Camera cam;
    Color topDownSky;

    GameObject root;
    Tilemap rowMap;
    Tilemap[] behindMaps;
    TilemapCollider2D tileCollider;
    CompositeCollider2D composite;
    Tile rockTopTile;
    readonly Dictionary<Color32, Tile> tiles = new Dictionary<Color32, Tile>();
    int row;
    float blockedUntil;
    GUIStyle labelStyle;

    public void Setup(PlanetOne planet, PlayerController player, CameraFollow follow, Camera cam)
    {
        this.planet = planet;
        this.player = player;
        this.follow = follow;
        this.cam = cam;

        var grid = new GameObject("Mining View").AddComponent<Grid>();
        grid.transform.SetParent(transform, false);
        root = grid.gameObject;

        // Furthest row is drawn first so nearer rows cover it.
        behindMaps = new Tilemap[BehindTints.Length];
        for (int i = 0; i < behindMaps.Length; i++)
        {
            behindMaps[i] = PlanetOne.MakeTilemap(grid, "Row Behind " + (i + 1), behindMaps.Length - i);
            behindMaps[i].color = BehindTints[i];
        }
        rowMap = PlanetOne.MakeTilemap(grid, "Row", behindMaps.Length + 1);

        var body = rowMap.gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        tileCollider = rowMap.gameObject.AddComponent<TilemapCollider2D>();
        composite = rowMap.gameObject.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

        rockTopTile = PlanetOne.MakeTile(Color.white, PlanetOne.PaintSprite((x, y) => PlanetOne.PaintRockTop(x, y, 1)), true);
        root.SetActive(false);
    }

    void Update()
    {
        if (GameInput.ToggleViewPressed()) Toggle();
        if (!Active) return;
        if (GameInput.RowBehindPressed()) ShiftRow(1);
        else if (GameInput.RowInFrontPressed()) ShiftRow(-1);
    }

    public void Toggle()
    {
        Active = !Active;
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
            cam.backgroundColor = MiningSky;
        }
        else
        {
            root.SetActive(false);
            planet.SetTopDownVisible(true);
            player.ExitSideView();
            follow.bounds = new Rect(0f, 0f, planet.width, planet.height);
            cam.backgroundColor = topDownSky;
        }
    }

    // Steps the player one row back (+1) or forward (-1), if nothing in that row is in the way where they are.
    // Returns false and does nothing when the space is taken.
    public bool ShiftRow(int direction)
    {
        int target = row + direction;
        Vector2 position = player.transform.position;
        int x0 = Mathf.FloorToInt(position.x - PlayerController.Width / 2f + 0.001f);
        int x1 = Mathf.FloorToInt(position.x + PlayerController.Width / 2f - 0.001f);
        for (int x = x0; x <= x1; x++)
        {
            if (planet.TopAt(x, target) > player.FeetHeight + 0.05f)
            {
                blockedUntil = Time.time + 0.7f;
                return false;
            }
        }

        row = target;
        Build();
        player.SetSideRow(row);
        return true;
    }

    void Build()
    {
        FillRow(rowMap, row);
        for (int i = 0; i < behindMaps.Length; i++) FillRow(behindMaps[i], row + 1 + i);
        // Make the new ground solid right away instead of waiting for the end of the frame.
        tileCollider.ProcessTilemapChanges();
        composite.GenerateGeometry();
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

        Color32 key = color;
        if (!tiles.TryGetValue(key, out Tile tile))
        {
            tile = PlanetOne.MakeTile(color, planet.Square, true);
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
