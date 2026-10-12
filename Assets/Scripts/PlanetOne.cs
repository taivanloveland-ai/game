using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

// Builds the frozen crash planet when the scene starts: ground, rock, the wrecked ship, the player and the camera.
// Every spot has a height in blocks. The top-down view is angled: higher ground is drawn shifted up the screen,
// with a cliff face showing on its south side. Mining mode uses the same heights to draw a row from the side.
//
// The planet has no edges. It joins up with itself, so walking far enough in any direction brings you back to
// where you started. Positions in the game just keep counting past the planet's size; whenever the land is looked
// up, the position is wrapped back into the planet. Only the land around the player is ever drawn.
public class PlanetOne : MonoBehaviour
{
    // The size of the planet in squares. Not saved in the scene, so these numbers are always the ones used.
    [System.NonSerialized] public int width = 1440;
    [System.NonSerialized] public int height = 1440;
    // The world is different every time the game starts. Untick this to always build the world from the seed below.
    public bool randomWorld = true;
    // The number the world is built from. The same seed always gives the same world.
    public int seed = 1;
    // For testing only, and off in the real game: set this to true to begin with an ice pickaxe already made.
    // Like the planet's size, these two are not saved in the scene, so the values here are always the ones used.
    // (When they were saved, the open editor kept the old test values after the code was changed.)
    [System.NonSerialized] public bool startWithPickaxe = false;
    // For testing only, and 0 in the real game: begin with this many of each block (snow, ice and stone), already
    // on the hotbar.
    [System.NonSerialized] public int startWithBlocks = 0;

    // What one block of a row looks like from the side in mining mode.
    public enum SideBlock : byte { Air, Snow, Ice, Stone, Rock, RockTop, Ship }

    // How far up the screen one block of height is drawn in the top-down view, in tiles.
    public const float HeightScale = 0.5f;
    // The tallest anything gets: rock standing 2 blocks above raised ground.
    public const int MaxTop = 3;
    // Blocks can be placed up to this many blocks above the lowest ground, and no higher.
    public const int BuildLimit = 20;

    const byte Snow = 0, Ice = 1, Rock = 2, Ship = 3, Stone = 4;
    // How many blocks deep the ground goes. The very bottom layer cannot be mined.
    public const int GroundDepth = 14;
    // A cliff face in the top-down view is never drawn taller than this many blocks; deeper holes look the same from above.
    const int MaxDrawnDrop = 4;
    // The tallest south face a square is ever drawn with, in blocks.
    const int MaxFaceBlocks = 20;

    // A range of mountains crosses the far side of the planet from the crash site, running east to west: as far
    // north of the ship as it is possible to go, which is also as far south. Its peaks are this many blocks tall.
    const int MountainHeight = 14;
    // The range reaches this many squares north and south of its middle, fading out over the last so many.
    const int MountainHalfDepth = 140, MountainEdge = 50;
    // Roughly how many squares across a mountain is. Mountains stand where their noise is above the first number,
    // and reach full height where it is above that by the second.
    const float MountainBlobSize = 64f, MountainAbove = 0.4f, MountainRise = 0.4f;
    // Mountains get gradually steeper toward the top. This is how many squares wide each step of a slope is, from
    // the first block up to the last: broad and gentle at the foot, a block for every square near the peak.
    // No step is ever more than one block high, so every mountain can still be climbed.
    static readonly int[] StepWidths = { 4, 3, 3, 3, 2, 2, 2, 2, 1, 1, 1, 1, 1, 1 };
    // Mountains this tall or taller have snow on top.
    const int SnowCapFrom = 9;
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
    // The top-down view holds only the tiles this far either side of the player: a little more than the screen shows.
    const int WindowHalfWidth = 22;
    const int WindowHalfHeight = 16;
    // Roughly how many squares across the big shapes of the land are: snow, ice and rock; raised ground; snow depth.
    const float LandBlobSize = 26f, RaisedBlobSize = 17f, DepthBlobSize = 8f;

    // Where the land noise is above the first number it is rock, and below the second it is ice. Raised ground is where
    // its own noise is above the third.
    const float RockAbove = 0.62f, IceBelow = 0.38f, RaisedAbove = 0.6f;

    // In a passage one square wide, the ground over the player turns see-through this many squares along it.
    const int SeeThroughRadius = 2;
    // A room's roof turns see-through at most this many squares from the player.
    const int RoomReach = 14;
    static readonly Color RoofTint = new Color(1f, 1f, 1f, 0.2f);
    // Under a see-through roof, the squares the walls stand on are drawn this much darker than the floor.
    static readonly Color WallFootprintTint = new Color(0.55f, 0.58f, 0.66f);
    // The floor of a tunnel is drawn darker than land in the open.
    static readonly Color UndergroundTint = new Color(0.5f, 0.56f, 0.68f);

    // Camera limits that never come into play, for a world with no edges.
    public static readonly Rect Unbounded = new Rect(-1e7f, -1e7f, 2e7f, 2e7f);

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
    // Snow comes in three looks: Taivan's two pictures and this plain white.
    const int SnowLooks = 3;
    static readonly Color32 PlainSnow = new Color32(255, 255, 255, 255);
    static readonly Color32 SnowRim = new Color32(200, 214, 228, 255), SnowFace = new Color32(176, 194, 214, 255), SnowFoot = new Color32(152, 173, 197, 255);
    static readonly Color32 IceRim = new Color32(200, 232, 246, 255), IceFace = new Color32(104, 166, 206, 255), IceFoot = new Color32(84, 144, 188, 255);
    static readonly Color32 RockEdge = new Color32(138, 152, 170, 255);
    static readonly Color32 CliffFace = new Color32(59, 70, 86, 255), CliffCrack = new Color32(48, 58, 73, 255), CliffFoot = new Color32(40, 49, 62, 255);
    static readonly Color32 ShipRim = new Color32(150, 158, 170, 255);
    // Bare stone, as seen from above where the snow or ice over it has been dug away.
    static readonly Color32 StoneColor = new Color32(70, 80, 96, 255), StoneRim = new Color32(98, 110, 128, 255);
    // Ice is reflective in the top-down view. Reflections are drawn at this sorting order, under the land, and
    // show through the top of the ice, which is this solid (out of 255).
    public const int ReflectionOrder = -3;
    const byte IceGlassAlpha = 150;
    // How strong a reflection is drawn before the ice dims it, and how much shorter than the real thing it is,
    // since the ground is seen at a slant.
    public static readonly Color ReflectionTint = new Color(1f, 1f, 1f, 0.8f);
    public const float ReflectionSquash = 0.7f;

    // A hole in a wall, seen from outside, is see-through.
    static readonly Color32 SeeThrough = new Color32(0, 0, 0, 0);
    // Marks a block of a tile's south face that is not there. The other values are the materials above.
    const int EmptyFaceBlock = 7;
    // A hole in the west, east or north side of a wall is shown if the wall carries on within this many squares
    // either side of it.
    const int WidestHole = 3;
    static readonly Color PlayerColor =new Color32(240, 138, 46, 255);
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

    BlockPicture snowPicture, snowPicture2, icePicture;

    byte[,] kind;
    // Height of the walkable ground at each spot (0 or 1), and the height of whatever stands there (rock and ship are taller).
    byte[,] ground;
    // The top of each spot as the world was made, and as it is now after any mining. Mining can take it below zero.
    byte[,] baseTop;
    sbyte[,] top;
    // The blocks that have been mined away, by spot: the levels removed from that spot's column.
    readonly Dictionary<Vector2Int, HashSet<int>> mined = new Dictionary<Vector2Int, HashSet<int>>();

    // The blocks the player has placed, by spot: the level each is at and what it is. A placed block can sit above
    // empty space, as the roof of a house does.
    readonly Dictionary<Vector2Int, Dictionary<int, SideBlock>> placed = new Dictionary<Vector2Int, Dictionary<int, SideBlock>>();

    // Raised after a block is mined, with its spot (unwrapped, as given) and level.
    public event System.Action<int, int, int> BlockMined;
    // How many blocks deep the ice and the snow go at each spot, seen from the side.
    byte[,] iceDepth;
    byte[,] snowDepth;
    // How many blocks tall the mountain is at each spot, or 0 where there is none.
    byte[,] mountain;
    GameObject topDownRoot;
    Tilemap terrain;
    Tilemap underground;
    Tilemap doorFloors;
    Tilemap iceBeneath;
    // The squares whose ground is currently see-through because the player is under it.
    readonly List<Vector2Int> seeThrough = new List<Vector2Int>();
    // The open squares of the room or tunnel the player is in, worked out afresh each frame.
    readonly HashSet<Vector2Int> room = new HashSet<Vector2Int>();
    readonly HashSet<Vector2Int> roomWalls = new HashSet<Vector2Int>();
    readonly Queue<Vector2Int> roomQueue = new Queue<Vector2Int>();
    static readonly Vector2Int[] Sides = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
    readonly Dictionary<(long, long), Tile> terrainTiles = new Dictionary<(long, long), Tile>();
    PlayerController player;
    // Mixed into each square's position to pick which look its tile has.
    int variantSeed;
    // The squares whose tiles are currently on the tilemap. Empty until the first tiles are placed.
    RectInt window;

    // The squares of land being drawn around the player right now, in unwrapped positions.
    public RectInt LandWindow => window;

    void Awake()
    {
        Square = PaintSprite((x, y) => new Color32(255, 255, 255, 255));

        // Drawn pictures, where they exist, are used for the top of tiles here and for blocks in mining mode.
        snowPicture = LoadBlockPicture("Art/SnowBlock");
        snowPicture2 = LoadBlockPicture("Art/SnowBlock2");
        icePicture = LoadBlockPicture("Art/IceBlock");
        IceSideSprites = MakeIceSideSprites();

        int cx = width / 2, cy = height / 2;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var picker = new System.Random();
        if (randomWorld) seed = picker.Next(1, 1000000);
        GenerateLand(cx, cy);
        float reachable = ReachableShare(cx, cy);
        // A random world is thrown away and picked again if rock walls off too much of it from the crash site.
        for (int tries = 0; randomWorld && tries < 5 && reachable < MinReachableShare; tries++)
        {
            seed = picker.Next(1, 1000000);
            GenerateLand(cx, cy);
            reachable = ReachableShare(cx, cy);
        }
        // Logged so a world worth keeping can be built again by typing its seed in and unticking Random World.
        Debug.Log("World seed: " + seed + " (" + Mathf.RoundToInt(reachable * 100f) + "% of its snow and ice can be walked to, worked out in " + (timer.ElapsedMilliseconds / 1000f).ToString("F1") + " seconds)");
        BuildTerrain();

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
        player = controller;
        ShowLandAround(Vector2Int.FloorToInt(player.GroundPosition));

        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
        follow.target = controller.transform;
        follow.bounds = Unbounded;
        // On high ground the player is drawn further up the screen, and the camera goes up with them.
        follow.lift = () => controller.CameraLift;

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
        var nook = gameObject.AddComponent<CraftingNook>();
        nook.Setup(this, controller, mining, gatherables, inventory, hud, cam, new Vector2Int(cx, cy + 1));
        if (startWithPickaxe) inventory.AddTool(nook.Pickaxe);
        var pickaxeWork = gameObject.AddComponent<Mining>();
        pickaxeWork.Setup(this, controller, mining, inventory, hud, pickups, nook, gatherables, cam);
        if (startWithBlocks > 0)
        {
            var blocks = new[] { pickaxeWork.SnowItem, pickaxeWork.IceItem, pickaxeWork.StoneItem };
            foreach (Item block in blocks)
            {
                inventory.Add(block, startWithBlocks);
                int slot = System.Array.IndexOf(inventory.Hotbar, null);
                if (slot >= 0) inventory.SetSlot(slot, block);
            }
        }
        gameObject.AddComponent<Building>().Setup(this, controller, mining, inventory, hud, gatherables, cam);
        // The player wakes up beside the ship, where they started.
        var health = gameObject.AddComponent<Health>();
        health.Setup(this, controller, mining, inventory, pickups, hud, new Vector2(cx + 0.5f, cy - 0.5f));
        gameObject.AddComponent<Spiders>().Setup(this, controller, mining, dayNight, health, inventory, nook, pickups, cam);
        // A thornam bush can only be cut down with the ice axe in hand.
        gatherables.AxeEquipped = () => inventory.Equipped == nook.Axe;
        gatherables.Say = text => hud.Say(text, 2f);
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
        // The player's reflection in the ice: the same rectangle upside down beneath their feet. It is kept out of
        // the sorted group above so that it is drawn under the land.
        GameObject reflection = MakeBox("Reflection", Square, PlayerColor * ReflectionTint, new Vector2(PlayerController.Width, PlayerController.Height * ReflectionSquash), ReflectionOrder);
        reflection.transform.SetParent(player.transform, false);

        controller.Setup(this, bodyVisual.transform, shadow.transform, reflection.transform, sorting);
        return controller;
    }

    // Works out the shape of the world from the seed: what each spot is made of and how high it is.
    // Nothing is drawn yet.
    void GenerateLand(int cx, int cy)
    {
        var rand = new System.Random(seed);
        int landSeed = rand.Next(), raisedSeed = rand.Next(), depthSeed = rand.Next();
        variantSeed = rand.Next();
        int mountainSeed = rand.Next();

        kind = new byte[width, height];
        ground = new byte[width, height];
        baseTop = new byte[width, height];
        top = new sbyte[width, height];
        mined.Clear();
        placed.Clear();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float fromCrash = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                float e = LoopingNoise(x, y, LandBlobSize, landSeed, 3);
                // Flatten the land around the crash site so the start is open snow.
                e = 0.5f + (e - 0.5f) * Mathf.Clamp01((fromCrash - 6f) / 6f);

                kind[x, y] = e > RockAbove ? Rock : e < IceBelow ? Ice : Snow;
                // Patches of ground one block higher. The crash site itself stays at ground level.
                ground[x, y] = (byte)(fromCrash > 8f && LoopingNoise(x, y, RaisedBlobSize, raisedSeed, 3) > RaisedAbove ? 1 : 0);
            }
        }
        // The wrecked ship sits just above the spot where the player starts.
        for (int y = cy + 1; y < cy + 3; y++)
        {
            for (int x = cx - 2; x < cx + 3; x++) kind[x, y] = Ship;
        }

        RaiseMountains(cy, mountainSeed);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                baseTop[x, y] = ground[x, y];
                top[x, y] = (sbyte)ground[x, y];
                if (mountain[x, y] > 0)
                {
                    // A mountain is rock from the ground up to its own height.
                    baseTop[x, y] = (byte)Mathf.Max(mountain[x, y], ground[x, y]);
                    top[x, y] = (sbyte)baseTop[x, y];
                    continue;
                }
                if (kind[x, y] != Rock && kind[x, y] != Ship) continue;
                int highest = 0;
                for (int ny = y - 1; ny <= y + 1; ny++)
                {
                    for (int nx = x - 1; nx <= x + 1; nx++) highest = Mathf.Max(highest, ground[WrapX(nx), WrapY(ny)]);
                }
                baseTop[x, y] = (byte)(highest + RockHeight);
                top[x, y] = (sbyte)baseTop[x, y];
            }
        }
        ComputeGroundDepths(depthSeed);
    }

    // Works out how tall the mountains are everywhere and turns the land under them to rock. The slopes rise one
    // block at a time, so any mountain can be climbed by jumping up its steps.
    void RaiseMountains(int cy, int mountainSeed)
    {
        mountain = new byte[width, height];
        int middle = cy + height / 2;
        for (int dy = -MountainHalfDepth; dy <= MountainHalfDepth; dy++)
        {
            int y = WrapY(middle + dy);
            // Full height through the middle of the range, falling away to nothing at its north and south edges.
            float strength = Mathf.Clamp01((MountainHalfDepth - Mathf.Abs(dy)) / (float)MountainEdge);
            for (int x = 0; x < width; x++)
            {
                float rise = (LoopingNoise(x, y, MountainBlobSize, mountainSeed, 2) - MountainAbove) / MountainRise * strength;
                int tall = Mathf.Clamp(Mathf.RoundToInt(rise * MountainHeight), 0, MountainHeight);
                // Kept for now as how far in from the foot of the mountain a spot this tall has to be.
                mountain[x, y] = (byte)RunTo(tall);
            }
        }

        // Wear the mountains into shape: no spot may be more than one square further in from the foot than the
        // spot next to it. Sweeping forwards and then backwards a few times settles the whole range.
        bool changed = true;
        for (int pass = 0; pass < 8 && changed; pass++)
        {
            changed = false;
            for (int dy = -MountainHalfDepth; dy <= MountainHalfDepth; dy++)
            {
                int y = WrapY(middle + dy), south = WrapY(y - 1);
                for (int x = 0; x < width; x++) changed |= WearDown(x, y, WrapX(x - 1), south);
            }
            for (int dy = MountainHalfDepth; dy >= -MountainHalfDepth; dy--)
            {
                int y = WrapY(middle + dy), north = WrapY(y + 1);
                for (int x = width - 1; x >= 0; x--) changed |= WearDown(x, y, WrapX(x + 1), north);
            }
        }

        // Turn how far in each spot is into how tall it is, and the land under the mountains into rock.
        for (int dy = -MountainHalfDepth; dy <= MountainHalfDepth; dy++)
        {
            int y = WrapY(middle + dy);
            for (int x = 0; x < width; x++)
            {
                int tall = 0;
                while (tall < MountainHeight && RunTo(tall + 1) <= mountain[x, y]) tall++;
                mountain[x, y] = (byte)tall;
                if (tall > 0) kind[x, y] = Rock;
            }
        }
    }

    // How many squares in from the foot of a mountain its slope has to go to be a number of blocks tall.
    static int RunTo(int tall)
    {
        int run = 0;
        for (int i = 0; i < tall; i++) run += StepWidths[i];
        return run;
    }

    // Lowers a spot of mountain to one more than its lower neighbour to the side or above or below, if it is higher.
    bool WearDown(int x, int y, int sideX, int nextY)
    {
        int limit = Mathf.Min(mountain[sideX, y], mountain[x, nextY]) + 1;
        if (mountain[x, y] <= limit) return false;
        mountain[x, y] = (byte)limit;
        return true;
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
                Vector2Int n = Wrap(p + step);
                if (seen[n.x, n.y] || kind[n.x, n.y] == Rock || kind[n.x, n.y] == Ship) continue;
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

    // Sets up the top-down view of the land. The tiles themselves are placed as the player moves around.
    void BuildTerrain()
    {
        var grid = new GameObject("Grid").AddComponent<Grid>();
        grid.transform.SetParent(transform, false);
        topDownRoot = grid.gameObject;
        terrain = MakeTilemap(grid, "Terrain", 0);
        // Each tile is sorted on its own by the far edge of its square, so the player is drawn over the tile
        // they stand on and behind taller tiles nearer the bottom of the screen.
        terrain.tileAnchor = new Vector3(0.5f, 1f, 0f);
        terrain.GetComponent<TilemapRenderer>().mode = TilemapRenderer.Mode.Individual;

        // Drawn beneath the land: the floor of a tunnel, seen through the ground above it when that turns see-through.
        underground = MakeTilemap(grid, "Underground", -1);
        underground.tileAnchor = terrain.tileAnchor;
        underground.color = UndergroundTint;

        // Also beneath the land: the floor seen through a hole in a wall.
        doorFloors = MakeTilemap(grid, "Door Floors", -1);
        doorFloors.tileAnchor = terrain.tileAnchor;
        doorFloors.color = UndergroundTint;

        // The solid ice under the see-through ice, with reflections drawn between the two.
        iceBeneath = MakeTilemap(grid, "Ice Beneath", ReflectionOrder - 1);
        iceBeneath.tileAnchor = terrain.tileAnchor;
    }

    void Update()
    {
        if (!topDownRoot.activeSelf) return;
        ShowLandAround(Vector2Int.FloorToInt(player.GroundPosition));
        ShowUnderRoof();
    }

    // True if a spot is open at a level but has ground over it: the inside of a tunnel or a cave.
    public bool IsTunnel(int x, int y, int level)
    {
        return SideBlockAt(x, y, level) == SideBlock.Air && TopAt(x, y) > level;
    }

    // When the player is under a roof, the whole roof of the room they are in turns see-through, so they and the
    // floor can be seen from above: a house they have built, or a tunnel they have dug. Other rooms keep their roofs.
    // It is redone every frame, which also keeps it right when tiles are replaced as the player moves.
    void ShowUnderRoof()
    {
        foreach (Vector2Int cell in seeThrough)
        {
            terrain.SetColor((Vector3Int)cell, Color.white);
            underground.SetTile((Vector3Int)cell, null);
        }
        seeThrough.Clear();

        int level = Mathf.FloorToInt(player.FeetHeight + 0.01f);
        Vector2Int at = Vector2Int.FloorToInt(player.GroundPosition);
        if (!IsTunnel(at.x, at.y, level)) return;

        // The room is every covered, open square that can be walked to from the player's without leaving cover.
        // A gap one square wide between walls is a doorway: it belongs to the room, but the room stops there.
        // In a passage that is one square wide all the way, the roof opens up a few squares either side instead.
        room.Clear();
        roomQueue.Clear();
        room.Add(at);
        roomQueue.Enqueue(at);
        while (roomQueue.Count > 0)
        {
            Vector2Int cell = roomQueue.Dequeue();
            bool narrow = cell != at && IsDoorway(cell, level);
            foreach (Vector2Int side in Sides)
            {
                Vector2Int next = cell + side;
                if (room.Contains(next) || !IsTunnel(next.x, next.y, level)) continue;
                int away = Mathf.Max(Mathf.Abs(next.x - at.x), Mathf.Abs(next.y - at.y));
                if (away > RoomReach) continue;
                if (narrow && (away > SeeThroughRadius || !IsDoorway(next, level))) continue;
                room.Add(next);
                roomQueue.Enqueue(next);
            }
        }

        // The room's floor shows through its roof. The walls round it fade too, since they are drawn tall enough to
        // hide the floor behind them, and each leaves a darker footprint so the shape of the room stays clear.
        roomWalls.Clear();
        foreach (Vector2Int cell in room)
        {
            Fade(cell, TunnelFloorTile(cell.x, cell.y, level), Color.white);
            for (int y = cell.y - 1; y <= cell.y + 1; y++)
            {
                for (int x = cell.x - 1; x <= cell.x + 1; x++)
                {
                    var wall = new Vector2Int(x, y);
                    if (room.Contains(wall) || roomWalls.Contains(wall)) continue;
                    SideBlock block = SideBlockAt(x, y, level);
                    if (block == SideBlock.Air) continue;
                    roomWalls.Add(wall);
                    Fade(wall, TerrainTile(MaterialOf(block), 0, 0, 0, 0, 0), WallFootprintTint);
                }
            }
        }
    }

    // Makes one square of the land see-through and shows a tile beneath it, in a shade of its own.
    void Fade(Vector2Int square, Tile beneath, Color shade)
    {
        var cell = (Vector3Int)square;
        // A tile normally keeps its own colour; this lets this one square be faded.
        terrain.SetTileFlags(cell, TileFlags.None);
        terrain.SetColor(cell, RoofTint);
        underground.SetTile(cell, beneath);
        underground.SetTileFlags(cell, TileFlags.None);
        underground.SetColor(cell, shade);
        seeThrough.Add(square);
    }

    // True for an open square with something solid on both sides of it, either way round: a doorway, or a passage
    // one square wide.
    bool IsDoorway(Vector2Int cell, int level)
    {
        bool west = SideBlockAt(cell.x - 1, cell.y, level) != SideBlock.Air, east = SideBlockAt(cell.x + 1, cell.y, level) != SideBlock.Air;
        bool south = SideBlockAt(cell.x, cell.y - 1, level) != SideBlock.Air, north = SideBlockAt(cell.x, cell.y + 1, level) != SideBlock.Air;
        return (west && east) || (south && north);
    }

    // The square of land whose picture is under a point of the top-down view. Higher ground is drawn further up
    // the screen, so this is not always the square the point is over on flat ground.
    public Vector2Int SquareAt(Vector2 point)
    {
        int x = Mathf.FloorToInt(point.x), pointRow = Mathf.FloorToInt(point.y);
        // Nearer squares are drawn over further ones, so the nearest one whose picture holds the point wins.
        for (int y = pointRow - BuildLimit / 2 - 1; y <= pointRow + GroundDepth / 2 + 1; y++)
        {
            int level = DrawnTopAt(x, y);
            float topEdge = y + level * HeightScale;
            float face = FaceDrop(level, DrawnTopAt(x, y - 1)) * HeightScale;
            if (point.y >= topEdge - face && point.y < topEdge + 1f) return new Vector2Int(x, y);
        }
        return new Vector2Int(x, pointRow);
    }

    // The tile for the floor of a tunnel: whatever block is under the open space at that level.
    Tile TunnelFloorTile(int x, int y, int level)
    {
        int floor = level - 1;
        while (floor > -GroundDepth && SideBlockAt(x, y, floor) == SideBlock.Air) floor--;
        return TerrainTile(MaterialOf(SideBlockAt(x, y, floor)), 0, 0, 0, 0, 0);
    }

    // Makes sure the tiles around a square are on the tilemap, and takes away the ones that have fallen out of range.
    // The planet is far too big to draw all at once, and drawing only what is near also lets it repeat without a seam.
    void ShowLandAround(Vector2Int centre)
    {
        var wanted = new RectInt(centre.x - WindowHalfWidth, centre.y - WindowHalfHeight, WindowHalfWidth * 2 + 1, WindowHalfHeight * 2 + 1);
        if (wanted.Equals(window)) return;

        foreach (Vector2Int cell in window.allPositionsWithin)
        {
            if (wanted.Contains(cell)) continue;
            terrain.SetTile((Vector3Int)cell, null);
            doorFloors.SetTile((Vector3Int)cell, null);
            iceBeneath.SetTile((Vector3Int)cell, null);
        }
        foreach (Vector2Int cell in wanted.allPositionsWithin)
        {
            if (!window.Contains(cell)) SetLandTile(cell);
        }
        window = wanted;
    }

    // The tile for one square of the top-down view.
    Tile TileAt(int x, int y, bool glassy = false)
    {
        // What shows from above is whatever block is now on top, which mining or building may have changed.
        int level = DrawnTopAt(x, y);
        byte material = MaterialOf(SideBlockAt(x, y, level - 1));
        int drop = FaceDrop(level, DrawnTopAt(x, y - 1));
        int rims = (DrawnTopAt(x, y + 1) < level ? 1 : 0) | (DrawnTopAt(x - 1, y) < level ? 2 : 0) | (DrawnTopAt(x + 1, y) < level ? 4 : 0);
        // Plain tiles get one of a few looks; tiles with an edge all share one so the edges line up cleanly.
        // A tile drawn from a picture always looks exactly like the picture, so it has no other looks.
        // The look is worked out from the square's position, so a square looks the same every time it comes into view.
        bool pictured = PictureFor(material) != null;
        int looks = material == Ice ? IceMarks.Length : 3;
        int variant = !pictured && rims == 0 && drop == 0 ? (int)(Hash(WrapX(x), WrapY(y), variantSeed) * looks) : 0;
        if (material == Snow && snowPicture != null) variant = SnowLookAt(x, y, level - 1);

        return TerrainTile(material, Mathf.Clamp(level, 0, 1), level, drop, rims, variant, FaceBlocks(x, y, level, drop), glassy && material == Ice);
    }

    // How many blocks tall the south face of a square is drawn: down to the ground south of it. Next to a deep hole
    // the face stops a few blocks down, since deeper holes look the same from above.
    static int FaceDrop(int level, int south)
    {
        return Mathf.Clamp(level - Mathf.Max(south, Mathf.Min(0, level - MaxDrawnDrop)), 0, MaxFaceBlocks);
    }

    // What each block of a square's south face is, from the top down, three bits a block: the block's own material,
    // or EmptyFaceBlock where there is no block. So a stack of different blocks shows each one, and a hole in a wall
    // shows as a hole, exactly where the blocks are missing.
    long FaceBlocks(int x, int y, int level, int drop)
    {
        long blocks = 0;
        for (int i = 0; i < drop; i++)
        {
            SideBlock block = SideBlockAt(x, y, level - 1 - i);
            blocks |= (long)(block == SideBlock.Air ? EmptyFaceBlock : MaterialOf(block)) << (i * 3);
        }
        return blocks;
    }

    // True if a spot has been mined or built on. Everywhere else is solid from its top down.
    bool Changed(int x, int y)
    {
        var spot = new Vector2Int(WrapX(x), WrapY(y));
        return mined.ContainsKey(spot) || placed.ContainsKey(spot);
    }

    // What is seen through a hole in a wall: the floor of the space inside. Every square with open space under
    // its top has one, drawn beneath the land. Null for a square that is solid all the way down.
    Tile FloorInsideAt(int x, int y)
    {
        if (!Changed(x, y)) return null;
        int level = TopAt(x, y) - 1;
        while (level > -GroundDepth && SideBlockAt(x, y, level) != SideBlock.Air) level--;
        if (level <= -GroundDepth) return null;
        while (level > -GroundDepth && SideBlockAt(x, y, level) == SideBlock.Air) level--;
        return TerrainTile(MaterialOf(SideBlockAt(x, y, level)), 0, 0, 0, 0, 0);
    }

    // The height a square is drawn at in the top-down view. That is its top, except where a wall has a hole in its
    // west, east or north side. Those sides cannot be seen from above, so the hole is shown as a piece missing from
    // the wall: the square is drawn down at the bottom of the hole.
    int DrawnTopAt(int x, int y)
    {
        int top = TopAt(x, y);
        if (!Changed(x, y)) return top;
        int drawn = top;
        for (int i = 0; i < 3; i++)
        {
            int nextX = x + (i == 0 ? -1 : i == 1 ? 1 : 0), nextY = y + (i == 2 ? 1 : 0);
            // The hole has to open onto ground under the open sky, at the level someone stands at there.
            int stand = TopAt(nextX, nextY);
            if (stand >= top || SideBlockAt(x, y, stand) != SideBlock.Air) continue;
            // It only counts as a hole in a wall if the wall carries on to both sides of it. That leaves alone the
            // edge of a roof that simply sticks out over open ground.
            int alongX = i == 2 ? 1 : 0, alongY = 1 - alongX;
            if (!SolidWithin(x, y, stand, alongX, alongY) || !SolidWithin(x, y, stand, -alongX, -alongY)) continue;
            int floor = stand;
            while (floor > -GroundDepth && SideBlockAt(x, y, floor - 1) == SideBlock.Air) floor--;
            drawn = Mathf.Min(drawn, floor);
        }
        return drawn;
    }

    // True if there is a block at a level within a few squares of a spot, going one way.
    bool SolidWithin(int x, int y, int level, int stepX, int stepY)
    {
        for (int i = 1; i <= WidestHole; i++)
        {
            if (SideBlockAt(x + stepX * i, y + stepY * i, level) != SideBlock.Air) return true;
        }
        return false;
    }

    // Puts the right tiles on the tilemaps for one square of the land that is being drawn.
    void SetLandTile(Vector2Int cell)
    {
        // Ice is reflective. Its tile is slightly see-through, with a solid copy of itself beneath, and reflections
        // are drawn in between the two. They show on the ice and nowhere else, since no other ground is see-through.
        bool ice = MaterialOf(SideBlockAt(cell.x, cell.y, DrawnTopAt(cell.x, cell.y) - 1)) == Ice;
        terrain.SetTile((Vector3Int)cell, TileAt(cell.x, cell.y, ice));
        iceBeneath.SetTile((Vector3Int)cell, ice ? TileAt(cell.x, cell.y, false) : null);
        doorFloors.SetTile((Vector3Int)cell, FloorInsideAt(cell.x, cell.y));
    }

    // One tile of the top-down view: the top of the block, plus the cliff face below it when the ground to the south
    // is lower. FaceBlocks says what each block of that face is; see FaceBlocks above.
    // Glassy is for ice that reflections show through.
    Tile TerrainTile(byte material, int groundLevel, int level, int drop, int rims, int variant, long faceBlocks = 0, bool glassy = false)
    {
        // The south face can be up to MaxFaceBlocks tall, which is too much to fit in one number with the rest.
        var key = ((long)(material | (groundLevel << 3) | ((level + 32) << 4) | (drop << 11) | (rims << 16) | (variant << 19) | (glassy ? 1 << 23 : 0)), faceBlocks);
        if (terrainTiles.TryGetValue(key, out Tile tile)) return tile;

        // Snow has three looks, picked square by square: Taivan's two snow pictures, and plain white.
        BlockPicture picture = material == Snow && snowPicture != null ? SnowPicture(variant) : PictureFor(material);
        bool pictured = picture != null;
        Color32 topColor, rimColor;
        switch (material)
        {
            case Ice: topColor = IceBase[groundLevel]; rimColor = IceRim; break;
            case Rock: topColor = RockColors[variant % RockColors.Length]; rimColor = RockEdge; break;
            case Ship: topColor = ShipColor; rimColor = ShipRim; break;
            case Stone: topColor = StoneColor; rimColor = StoneRim; break;
            default: topColor = material == Snow && snowPicture != null && !pictured ? PlainSnow : SnowColors[groundLevel][pictured ? 0 : variant]; rimColor = SnowRim; break;
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
            if (y >= TilePixels)
            {
                // Each block of the face is drawn as what it is. Where there is no block the face is see-through,
                // and the floor inside shows through the hole.
                int faceY = y - TilePixels;
                int block = (int)((faceBlocks >> (faceY / FacePixels * 3)) & 7);
                return block == EmptyFaceBlock ? SeeThrough : PaintFace((byte)block, x, faceY, facePixels);
            }
            bool rim =((rims & 1) != 0 && y < 2) || ((rims & 2) != 0 && x < 1) || ((rims & 4) != 0 && x > TilePixels - 2);
            if (rim) return rimColor;
            if (pictured) return PicturePixel(picture, fineX, fineY);
            if (material != Ice) return topColor;
            // The top of ice is a little see-through when it is glassy, so reflections drawn beneath it show.
            Color32 ice = IcePixel(groundLevel, variant, fineX, fineY);
            if (glassy) ice.a = IceGlassAlpha;
            return ice;
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

    // Which of the three looks a block of snow has: 0 and 1 are Taivan's two snow pictures, 2 is plain white.
    // It is picked at random for each block, but always the same for the same block.
    public int SnowLookAt(int x, int y, int level)
    {
        return Mathf.Min(SnowLooks - 1, (int)(Hash(WrapX(x), WrapY(y), variantSeed + 7919 * (level + 64)) * SnowLooks));
    }

    // The picture for a look of snow, or null for plain white.
    public Sprite SnowLookSprite(int look)
    {
        return SnowPicture(look)?.Sprite;
    }

    BlockPicture SnowPicture(int look)
    {
        // If the second picture is missing, its share of the snow uses the first one.
        return look == 0 ? snowPicture : look == 1 ? snowPicture2 ?? snowPicture : null;
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
            case Stone:
                if (y < 2) return RockEdge;
                if (foot) return CliffFoot;
                // The cracks repeat every two blocks down a tall face.
                int crackY = y % (FacePixels * 2);
                bool crack = (x == 3 && crackY > 4) || (x == 8 && crackY < 11) || (x == 12 && crackY > 6);
                return crack ? CliffCrack : CliffFace;
            default: return foot ? SnowFoot : SnowFace;
        }
    }

    // Works out how deep the ice and snow go everywhere. Ice is shallow at the shore and deeper toward the middle,
    // so from the side a patch of ice looks like a lake or river that froze in its bed.
    void ComputeGroundDepths(int depthSeed)
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
                Vector2Int n = Wrap(p + step);
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
                snowDepth[x, y] = (byte)(LoopingNoise(x, y, DepthBlobSize, depthSeed, 1) > 0.5f ? 5 : 4);
            }
        }
    }

    // Brings a position back into the planet. Going off one side comes back on the other.
    public int WrapX(int x)
    {
        x %= width;
        return x < 0 ? x + width : x;
    }

    public int WrapY(int y)
    {
        y %= height;
        return y < 0 ? y + height : y;
    }

    public Vector2Int Wrap(Vector2Int cell)
    {
        return new Vector2Int(WrapX(cell.x), WrapY(cell.y));
    }

    // Because the planet repeats, every spot can be found again one planet-width away in each direction.
    // This gives the copy of a spot that is closest to another position.
    public Vector2 Nearest(Vector2 spot, Vector2 from)
    {
        return new Vector2(
            from.x + Mathf.Repeat(spot.x - from.x + width / 2f, width) - width / 2f,
            from.y + Mathf.Repeat(spot.y - from.y + height / 2f, height) - height / 2f);
    }

    // Everything that belongs to the top-down view hangs under this.
    public Transform TopDownRoot => topDownRoot.transform;

    // The colour a spot is shown in on the maps: white snow, blue ice, grey rock and ship, each paler where raised.
    public Color32 MapColorAt(int x, int y)
    {
        x = WrapX(x);
        y = WrapY(y);
        switch (SurfaceMaterial(x, y))
        {
            case Stone: return StoneColor;
            case Ice: return IceBase[ground[x, y]];
            case Rock: return RockColors[0];
            case Ship: return ShipColor;
            default: return ground[x, y] > 0 ? new Color32(255, 255, 255, 255) : new Color32(222, 230, 238, 255);
        }
    }

    public bool IsSnow(int x, int y)
    {
        return kind[WrapX(x), WrapY(y)] == Snow;
    }

    // True where the land was made as ice, whatever has been dug or built there since.
    public bool IsIceAt(int x, int y)
    {
        return kind[WrapX(x), WrapY(y)] == Ice;
    }

    public bool IsIce(Vector2 position)
    {
        return SurfaceMaterial(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y)) == Ice;
    }

    // Height in blocks of whatever is at a spot.
    public int TopAt(int x, int y)
    {
        return top[WrapX(x), WrapY(y)];
    }

    // Height of the snow or ice surface at a spot, ignoring any rock or ship standing on it.
    public int GroundAt(int x, int y)
    {
        return ground[WrapX(x), WrapY(y)];
    }

    // The block at one spot of a row seen from the side. Level 0 is the first block above the lowest ground,
    // and negative levels are underground.
    public SideBlock SideBlockAt(int x, int row, int level)
    {
        x = WrapX(x);
        row = WrapY(row);
        if (placed.Count > 0 && placed.TryGetValue(new Vector2Int(x, row), out Dictionary<int, SideBlock> put) && put.TryGetValue(level, out SideBlock placedBlock)) return placedBlock;
        if (level >= baseTop[x, row]) return SideBlock.Air;
        if (mined.TryGetValue(new Vector2Int(x, row), out HashSet<int> gone) && gone.Contains(level)) return SideBlock.Air;

        byte k = kind[x, row];
        int surface = ground[x, row];
        if (k == Rock)
        {
            if (level < surface) return SideBlock.Stone;
            // The tallest mountains have a block of snow on top.
            if (level == baseTop[x, row] - 1 && mountain[x, row] >= SnowCapFrom) return SideBlock.Snow;
            return level == baseTop[x, row] - 1 ? SideBlock.RockTop : SideBlock.Rock;
        }
        if (level >= surface) return SideBlock.Ship;

        int depth = surface - level;
        if (k == Ice && depth <= iceDepth[x, row]) return SideBlock.Ice;
        return depth <= snowDepth[x, row] ? SideBlock.Snow : SideBlock.Stone;
    }

    // Set by whatever stands on the ground and gets in the way, such as ferns and ice shards: true for a spot that
    // has one. It is given the spot's position within the planet.
    public System.Func<int, int, bool> Obstacle;

    // True if something stands on the ground at a spot and blocks the way.
    public bool ObstacleAt(int x, int y)
    {
        return Obstacle != null && Obstacle(WrapX(x), WrapY(y));
    }

    // True if the player cannot be in a space: there is a block there, or something standing on the ground there.
    // A thing on the ground fills the one block of space above the ground it stands on.
    public bool SolidAt(int x, int y, int level)
    {
        return SideBlockAt(x, y, level) != SideBlock.Air || (level == GroundAt(x, y) && ObstacleAt(x, y));
    }

    // The material showing on top of a spot, as the top-down view draws it.
    byte SurfaceMaterial(int x, int y)
    {
        return MaterialOf(SideBlockAt(x, y, TopAt(x, y) - 1));
    }

    // The top-down material for a block.
    static byte MaterialOf(SideBlock block)
    {
        switch (block)
        {
            case SideBlock.Snow: return Snow;
            case SideBlock.Ice: return Ice;
            case SideBlock.Ship: return Ship;
            case SideBlock.Stone: return Stone;
            default: return Rock;
        }
    }

    // Mines the block at one level of a spot. Returns what the block was, or Air if there was nothing there to mine:
    // empty space, part of the ship, or the unbreakable bottom layer of the ground.
    public SideBlock Mine(int x, int row, int level)
    {
        SideBlock was = SideBlockAt(x, row, level);
        if (was == SideBlock.Air || was == SideBlock.Ship || level <= -GroundDepth) return SideBlock.Air;

        var spot = new Vector2Int(WrapX(x), WrapY(row));
        // A block the player placed is simply taken away again. A block of the land is recorded as mined.
        if (!placed.TryGetValue(spot, out Dictionary<int, SideBlock> put) || !put.Remove(level))
        {
            if (!mined.TryGetValue(spot, out HashSet<int> gone)) mined[spot] = gone = new HashSet<int>();
            gone.Add(level);
        }
        // If that was the top block, the top of this spot is now the next block down that is still there.
        int newTop = top[spot.x, spot.y];
        while (newTop > -GroundDepth && SideBlockAt(x, row, newTop - 1) == SideBlock.Air) newTop--;
        top[spot.x, spot.y] = (sbyte)newTop;
        RedrawAround(x, row);
        BlockMined?.Invoke(x, row, level);
        return was;
    }

    // Puts a block in an empty space. Returns false, doing nothing, if the space is taken or out of bounds.
    public bool Place(int x, int row, int level, SideBlock block)
    {
        if (level >= BuildLimit || level <= -GroundDepth || SideBlockAt(x, row, level) != SideBlock.Air) return false;

        var spot = new Vector2Int(WrapX(x), WrapY(row));
        if (!placed.TryGetValue(spot, out Dictionary<int, SideBlock> put)) placed[spot] = put = new Dictionary<int, SideBlock>();
        put[level] = block;
        if (level >= top[spot.x, spot.y]) top[spot.x, spot.y] = (sbyte)(level + 1);
        RedrawAround(x, row);
        return true;
    }

    // Redraws a spot and the four around it, since their edges and cliff faces depend on its height.
    // Doorways are drawn by looking a few squares along, so the redrawing reaches a little further than next door.
    void RedrawAround(int x, int y)
    {
        const int reach = 4;
        foreach (Vector2Int cell in window.allPositionsWithin)
        {
            int dx = WrapX(cell.x - x), dy = WrapY(cell.y - y);
            if (Mathf.Min(dx, width - dx) <= reach && Mathf.Min(dy, height - dy) <= reach) SetLandTile(cell);
        }
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

    // Smooth blobs of noise about blobSize squares across, with finer detail layered on top for each extra layer.
    // It joins up with itself across the edges of the planet, so the land has no seam. Returns roughly 0 to 1.
    float LoopingNoise(int x, int y, float blobSize, int noiseSeed, int layers)
    {
        // A whole number of blobs has to fit across the planet for the edges to meet.
        int across = Mathf.Max(1, Mathf.RoundToInt(width / blobSize)), down = Mathf.Max(1, Mathf.RoundToInt(height / blobSize));
        float sum = 0f, amplitude = 0.5f, total = 0f;
        for (int i = 0; i < layers; i++)
        {
            sum += GridNoise(x * across / (float)width, y * down / (float)height, across, down, noiseSeed + i * 101) * amplitude;
            total += amplitude;
            amplitude *= 0.5f;
            across *= 2;
            down *= 2;
        }
        return sum / total;
    }

    // Random values on a grid, blended smoothly between grid points. The grid repeats after `across` and `down` points.
    static float GridNoise(float x, float y, int across, int down, int noiseSeed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
        int x1 = (x0 + 1) % across, y1 = (y0 + 1) % down;
        x0 %= across;
        y0 %= down;
        float a = Hash(x0, y0, noiseSeed), b = Hash(x1, y0, noiseSeed), c = Hash(x0, y1, noiseSeed), d = Hash(x1, y1, noiseSeed);
        return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
    }

    // A repeatable random value from 0 up to (but not including) 1 for a grid point.
    static float Hash(int x, int y, int hashSeed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393) ^ (uint)(y * 668265263) ^ (uint)(hashSeed * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h >> 8) / 16777216f;
        }
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
