using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

// Builds the frozen crash planet when the scene starts: ground, rock, the wrecked ship, the player and the camera.
// Every spot has a height in blocks. The top-down view is angled: higher ground is drawn shifted up the screen,
// with a cliff face showing on its south side. Mining mode uses the same heights to draw a row from the side.
public class PlanetOne : MonoBehaviour
{
    public int width = 160;
    public int height = 160;
    public int seed = 1;

    // What one block of a row looks like from the side in mining mode.
    public enum SideBlock : byte { Air, Snow, Ice, Stone, Rock, RockTop, Ship }

    // How far up the screen one block of height is drawn in the top-down view, in tiles.
    public const float HeightScale = 0.5f;
    // The tallest anything gets: rock standing 2 blocks above raised ground.
    public const int MaxTop = 3;

    const byte Snow = 0, Ice = 1, Rock = 2, Ship = 3;
    const int TilePixels = 16;
    const int FacePixels = 8;
    // The snow picture is drawn with this many pixels for every ordinary tile pixel (8 x 16 = 128 across a tile).
    const int SnowPictureDetail = 8;
    // Grey rocks and the ship stand this many blocks above the highest ground next to them, so a 1.5 block jump never clears them.
    const int RockHeight = 2;
    const int MaxIceDepth = 4;
    const int Blocked = 99;

    public static readonly Color[] RockColors = { new Color32(93, 107, 126, 255), new Color32(84, 98, 117, 255) };
    public static readonly Color ShipColor = new Color32(120, 128, 140, 255);
    public static readonly Color ShipSideColor = new Color32(84, 91, 102, 255);
    // Snow and ice are a little brighter on raised ground, which helps the two levels read apart.
    static readonly Color32[][] SnowColors =
    {
        new[] { new Color32(226, 235, 243, 255), new Color32(219, 229, 239, 255), new Color32(231, 239, 246, 255) },
        new[] { new Color32(245, 249, 252, 255), new Color32(240, 245, 250, 255), new Color32(249, 252, 254, 255) },
    };
    static readonly Color32[][] IceColors =
    {
        new[] { new Color32(159, 201, 224, 255), new Color32(148, 192, 217, 255), new Color32(168, 207, 228, 255) },
        new[] { new Color32(186, 220, 238, 255), new Color32(177, 213, 233, 255), new Color32(194, 225, 241, 255) },
    };
    static readonly Color32 SnowRim = new Color32(200, 214, 228, 255), SnowFace = new Color32(176, 194, 214, 255), SnowFoot = new Color32(152, 173, 197, 255);
    static readonly Color32 IceRim = new Color32(128, 176, 208, 255), IceFace = new Color32(112, 164, 200, 255), IceFoot = new Color32(92, 142, 181, 255);
    static readonly Color32 RockEdge = new Color32(138, 152, 170, 255);
    static readonly Color32 CliffFace = new Color32(59, 70, 86, 255), CliffCrack = new Color32(48, 58, 73, 255), CliffFoot = new Color32(40, 49, 62, 255);
    static readonly Color32 ShipRim = new Color32(150, 158, 170, 255);
    static readonly Color PlayerColor = new Color32(240, 138, 46, 255);
    static readonly Color SkyColor = new Color32(11, 16, 24, 255);

    // A plain white one-tile sprite; tint it to get a coloured block.
    public Sprite Square { get; private set; }
    // The drawn snow picture as a one-block sprite, or null if there is no picture.
    public Sprite SnowSprite { get; private set; }

    // The snow picture's pixels, for painting it onto top-down tiles.
    Color32[] snowPixels;
    int snowWidth, snowHeight;

    byte[,] kind;
    // Height of the walkable ground at each spot (0 or 1), and the height of whatever stands there (rock and ship are taller).
    byte[,] ground;
    byte[,] top;
    // How many blocks deep the ice and the snow go at each spot, seen from the side.
    byte[,] iceDepth;
    byte[,] snowDepth;
    GameObject topDownRoot;
    readonly Dictionary<int, Tile> terrainTiles = new Dictionary<int, Tile>();

    void Awake()
    {
        Square = PaintSprite((x, y) => new Color32(255, 255, 255, 255));

        // The drawn snow picture, if there is one, is used for the top of snow tiles and for snow blocks in mining mode.
        var snowPicture = Resources.Load<Texture2D>("Art/SnowBlock");
        if (snowPicture != null)
        {
            SnowSprite = Sprite.Create(snowPicture, new Rect(0, 0, snowPicture.width, snowPicture.height), new Vector2(0.5f, 0.5f), snowPicture.width);
            if (snowPicture.isReadable)
            {
                snowPixels = snowPicture.GetPixels32();
                snowWidth = snowPicture.width;
                snowHeight = snowPicture.height;
            }
        }

        int cx = width / 2, cy = height / 2;
        BuildTerrain(cx, cy);

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
        // Things lower on the screen are nearer, so they are drawn over things higher up.
        cam.transparencySortMode = TransparencySortMode.CustomAxis;
        cam.transparencySortAxis = Vector3.up;

        PlayerController controller = BuildPlayer(new Vector2(cx + 0.5f, cy - 0.5f));

        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
        follow.target = controller.transform;
        follow.bounds = new Rect(0f, 0f, width, height);

        var mining = gameObject.AddComponent<MiningMode>();
        var dayNight = gameObject.AddComponent<DayNight>();
        mining.Setup(this, controller, follow, cam, dayNight);
        dayNight.Setup(cam, mining, Square);

        var inventory = new Inventory();
        var hud = gameObject.AddComponent<Hud>();
        hud.Setup(inventory);
        var pickups = gameObject.AddComponent<Pickups>();
        pickups.Setup(this, controller, mining, dayNight, inventory);
        gameObject.AddComponent<Gatherables>().Setup(this, controller, mining, dayNight, pickups, cam);
    }

    PlayerController BuildPlayer(Vector2 position)
    {
        var player = new GameObject("Player");
        player.transform.position = position;
        var box = player.AddComponent<BoxCollider2D>();
        box.size = new Vector2(PlayerController.Width, PlayerController.Height);
        // No friction, so the player does not stick to walls while falling in mining mode.
        box.sharedMaterial = new PhysicsMaterial2D("Player") { friction = 0f, bounciness = 0f };
        var body = player.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // The visual sits at the player's feet and is sorted as one unit, so jumping does not change what covers what.
        var visual = new GameObject("Visual");
        visual.transform.SetParent(player.transform, false);
        visual.transform.localPosition = new Vector3(0f, -PlayerController.Height / 2f, 0f);
        var sorting = visual.AddComponent<SortingGroup>();

        GameObject shadow = MakeBox("Shadow", Square, new Color(0.05f, 0.1f, 0.2f, 0.3f), new Vector2(PlayerController.Width, 0.2f), 0);
        shadow.transform.SetParent(visual.transform, false);
        GameObject bodyVisual = MakeBox("Body", Square, PlayerColor, new Vector2(PlayerController.Width, PlayerController.Height), 1);
        bodyVisual.transform.SetParent(visual.transform, false);

        var controller = player.AddComponent<PlayerController>();
        controller.Setup(this, bodyVisual.transform, shadow.transform, sorting);
        return controller;
    }

    void BuildTerrain(int cx, int cy)
    {
        var rand = new System.Random(seed);
        float ox = (float)rand.NextDouble() * 1000f, oy = (float)rand.NextDouble() * 1000f;
        float hx = (float)rand.NextDouble() * 1000f, hy = (float)rand.NextDouble() * 1000f;

        kind = new byte[width, height];
        ground = new byte[width, height];
        top = new byte[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(width - 1 - x, height - 1 - y));
                float fromCrash = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                float e = Noise(x + ox, y + oy, 26f);
                // Raise the ground into rock near the map edge so the world is walled in.
                if (edge < 8) e += (8 - edge) / 8f * 0.6f;
                // Flatten the land around the crash site so the start is open snow.
                e = 0.5f + (e - 0.5f) * Mathf.Clamp01((fromCrash - 6f) / 6f);

                kind[x, y] = edge == 0 || e > 0.62f ? Rock : e < 0.36f ? Ice : Snow;
                // Patches of ground one block higher. The crash site itself stays at ground level.
                ground[x, y] = (byte)(fromCrash > 8f && Noise(x + hx, y + hy, 17f) > 0.56f ? 1 : 0);
            }
        }
        // The wrecked ship sits just above the spot where the player starts.
        for (int y = cy + 1; y < cy + 3; y++)
        {
            for (int x = cx - 2; x < cx + 3; x++) kind[x, y] = Ship;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                top[x, y] = ground[x, y];
                if (kind[x, y] != Rock && kind[x, y] != Ship) continue;
                int highest = 0;
                for (int ny = y - 1; ny <= y + 1; ny++)
                {
                    for (int nx = x - 1; nx <= x + 1; nx++)
                    {
                        if (nx >= 0 && ny >= 0 && nx < width && ny < height) highest = Mathf.Max(highest, ground[nx, ny]);
                    }
                }
                top[x, y] = (byte)(highest + RockHeight);
            }
        }
        ComputeGroundDepths(ox, oy);

        var grid = new GameObject("Grid").AddComponent<Grid>();
        grid.transform.SetParent(transform, false);
        topDownRoot = grid.gameObject;
        Tilemap terrain = MakeTilemap(grid, "Terrain", 0);
        // Each tile is sorted on its own by the far edge of its square, so the player is drawn over the tile
        // they stand on and behind taller tiles nearer the bottom of the screen.
        terrain.tileAnchor = new Vector3(0.5f, 1f, 0f);
        terrain.GetComponent<TilemapRenderer>().mode = TilemapRenderer.Mode.Individual;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int level = top[x, y];
                int drop = Mathf.Max(0, level - TopAt(x, y - 1, level));
                int rims = (TopAt(x, y + 1, level) < level ? 1 : 0) | (TopAt(x - 1, y, level) < level ? 2 : 0) | (TopAt(x + 1, y, level) < level ? 4 : 0);
                // Plain tiles get one of a few shades; tiles with an edge all share one so the edges line up cleanly.
                // Snow drawn from the picture has four versions instead of three: the picture flipped each way.
                bool pictured = kind[x, y] == Snow && snowPixels != null;
                int variant = rims == 0 && drop == 0 ? rand.Next(pictured ? 4 : 3) : 0;
                terrain.SetTile(new Vector3Int(x, y, 0), TerrainTile(kind[x, y], ground[x, y], level, drop, rims, variant));
            }
        }
    }

    // One tile of the top-down view: the top of the block, plus the cliff face below it when the ground to the south is lower.
    Tile TerrainTile(byte material, int groundLevel, int level, int drop, int rims, int variant)
    {
        int key = material | (groundLevel << 2) | (level << 3) | (drop << 6) | (rims << 9) | (variant << 12);
        if (terrainTiles.TryGetValue(key, out Tile tile)) return tile;

        bool pictured = material == Snow && snowPixels != null;
        Color32 topColor, rimColor;
        switch (material)
        {
            case Ice: topColor = IceColors[groundLevel][variant]; rimColor = IceRim; break;
            case Rock: topColor = RockColors[variant % RockColors.Length]; rimColor = RockEdge; break;
            case Ship: topColor = ShipColor; rimColor = ShipRim; break;
            default: topColor = SnowColors[groundLevel][pictured ? 0 : variant]; rimColor = SnowRim; break;
        }

        int facePixels = drop * FacePixels;
        int spriteHeight = TilePixels + facePixels;
        // The pivot sits where the far edge of the square is on flat ground, so higher blocks are drawn shifted up.
        var pivot = new Vector2(0.5f, (spriteHeight - level * FacePixels) / (float)spriteHeight);
        // Tiles that use the snow picture are painted with several picture pixels for each ordinary tile pixel.
        int detail = pictured ? SnowPictureDetail : 1;
        Sprite sprite = PaintSprite(TilePixels * detail, spriteHeight * detail, pivot, TilePixels * detail, pictured, (fineX, fineY) =>
        {
            int x = fineX / detail, y = fineY / detail;
            if (y >= TilePixels) return PaintFace(material, x, y - TilePixels, facePixels);
            bool rim = ((rims & 1) != 0 && y < 2) || ((rims & 2) != 0 && x < 1) || ((rims & 4) != 0 && x > TilePixels - 2);
            if (rim) return rimColor;
            return pictured ? SnowPicturePixel(fineX, fineY, variant, topColor) : topColor;
        });
        tile = MakeTile(Color.white, sprite, false);
        terrainTiles[key] = tile;
        return tile;
    }

    // One pixel of the top of a snow tile, taken from the snow picture and tinted for its height.
    // The variant flips the picture left-right and top-bottom so neighbouring tiles do not all look alike.
    Color32 SnowPicturePixel(int x, int y, int variant, Color32 tint)
    {
        int size = TilePixels * SnowPictureDetail;
        if ((variant & 1) != 0) x = size - 1 - x;
        if ((variant & 2) != 0) y = size - 1 - y;
        int pictureX = x * snowWidth / size, pictureY = y * snowHeight / size;
        Color32 c = snowPixels[(snowHeight - 1 - pictureY) * snowWidth + pictureX];
        return new Color32((byte)(c.r * tint.r / 255), (byte)(c.g * tint.g / 255), (byte)(c.b * tint.b / 255), 255);
    }

    // The south-facing side of a raised block. y counts down from the top of the face.
    static Color32 PaintFace(byte material, int x, int y, int facePixels)
    {
        bool foot = y >= facePixels - 2;
        switch (material)
        {
            case Ice: return foot ? IceFoot : IceFace;
            case Ship: return ShipSideColor;
            case Rock:
                if (y < 2) return RockEdge;
                if (foot) return CliffFoot;
                bool crack = (x == 3 && y > 4) || (x == 8 && y < 11) || (x == 12 && y > 6);
                return crack ? CliffCrack : CliffFace;
            default: return foot ? SnowFoot : SnowFace;
        }
    }

    // Works out how deep the ice and snow go everywhere. Ice is shallow at the shore and deeper toward the middle,
    // so from the side a patch of ice looks like a lake or river that froze in its bed.
    void ComputeGroundDepths(float ox, float oy)
    {
        iceDepth = new byte[width, height];
        snowDepth = new byte[width, height];

        // Distance from each ice tile to the nearest shore, spreading inward from everything that is not ice.
        var distance = new int[width, height];
        var queue = new Queue<Vector2Int>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (kind[x, y] == Ice) distance[x, y] = int.MaxValue;
                else queue.Enqueue(new Vector2Int(x, y));
            }
        }
        var steps = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
        while (queue.Count > 0)
        {
            Vector2Int p = queue.Dequeue();
            foreach (Vector2Int step in steps)
            {
                Vector2Int n = p + step;
                if (n.x < 0 || n.y < 0 || n.x >= width || n.y >= height) continue;
                if (distance[n.x, n.y] <= distance[p.x, p.y] + 1) continue;
                distance[n.x, n.y] = distance[p.x, p.y] + 1;
                queue.Enqueue(n);
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (kind[x, y] == Ice) iceDepth[x, y] = (byte)Mathf.Min(distance[x, y], MaxIceDepth);
                // Snow is 4 or 5 blocks deep, changing gently from place to place.
                snowDepth[x, y] = (byte)(Mathf.PerlinNoise((x + ox) * 0.13f, (y + oy) * 0.13f) > 0.5f ? 5 : 4);
            }
        }
    }

    bool InBounds(int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    // Everything that belongs to the top-down view hangs under this.
    public Transform TopDownRoot => topDownRoot.transform;

    public bool IsSnow(int x, int y)
    {
        return InBounds(x, y) && kind[x, y] == Snow;
    }

    public bool IsIce(Vector2 position)
    {
        int x = Mathf.FloorToInt(position.x), y = Mathf.FloorToInt(position.y);
        return InBounds(x, y) && kind[x, y] == Ice;
    }

    // Height in blocks of whatever is at a spot. Anything past the edge of the map counts as too tall to enter.
    public int TopAt(int x, int y)
    {
        return TopAt(x, y, Blocked);
    }

    int TopAt(int x, int y, int outside)
    {
        return InBounds(x, y) ? top[x, y] : outside;
    }

    // Height of the snow or ice surface at a spot, ignoring any rock or ship standing on it.
    public int GroundAt(int x, int y)
    {
        return InBounds(x, y) ? ground[x, y] : 0;
    }

    // The block at one spot of a row seen from the side. Level 0 is the first block above the lowest ground,
    // and negative levels are underground.
    public SideBlock SideBlockAt(int x, int row, int level)
    {
        if (!InBounds(x, row) || level >= top[x, row]) return SideBlock.Air;

        byte k = kind[x, row];
        int surface = ground[x, row];
        if (k == Rock)
        {
            if (level < surface) return SideBlock.Stone;
            return level == top[x, row] - 1 ? SideBlock.RockTop : SideBlock.Rock;
        }
        if (level >= surface) return SideBlock.Ship;

        int depth = surface - level;
        if (k == Ice && depth <= iceDepth[x, row]) return SideBlock.Ice;
        return depth <= snowDepth[x, row] ? SideBlock.Snow : SideBlock.Stone;
    }

    // Hides or shows everything that belongs to the top-down view (the player and camera are shared).
    public void SetTopDownVisible(bool visible)
    {
        topDownRoot.SetActive(visible);
    }

    // Pixel position inside one tile, with (0, 0) at the top left. Rims are the lighter edges of a rock top.
    public static Color32 PaintRockTop(int x, int y, int rims)
    {
        bool lit = ((rims & 1) != 0 && y < 2) || ((rims & 2) != 0 && x < 1) || ((rims & 4) != 0 && x > TilePixels - 2);
        return lit ? RockEdge : (Color32)RockColors[0];
    }

    // Smooth blobs of noise: big shapes with smaller detail layered on top. Returns roughly 0 to 1.
    static float Noise(float x, float y, float size)
    {
        float sum = 0f, amplitude = 0.5f, total = 0f, scale = 1f / size;
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
    public static Sprite PaintSprite(System.Func<int, int, Color32> paint)
    {
        return PaintSprite(TilePixels, TilePixels, new Vector2(0.5f, 0.5f), paint);
    }

    // Makes a sprite of any size. The paint function gets pixel positions with (0, 0) at the top left.
    public static Sprite PaintSprite(int pixelWidth, int pixelHeight, Vector2 pivot, System.Func<int, int, Color32> paint)
    {
        return PaintSprite(pixelWidth, pixelHeight, pivot, TilePixels, paint);
    }

    // As above, with a chosen number of pixels per block instead of the usual 16.
    public static Sprite PaintSprite(int pixelWidth, int pixelHeight, Vector2 pivot, float pixelsPerUnit, System.Func<int, int, Color32> paint)
    {
        return PaintSprite(pixelWidth, pixelHeight, pivot, pixelsPerUnit, false, paint);
    }

    // As above. Smooth is for detailed pictures that are shown smaller than they are painted; chunky pixel art leaves it off.
    public static Sprite PaintSprite(int pixelWidth, int pixelHeight, Vector2 pivot, float pixelsPerUnit, bool smooth, System.Func<int, int, Color32> paint)
    {
        var texture = new Texture2D(pixelWidth, pixelHeight) { filterMode = smooth ? FilterMode.Bilinear : FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[pixelWidth * pixelHeight];
        for (int y = 0; y < pixelHeight; y++)
        {
            for (int x = 0; x < pixelWidth; x++) pixels[(pixelHeight - 1 - y) * pixelWidth + x] = paint(x, y);
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, pixelWidth, pixelHeight), pivot, pixelsPerUnit);
    }

    public static Tile MakeTile(Color color, Sprite sprite, bool isSolid)
    {
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.color = color;
        tile.colliderType = isSolid ? Tile.ColliderType.Grid : Tile.ColliderType.None;
        return tile;
    }

    public static Tilemap MakeTilemap(Grid grid, string name, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(grid.transform, false);
        var tilemap = go.AddComponent<Tilemap>();
        go.AddComponent<TilemapRenderer>().sortingOrder = sortingOrder;
        return tilemap;
    }

    static GameObject MakeBox(string name, Sprite sprite, Color color, Vector2 size, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return go;
    }
}
