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
    // The world is different every time the game starts. Untick this to always build the world from the seed below.
    public bool randomWorld = true;
    // The number the world is built from. The same seed always gives the same world.
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
    // A drawn picture is painted with this many pixels for every ordinary tile pixel (8 x 16 = 128 across a tile).
    const int PictureDetail = 8;
    // Glints and cracks in the ice are thin lines this many fine pixels either side of their centre (3 pixels wide).
    const int IceMarkHalfWidth = 1;
    // Grey rocks and the ship stand this many blocks above the highest ground next to them, so a 1.5 block jump never clears them.
    const int RockHeight = 2;
    const int MaxIceDepth = 4;
    // A random world must let the player walk to at least this share of its snow and ice from the crash site.
    const float MinReachableShare = 0.8f;
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
    // Ice: a clear blue, with pale glints and the odd darker crack. The first of each pair is ground level, the second raised.
    static readonly Color32[] IceBase = { new Color32(146, 202, 232, 255), new Color32(170, 217, 240, 255) };
    static readonly Color32[] IceGlint = { new Color32(206, 235, 248, 255), new Color32(226, 244, 252, 255) };
    static readonly Color32[] IceCrack = { new Color32(112, 174, 212, 255), new Color32(136, 192, 224, 255) };
    // Each ice tile is one of these looks, picked at random so a lake never shows a repeating pattern. Most are plain;
    // a few carry short marks. A mark is { x, y, length, sideways step, kind }: it starts at (x, y) and climbs one pixel
    // per step, moving sideways by the step each time. Kind 1 is a glint, kind 2 a crack.
    static readonly int[][][] IceMarks =
    {
        new int[0][],
        new int[0][],
        new int[0][],
        new int[0][],
        new int[0][],
        new[] { new[] { 3, 11, 4, 1, 1 }, new[] { 10, 6, 2, 1, 1 } },
        new[] { new[] { 7, 13, 5, 1, 1 } },
        new[] { new[] { 2, 6, 3, 1, 1 }, new[] { 11, 11, 3, 1, 1 } },
        new[] { new[] { 4, 12, 5, 1, 2 }, new[] { 8, 8, 3, -1, 2 } },
        new[] { new[] { 12, 5, 2, 1, 1 } },
    };
    static readonly Color32 SnowRim = new Color32(200, 214, 228, 255), SnowFace = new Color32(176, 194, 214, 255), SnowFoot = new Color32(152, 173, 197, 255);
    static readonly Color32 IceRim = new Color32(200, 232, 246, 255), IceFace = new Color32(104, 166, 206, 255), IceFoot = new Color32(84, 144, 188, 255);
    static readonly Color32 RockEdge = new Color32(138, 152, 170, 255);
    static readonly Color32 CliffFace = new Color32(59, 70, 86, 255), CliffCrack = new Color32(48, 58, 73, 255), CliffFoot = new Color32(40, 49, 62, 255);
    static readonly Color32 ShipRim = new Color32(150, 158, 170, 255);
    static readonly Color PlayerColor = new Color32(240, 138, 46, 255);
    static readonly Color SkyColor = new Color32(11, 16, 24, 255);

    // A plain white one-tile sprite; tint it to get a coloured block.
    public Sprite Square { get; private set; }
    // The drawn pictures for snow and ice as one-block sprites, or null where there is no picture.
    public Sprite SnowSprite => snowPicture?.Sprite;
    public Sprite IceSprite => icePicture?.Sprite;
    // Ice blocks drawn by the game for mining mode: plain ice first, then one for each marked look.
    public Sprite[] IceSideSprites { get; private set; }

    // A drawn picture of a block: as a sprite for mining mode, and as pixels for painting onto top-down tiles.
    class BlockPicture
    {
        public Sprite Sprite;
        public Color32[] Pixels;
        public int Width, Height;
    }

    BlockPicture snowPicture, icePicture;

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

        // Drawn pictures, where they exist, are used for the top of tiles here and for blocks in mining mode.
        snowPicture = LoadBlockPicture("Art/SnowBlock");
        icePicture = LoadBlockPicture("Art/IceBlock");
        IceSideSprites = MakeIceSideSprites();

        int cx = width / 2, cy = height / 2;
        var picker = new System.Random();
        if (randomWorld) seed = picker.Next(1, 1000000);
        System.Random rand = GenerateLand(cx, cy);
        // A random world is thrown away and picked again if rock walls off too much of it from the crash site.
        for (int tries = 0; randomWorld && tries < 20 && ReachableShare(cx, cy) < MinReachableShare; tries++)
        {
            seed = picker.Next(1, 1000000);
            rand = GenerateLand(cx, cy);
        }
        // Logged so a world worth keeping can be built again by typing its seed in and unticking Random World.
        Debug.Log("World seed: " + seed + " (" + Mathf.RoundToInt(ReachableShare(cx, cy) * 100f) + "% of its snow and ice can be walked to)");
        BuildTerrain(rand);

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
        var gatherables = gameObject.AddComponent<Gatherables>();
        gatherables.Setup(this, controller, mining, dayNight, pickups, cam);
        // The crafting nook is set into the middle of the ship's front.
        gameObject.AddComponent<CraftingNook>().Setup(this, controller, mining, gatherables, inventory, hud, cam, new Vector2Int(cx, cy + 1));
        // The maps mark the ship at the middle of its hull.
        gameObject.AddComponent<WorldMap>().Setup(this, controller, new Vector2(cx + 0.5f, cy + 2f));
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

    // Works out the shape of the world from the seed: what each spot is made of and how high it is.
    // Nothing is drawn yet. Returns the random number source, which the drawing carries on from.
    System.Random GenerateLand(int cx, int cy)
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
        return rand;
    }

    // How much of the walkable land can be reached on foot from the crash site, from 0 to 1.
    // Raised ground does not count as a barrier, since the player can jump onto it; rock and the ship do.
    float ReachableShare(int cx, int cy)
    {
        var seen = new bool[width, height];
        var queue = new Queue<Vector2Int>();
        var start = new Vector2Int(cx, cy - 1);
        seen[start.x, start.y] = true;
        queue.Enqueue(start);
        var steps = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
        int reached = 0;
        while (queue.Count > 0)
        {
            Vector2Int p = queue.Dequeue();
            reached++;
            foreach (Vector2Int step in steps)
            {
                Vector2Int n = p + step;
                if (!InBounds(n.x, n.y) || seen[n.x, n.y] || kind[n.x, n.y] == Rock || kind[n.x, n.y] == Ship) continue;
                seen[n.x, n.y] = true;
                queue.Enqueue(n);
            }
        }

        int walkable = 0;
        foreach (byte k in kind)
        {
            if (k != Rock && k != Ship) walkable++;
        }
        return reached / (float)walkable;
    }

    // Draws the top-down view of the land.
    void BuildTerrain(System.Random rand)
    {
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
                // A tile drawn from a picture always looks exactly like the picture, so it has no shades.
                bool pictured = PictureFor(kind[x, y]) != null;
                int looks = kind[x, y] == Ice ? IceMarks.Length : 3;
                int variant = !pictured && rims == 0 && drop == 0 ? rand.Next(looks) : 0;
                terrain.SetTile(new Vector3Int(x, y, 0), TerrainTile(kind[x, y], ground[x, y], level, drop, rims, variant));
            }
        }
    }

    // One tile of the top-down view: the top of the block, plus the cliff face below it when the ground to the south is lower.
    Tile TerrainTile(byte material, int groundLevel, int level, int drop, int rims, int variant)
    {
        int key = material | (groundLevel << 2) | (level << 3) | (drop << 6) | (rims << 9) | (variant << 12);
        if (terrainTiles.TryGetValue(key, out Tile tile)) return tile;

        BlockPicture picture = PictureFor(material);
        bool pictured = picture != null;
        Color32 topColor, rimColor;
        switch (material)
        {
            case Ice: topColor = IceBase[groundLevel]; rimColor = IceRim; break;
            case Rock: topColor = RockColors[variant % RockColors.Length]; rimColor = RockEdge; break;
            case Ship: topColor = ShipColor; rimColor = ShipRim; break;
            default: topColor = SnowColors[groundLevel][pictured ? 0 : variant]; rimColor = SnowRim; break;
        }

        int facePixels = drop * FacePixels;
        int spriteHeight = TilePixels + facePixels;
        // The pivot sits where the far edge of the square is on flat ground, so higher blocks are drawn shifted up.
        var pivot = new Vector2(0.5f, (spriteHeight - level * FacePixels) / (float)spriteHeight);
        // Tiles that use a picture, and ice, are painted with several fine pixels for each ordinary tile pixel,
        // so their detail is as fine as the snow picture's.
        bool fine = pictured || material == Ice;
        int detail = fine ? PictureDetail : 1;
        Sprite sprite = PaintSprite(TilePixels * detail, spriteHeight * detail, pivot, TilePixels * detail, fine, (fineX, fineY) =>
        {
            int x = fineX / detail, y = fineY / detail;
            if (y >= TilePixels) return PaintFace(material, x, y - TilePixels, facePixels);
            bool rim = ((rims & 1) != 0 && y < 2) || ((rims & 2) != 0 && x < 1) || ((rims & 4) != 0 && x > TilePixels - 2);
            if (rim) return rimColor;
            if (pictured) return PicturePixel(picture, fineX, fineY);
            return material == Ice ? IcePixel(groundLevel, variant, fineX, fineY) : topColor;
        });
        tile = MakeTile(Color.white, sprite, false);
        terrainTiles[key] = tile;
        return tile;
    }

    // One fine pixel of an ice block: the ice blue, or a glint or crack where this look has a mark.
    // The marks are laid out on the 16-pixel tile grid but drawn as thin lines at the fine scale.
    static Color32 IcePixel(int groundLevel, int variant, int fineX, int fineY)
    {
        foreach (int[] mark in IceMarks[variant])
        {
            int step = mark[1] * PictureDetail + PictureDetail - 1 - fineY;
            if (step < 0 || step >= mark[2] * PictureDetail) continue;
            int lineX = mark[0] * PictureDetail + PictureDetail / 2 + step * mark[3];
            if (Mathf.Abs(fineX - lineX) > IceMarkHalfWidth) continue;
            return mark[4] == 1 ? IceGlint[groundLevel] : IceCrack[groundLevel];
        }
        return IceBase[groundLevel];
    }

    // Ice blocks for mining mode, one for each look that differs from plain: element 0 is plain ice.
    Sprite[] MakeIceSideSprites()
    {
        var looks = new List<int> { 0 };
        for (int look = 1; look < IceMarks.Length; look++)
        {
            if (IceMarks[look].Length > 0) looks.Add(look);
        }
        int size = TilePixels * PictureDetail;
        var sprites = new Sprite[looks.Count];
        for (int i = 0; i < sprites.Length; i++)
        {
            int look = looks[i];
            sprites[i] = PaintSprite(size, size, new Vector2(0.5f, 0.5f), size, true, (x, y) => IcePixel(0, look, x, y));
        }
        return sprites;
    }

    // The drawn picture for a kind of ground, or null if that kind is drawn as flat colour.
    BlockPicture PictureFor(byte material)
    {
        return material == Snow ? snowPicture : material == Ice ? icePicture : null;
    }

    // One pixel of the top of a tile, taken straight from its picture: one picture fills one tile,
    // the same way up and in the same colours as it was drawn.
    static Color32 PicturePixel(BlockPicture picture, int x, int y)
    {
        int size = TilePixels * PictureDetail;
        int pictureX = x * picture.Width / size, pictureY = y * picture.Height / size;
        Color32 c = picture.Pixels[(picture.Height - 1 - pictureY) * picture.Width + pictureX];
        c.a = 255;
        return c;
    }

    // Loads a block picture from the art folder. Returns null if there is no picture of that name.
    static BlockPicture LoadBlockPicture(string path)
    {
        var texture = Resources.Load<Texture2D>(path);
        if (texture == null) return null;
        return new BlockPicture
        {
            Sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width),
            Pixels = ReadPixels(texture),
            Width = texture.width,
            Height = texture.height,
        };
    }

    // Reads the pixels of a picture whether or not Unity imported it as readable, by drawing it and reading the drawing back.
    static Color32[] ReadPixels(Texture2D picture)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(picture.width, picture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(picture, target);
        RenderTexture.active = target;
        var copy = new Texture2D(picture.width, picture.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, picture.width, picture.height), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        Color32[] pixels = copy.GetPixels32();
        Destroy(copy);
        return pixels;
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

    // The colour a spot is shown in on the maps: white snow, blue ice, grey rock and ship, each paler where raised.
    public Color32 MapColorAt(int x, int y)
    {
        if (!InBounds(x, y)) return RockColors[0];
        switch (kind[x, y])
        {
            case Ice: return IceBase[ground[x, y]];
            case Rock: return RockColors[0];
            case Ship: return ShipColor;
            default: return ground[x, y] > 0 ? new Color32(255, 255, 255, 255) : new Color32(222, 230, 238, 255);
        }
    }

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
