using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Builds the frozen crash planet when the scene starts: ground, rock cliffs, the wrecked ship, the player and the camera.
// The view is angled: rock is raised ground, and its south-facing edges show a cliff face.
public class PlanetOne : MonoBehaviour
{
    public int width = 160;
    public int height = 160;
    public int seed = 1;

    const byte Snow = 0, Ice = 1, Rock = 2;
    const int TilePixels = 16;

    static readonly Color[] SnowColors = { new Color32(230, 238, 245, 255), new Color32(222, 232, 241, 255), new Color32(236, 243, 248, 255) };
    static readonly Color[] IceColors = { new Color32(159, 201, 224, 255), new Color32(148, 192, 217, 255), new Color32(170, 209, 230, 255) };
    static readonly Color[] RockColors = { new Color32(93, 107, 126, 255), new Color32(84, 98, 117, 255) };
    static readonly Color32 RockEdge = new Color32(138, 152, 170, 255);
    static readonly Color32 CliffFace = new Color32(59, 70, 86, 255);
    static readonly Color32 CliffCrack = new Color32(48, 58, 73, 255);
    static readonly Color32 CliffFoot = new Color32(40, 49, 62, 255);
    static readonly Color ShipColor = new Color32(120, 128, 140, 255);
    static readonly Color ShipSideColor = new Color32(84, 91, 102, 255);
    static readonly Color PlayerColor = new Color32(240, 138, 46, 255);
    static readonly Color SkyColor = new Color32(11, 16, 24, 255);

    byte[,] kind;

    void Awake()
    {
        Sprite square = PaintSprite((x, y) => new Color32(255, 255, 255, 255));

        int cx = width / 2, cy = height / 2;
        BuildTerrain(square, cx, cy);

        // The wrecked ship sits just above the spot where the player starts. The darker strip is the side of its hull.
        GameObject ship = MakeBox("Crashed Ship", square, ShipColor, new Vector2(cx + 0.5f, cy + 2f), new Vector2(5f, 2f), 3);
        ship.AddComponent<BoxCollider2D>();
        MakeBox("Crashed Ship Side", square, ShipSideColor, new Vector2(cx + 0.5f, cy + 1.35f), new Vector2(5f, 0.7f), 4);

        GameObject player = MakeBox("Player", square, PlayerColor, new Vector2(cx + 0.5f, cy - 0.5f), new Vector2(0.6f, 0.9f), 5);
        player.AddComponent<BoxCollider2D>();
        var body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        player.AddComponent<PlayerController>().planet = this;

        Camera cam = Camera.main;
        if (cam == null)
        {
            var camObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = camObject.AddComponent<Camera>();
            camObject.AddComponent<AudioListener>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 7f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = SkyColor;
        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
        follow.target = player.transform;
        follow.bounds = new Rect(0f, 0f, width, height);
    }

    void BuildTerrain(Sprite square, int cx, int cy)
    {
        var rand = new System.Random(seed);
        float ox = (float)rand.NextDouble() * 1000f, oy = (float)rand.NextDouble() * 1000f;

        var grid = new GameObject("Grid").AddComponent<Grid>();
        grid.transform.SetParent(transform, false);
        Tilemap ground = MakeTilemap(grid, "Ground", 0);
        Tilemap shadows = MakeTilemap(grid, "Shadows", 1);
        Tilemap solid = MakeTilemap(grid, "Solid", 2);

        // One merged collider for everything solid, so the player slides along walls without catching on tile edges.
        var solidBody = solid.gameObject.AddComponent<Rigidbody2D>();
        solidBody.bodyType = RigidbodyType2D.Static;
        var tileCollider = solid.gameObject.AddComponent<TilemapCollider2D>();
        var composite = solid.gameObject.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

        Tile[] snowTiles = MakeTiles(SnowColors, square, false);
        Tile[] iceTiles = MakeTiles(IceColors, square, false);
        Tile[] rockTiles = MakeTiles(RockColors, square, true);
        Tile rockFloor = MakeTile(RockColors[0], square, false);
        Tile cliffTile = MakeTile(Color.white, PaintSprite(PaintCliffFace), true);
        Tile shadowTile = MakeTile(Color.white, PaintSprite(PaintCliffShadow), false);
        var rockEdgeTiles = new Dictionary<int, Tile>();

        kind = new byte[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(width - 1 - x, height - 1 - y));
                float e = Elevation(x + ox, y + oy);
                // Raise the ground into rock near the map edge so the world is walled in.
                if (edge < 8) e += (8 - edge) / 8f * 0.6f;
                // Flatten the land around the crash site so the start is open snow.
                float open = Mathf.Clamp01((Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) - 6f) / 6f);
                e = 0.5f + (e - 0.5f) * open;

                kind[x, y] = edge == 0 || e > 0.62f ? Rock : e < 0.36f ? Ice : Snow;
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                byte k = kind[x, y];
                if (k != Rock)
                {
                    ground.SetTile(cell, k == Ice ? iceTiles[rand.Next(iceTiles.Length)] : snowTiles[rand.Next(snowTiles.Length)]);
                    if (IsRock(kind, x, y + 1)) shadows.SetTile(cell, shadowTile);
                    continue;
                }

                ground.SetTile(cell, rockFloor);
                if (!IsRock(kind, x, y - 1))
                {
                    // Open ground to the south: this is the front of the cliff.
                    solid.SetTile(cell, cliffTile);
                    continue;
                }

                // Top of the rock. Edges that drop away to the north, west or east get a lighter rim.
                int rims = (IsRock(kind, x, y + 1) ? 0 : 1) | (IsRock(kind, x - 1, y) ? 0 : 2) | (IsRock(kind, x + 1, y) ? 0 : 4);
                if (rims == 0)
                {
                    solid.SetTile(cell, rockTiles[rand.Next(rockTiles.Length)]);
                    continue;
                }
                if (!rockEdgeTiles.TryGetValue(rims, out Tile rimTile))
                {
                    int flags = rims;
                    rimTile = MakeTile(Color.white, PaintSprite((px, py) => PaintRockTop(px, py, flags)), true);
                    rockEdgeTiles[rims] = rimTile;
                }
                solid.SetTile(cell, rimTile);
            }
        }
    }

    public bool IsIce(Vector2 position)
    {
        int x = Mathf.FloorToInt(position.x), y = Mathf.FloorToInt(position.y);
        return x >= 0 && y >= 0 && x < width && y < height && kind[x, y] == Ice;
    }

    // Anything past the edge of the map counts as rock.
    bool IsRock(byte[,] kind, int x, int y)
    {
        return x < 0 || y < 0 || x >= width || y >= height || kind[x, y] == Rock;
    }

    // The paint functions take a pixel position inside one tile, with (0, 0) at the top left.
    static Color32 PaintRockTop(int x, int y, int rims)
    {
        bool lit = ((rims & 1) != 0 && y < 2) || ((rims & 2) != 0 && x < 1) || ((rims & 4) != 0 && x > TilePixels - 2);
        return lit ? RockEdge : (Color32)RockColors[0];
    }

    static Color32 PaintCliffFace(int x, int y)
    {
        if (y < 2) return RockEdge;
        if (y >= TilePixels - 2) return CliffFoot;
        bool crack = (x == 3 && y > 4) || (x == 8 && y < 11) || (x == 12 && y > 6);
        return crack ? CliffCrack : CliffFace;
    }

    static Color32 PaintCliffShadow(int x, int y)
    {
        return new Color32(20, 35, 60, (byte)Mathf.Max(0, 80 - y * 16));
    }

    // Smooth hills and valleys: big shapes with smaller detail layered on top. Returns roughly 0 to 1.
    static float Elevation(float x, float y)
    {
        float sum = 0f, amplitude = 0.5f, total = 0f, scale = 1f / 26f;
        for (int i = 0; i < 3; i++)
        {
            sum += Mathf.PerlinNoise(x * scale, y * scale) * amplitude;
            total += amplitude;
            amplitude *= 0.5f;
            scale *= 2f;
        }
        return sum / total;
    }

    // Makes a one-tile sprite by asking the paint function for each pixel.
    static Sprite PaintSprite(System.Func<int, int, Color32> paint)
    {
        var texture = new Texture2D(TilePixels, TilePixels) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[TilePixels * TilePixels];
        for (int y = 0; y < TilePixels; y++)
        {
            for (int x = 0; x < TilePixels; x++) pixels[(TilePixels - 1 - y) * TilePixels + x] = paint(x, y);
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, TilePixels, TilePixels), new Vector2(0.5f, 0.5f), TilePixels);
    }

    static Tile MakeTile(Color color, Sprite sprite, bool isSolid)
    {
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.color = color;
        tile.colliderType = isSolid ? Tile.ColliderType.Grid : Tile.ColliderType.None;
        return tile;
    }

    static Tile[] MakeTiles(Color[] colors, Sprite sprite, bool isSolid)
    {
        var tiles = new Tile[colors.Length];
        for (int i = 0; i < colors.Length; i++) tiles[i] = MakeTile(colors[i], sprite, isSolid);
        return tiles;
    }

    static Tilemap MakeTilemap(Grid grid, string name, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(grid.transform, false);
        var tilemap = go.AddComponent<Tilemap>();
        go.AddComponent<TilemapRenderer>().sortingOrder = sortingOrder;
        return tilemap;
    }

    static GameObject MakeBox(string name, Sprite sprite, Color color, Vector2 position, Vector2 size, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return go;
    }
}
