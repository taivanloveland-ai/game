using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

// Builds the frozen crash planet when the scene starts: ground, rock, the wrecked ship, the player and the camera.
// Every spot has a height in blocks. The top-down view is angled: higher ground is drawn shifted up the screen,
// with a cliff face showing on its south side. Mining mode uses the same heights to draw a row from the side.
//
// The planet is round like the Earth, and the map of it is twice as wide as it is tall. Walking east or west far
// enough brings you back to where you started. Walking north over the North Pole brings you down the far side of
// the planet, half way round, walking south (and the same over the South Pole). Positions in the game just keep
// counting past the edges of the map; whenever the land is looked up, the position is wrapped back into the planet.
// Only the land around the player is ever drawn.
public class PlanetOne : MonoBehaviour
{
    // The size of the planet in squares. Not saved in the scene, so these numbers are always the ones used.
    [System.NonSerialized] public int width = 5760;
    [System.NonSerialized] public int height = 2880;
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
    // For testing only, and off in the real game: an extra frost cave right by the crash site, in a little rock
    // cliff a few squares east of the ship.
    [System.NonSerialized] public bool caveByCrashSite = false;
    // For testing only, and to be turned off later: in creative mode, a straight road of snow from the crash site to
    // both ice caps, cut flat through the mountains in the north and frozen over the ocean in the south, and one along
    // the equator to the algae.
    [System.NonSerialized] public bool roadToCapsInCreative = true;
    // For testing only, and 1 in the real game: every hit of the ice axe does this many times its usual damage, to
    // spiders and to the ice pillar.
    [System.NonSerialized] public int axeDamageTimes = 4;
    // In creative mode: gives the player every item they do not have yet.
    System.Action giveEverything;
    // Whether the world being made gets that road.
    bool makeRoad;

    // Which planet is being made: Planet 1, the ice planet, or (true) the rocky planet, the next planet the fixed ship
    // flies to (Taivan's request of 2026-10-18). The rocky planet is red desert rock, 1.25 times as big, and for now
    // just its ground: no ice caps, ocean, algae, mountains, caves, plants, spiders, ice pillar or weather. It is made
    // the same way as Planet 1 otherwise, from the same saved game's seed, with every colour turned to the desert's.
    public static bool Rocky;
    public const int RockyWidth = 7200, RockyHeight = 3600;
    // For checking: a new game started with -rocky on the command line begins on the rocky planet.
    static bool StartRocky => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-rocky") >= 0;
    // A trip in the ship: the saved game to carry on with on the other planet once the scene has started again, its
    // slot, and whether the player begins sitting in the ship.
    static SaveGame.Loaded travelling;
    static int travellingSlot;
    static bool arriveAboard;

    // Where the player starts on a planet: just in front of the ship.
    public static Vector2 StartSpot(bool rocky)
    {
        int wide = rocky ? RockyWidth : 5760, tall = rocky ? RockyHeight : 2880;
        return new Vector2(wide / 2 + 0.5f, tall / 4 - 0.5f);
    }

    // What one block of a row looks like from the side in mining mode.
    public enum SideBlock : byte { Air, Snow, Ice, Stone, Rock, RockTop, Ship, Water, Algae }

    // How far up the screen one block of height is drawn in the top-down view, in tiles.
    public const float HeightScale = 0.5f;
    // The tallest anything gets: rock standing 2 blocks above raised ground.
    public const int MaxTop = 3;
    // Blocks can be placed up to this many blocks above the lowest ground, and no higher.
    public const int BuildLimit = 48;

    const byte Snow = 0, Ice = 1, Rock = 2, Ship = 3, Stone = 4;
    // The South Pole's ice cap: a block of snow on top with ice under it. And the freezing ocean round it.
    const byte SnowOverIce = 5, Water = 6;
    // Algae, as what a block is drawn as from above. (It shares its number with SnowOverIce, which is only ever a
    // kind of land and never what a block is drawn as.)
    const byte Algae = 5;

    // The algae biome: one big region on the equator where teal-green algae covers the snow and ice, and glows at
    // night. It is this many squares east or west of the crash site (which one depends on the world's seed), and
    // roughly this many squares from its middle to its edge, which wanders in and out in bumps about the third
    // number of squares across.
    const int AlgaeAway = 1000, AlgaeRadius = 300;
    const float AlgaeEdgeBlobSize = 90f;
    // Where the middle of this world's algae is, across, and the number its shapes are made from.
    int algaeX, algaeSeed;

    // The algae's shades, on the ground and on the maps alike: deep blue-teal, teal, his algae colour, and green. They
    // lie in big patches with sharp edges, never blended together. (Until 2026-10-18 the maps blended five shades
    // softly, and the ground was all one colour.)
    static readonly Color32[] AlgaeMapShades =
    {
        new Color32(28, 104, 122, 255), new Color32(40, 140, 140, 255), new Color32(46, 160, 142, 255), new Color32(66, 176, 126, 255),
    };
    const float AlgaeMapPatchSize = 70f;

    // Which shade a patch value (0 to 1) falls in.
    public static int AlgaeShadeOf(float patch) => Mathf.Clamp(Mathf.FloorToInt(patch * AlgaeMapShades.Length), 0, AlgaeMapShades.Length - 1);

    // One colour of algae on a map: patch (0 to 1) picks one of its shades, lined rows are a little lighter, and a band
    // round its edge (edge 0 in the middle, 1 at the edge) is paler. Every step is sharp. Used by the maps and the title.
    public static Color AlgaeMapColor(float patch, bool lined, float edge)
    {
        Color c = AlgaeMapShades[AlgaeShadeOf(patch)];
        if (lined) c = Color.Lerp(c, AlgaeLight, 0.22f);
        if (edge > 0.88f) c = Color.Lerp(c, new Color32(206, 228, 226, 255), 0.35f);
        return c;
    }

    // Which shade the algae is at a square, the same as on the map.
    public int AlgaeShadeAt(int x, int y)
    {
        Vector2Int spot = Wrap(x, y);
        return AlgaeShadeOf(Mathf.InverseLerp(0.25f, 0.75f, LoopingNoise(spot.x, spot.y, AlgaeMapPatchSize, algaeSeed + 11, 3)));
    }

    // One of his algae blocks (or the old speckled algae) in one of the shades: its algae colour changed to the shade,
    // its lines and specks left as they are. Made the first time each is needed.
    readonly Dictionary<int, BlockPicture> shadedAlgae = new Dictionary<int, BlockPicture>();
    Color32 algaeBase;
    BlockPicture ShadedAlgae(int look, int shade)
    {
        int key = look * 16 + shade;
        if (shadedAlgae.TryGetValue(key, out BlockPicture picture)) return picture;
        Color32[] pixels = (Color32[])algaePictures[look].Pixels.Clone();
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].r == algaeBase.r && pixels[i].g == algaeBase.g && pixels[i].b == algaeBase.b) pixels[i] = AlgaeMapShades[shade];
        }
        var texture = new Texture2D(algaePictures[look].Width, algaePictures[look].Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        texture.SetPixels32(pixels);
        texture.Apply();
        picture = LoadBlockPicture(texture);
        shadedAlgae[key] = picture;
        return picture;
    }
    // How strongly the algae glows in the middle of the night: how solid its bright copy is, drawn over the dark.
    const float AlgaeGlow = 0.6f;
    public static readonly Color32 AlgaeColor = new Color32(46, 160, 142, 255), AlgaeLight = new Color32(126, 218, 194, 255), AlgaeDark = new Color32(30, 122, 114, 255);
    static readonly Color32 AlgaeRim = new Color32(112, 204, 184, 255), AlgaeFace = new Color32(28, 108, 102, 255), AlgaeFoot = new Color32(20, 84, 82, 255);
    // How many blocks deep the ground goes. The very bottom layer cannot be mined.
    public const int GroundDepth = 14;
    // A cliff face in the top-down view is never drawn taller than this many blocks; deeper holes look the same from above.
    const int MaxDrawnDrop = 4;
    // The tallest south face a square is ever drawn with, in blocks.
    const int MaxFaceBlocks = 21;

    // Each pole sits in the middle of a flat, roughly round sheet of ice, this many squares from the pole to its edge
    // give or take the second number. Its edge wanders in and out in bumps about the third number of squares across.
    // On the map, which is flat, the ice shows as a strip along the top and another along the bottom.
    const int CapRadius = 120, CapWobble = 30;
    const float CapBlobSize = 200f;
    // The South Pole's ice cap is snowy: snow over ice, with patches of bare ice about this many squares across where
    // their noise is above the second number. Under its one block of snow the ice goes this many blocks deep.
    const float CapBareIceBlobSize = 18f, CapBareIceAbove = 0.64f;
    const int CapIceDepth = 4;
    // A freezing ocean surrounds the South Pole's ice cap, about this many squares wide, give or take the second
    // number, its far shore wandering in bumps about the third number of squares across. The water is this many
    // blocks deep, with stone under it.
    const int OceanWidth = 150, OceanWobble = 25, OceanDepth = 4;
    const float OceanBlobSize = 160f;
    // A ring of mountains like the Himalayas stands round the North Pole's ice sheet, this many squares deep. Its
    // peaks are this many blocks tall. (Round the South Pole, the ocean is where the mountains were.)
    const int RingDepth = 300;
    const int MountainHeight = 30;
    // The ring fades in and out over this many squares at its inner and outer edges.
    const int MountainEdge = 40;
    // Roughly how many squares across a mountain is. Mountains stand where their noise is above the first number,
    // and reach full height where it is above that by the second.
    const float MountainBlobSize = 60f, MountainAbove = 0.4f, MountainRise = 0.4f;
    // Ridges and peaks: roughly how many squares apart the ridges are.
    const float RidgeBlobSize = 30f;
    // Along the ridges, peaks are about this many squares apart, and how much they stand above the saddles between.
    const float PeakBlobSize = 20f, PeakShare = 0.6f;
    // How a mountain rises, after his sketch and his photo of the Himalayas: a long, gentle rise at the foot that gets
    // steeper and steeper, up to a narrow spire at the peak. For each band of height, this is how wide each block of
    // rise is, in sixths of a square. Made very steep on 2026-10-12: a short ramp of a block a square at the foot,
    // then two blocks a square, then three, and from 14 blocks up six a square: sheer cliffs. No spot rises faster
    // than this from the spot next to it.
    const int SlopeUnitsPerSquare = 6;
    static readonly int[] SlopeBandTops = { 4, 8, 14, 20, 26, 30 };
    static readonly int[] SlopeBandWidths = { 6, 3, 2, 1, 1, 1 };
    // Between these heights the mountainside is broken into flat ledges this many blocks apart, with sheer cliff faces
    // between them: good places for cave mouths later. A cliff of 4 cannot be jumped up.
    const int LedgesFrom = 8, LedgesTo = 20, LedgeStep = 4;
    // Here and there, in small patches about this many squares across, the ledges are not cut, so the slope there
    // happens to give a way up to the next ledge. Patches stand where their noise is above this.
    const float WayUpBlobSize = 14f, WayUpAbove = 0.8f;
    // Mountains this tall or taller have snow on top.
    const int SnowCapFrom = 18;
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
    const int WindowHalfHeight = 34;
    // Roughly how many squares across the big shapes of the land are: snow, ice and rock; raised ground; snow depth.
    const float LandBlobSize = 26f, RaisedBlobSize = 17f, DepthBlobSize = 8f;

    // Where the land noise is above the first number it is rock, and below the second it is ice. Raised ground is where
    // its own noise is above the third.
    const float RockAbove = 0.62f, IceBelow = 0.38f, RaisedAbove = 0.6f;

    // In a passage one square wide, the ground over the player turns see-through this many squares along it.
    const int SeeThroughRadius = 2;
    // A room's roof turns see-through at most this many squares from the player.
    const int RoomReach = 14;
    // Blocks drawn this close to the player turn see-through, in blocks.
    const float CoverMargin = 0.3f;
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
    // Snow comes in three looks: Taivan's three snow pictures. Plain white stands in if the third is missing.
    const int SnowLooks = 3;
    static readonly Color32 PlainSnow = new Color32(255, 255, 255, 255);
    static readonly Color32 SnowRim = new Color32(200, 214, 228, 255), SnowFace = new Color32(176, 194, 214, 255), SnowFoot = new Color32(152, 173, 197, 255);
    static readonly Color32 IceRim = new Color32(200, 232, 246, 255), IceFace = new Color32(104, 166, 206, 255), IceFoot = new Color32(84, 144, 188, 255);
    static readonly Color32 RockEdge = new Color32(138, 152, 170, 255);
    static readonly Color32 CliffFace = new Color32(59, 70, 86, 255), CliffCrack = new Color32(48, 58, 73, 255), CliffFoot = new Color32(40, 49, 62, 255);
    static readonly Color32 ShipRim = new Color32(150, 158, 170, 255);
    // Bare stone, as seen from above where the snow or ice over it has been dug away.
    static readonly Color32 StoneColor = new Color32(70, 80, 96, 255), StoneRim = new Color32(98, 110, 128, 255);
    // The freezing ocean: dark, cold blue, with lighter ripples. A ripple is { x, y, length } on the 16-pixel tile.
    public static readonly Color32 WaterColor = new Color32(38, 78, 122, 255), WaterLight = new Color32(78, 128, 176, 255);
    // The top of the water is see-through, so the seabed and anyone who has sunk show through it.
    static readonly Color32 WaterClear = new Color32(38, 78, 122, 150), WaterLightClear = new Color32(78, 128, 176, 190);
    // The seabed seen through the water is drawn this much darker than stone in the open.
    static readonly Color SeabedTint = new Color(0.5f, 0.58f, 0.72f);
    Tile seabedTile;
    static readonly Color32 WaterRim = new Color32(96, 146, 190, 255), WaterFace = new Color32(28, 60, 98, 255), WaterFoot = new Color32(20, 46, 78, 255);
    static readonly int[][][] WaterRipples =
    {
        new int[0][],
        new[] { new[] { 2, 4, 4 }, new[] { 9, 11, 5 } },
        new[] { new[] { 6, 7, 4 }, new[] { 1, 13, 3 }, new[] { 11, 2, 3 } },
    };
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

    BlockPicture snowPicture, snowPicture2, snowPicture3, icePicture;
    // Taivan's ice blocks, one look each, picked block by block like the snow; null if there are none.
    BlockPicture[] icePictures;
    // Whether the ship is drawn from Taivan's picture (see CrashedShip) rather than as blocks.
    bool shipPictured;
    // Which of them is plain, with no marks at all (-1 for none). It shows on this share of the ice, and the others
    // share the rest.
    int blankIce = -1;
    const float BlankIceShare = 0.6f;
    // Taivan's algae blocks, null if there are none. Their bright lines run on from one into the next, so they are laid
    // in order going east, first to last and round again, and the lines flow unbroken across the algae.
    BlockPicture[] algaePictures;
    // How many of them he drew, and where the bright lines are across all of them side by side: true for a pixel of
    // a line, bottom row first.
    int algaeLooks;
    bool[] algaeLineMask;
    int algaeStripWide, algaeStripTall;

    // The algae comes alive with the music, day and night. The bright lines stay where he drew them, and light runs
    // along inside them, east, slowly while the music is quiet and faster when it is loud. On the big booms of the
    // drums all the lines flash bright. (Until 2026-10-16 the whole algae flashed, which he did not like.) At night it
    // glows as well. These are drawn on layers of their own over the night's dark, cut away wherever something nearer
    // covers the algae (a rock or a raised block in front), and hidden behind the player and the things growing on it.
    // How fast the light runs along the lines, in his pixels a second (32 to a square), when the music is quiet and
    // when it is loudest; how far apart the patches of light are along the lines, and how long each one is (from
    // its middle to where it fades out), in his pixels.
    const float LightRunsQuiet = 8f, LightRunsLoud = 64f, LightApart = 64f, LightHalfLength = 10f;
    // How bright a boom makes the lines flash, and how fast that fades, in shares a second. A boom is the low notes
    // suddenly this many times as loud as they have been lately.
    const float BoomFlash = 1f, BoomFade = 2.5f, BoomJump = 1.6f;
    // Squares this many rows nearer are looked at for anything covering a square of algae.
    const int CoverRows = 24;
    Tilemap algaeFlash, algaeLight;
    // Listens for the drum hits of the night theme, so the lines can flash to them.
    readonly BeatFollower beats = new BeatFollower();
    bool followingNightSong;
    float lightPhase, boom, loudness, slowLevel, fastLevel, slowBass, fastBass, peakBass, nextBoomAt;
    int lightShown;
        // The squares of algae being drawn: how high their tops are, and how many of his pixels up from the bottom of each
    // something nearer covers it.
    readonly Dictionary<Vector2Int, Vector2Int> algaeTops = new Dictionary<Vector2Int, Vector2Int>();
    readonly Dictionary<long, Tile> algaeLayerTiles = new Dictionary<long, Tile>();

    byte[,] kind;
    // Height of the walkable ground at each spot (0 or 1), and the height of whatever stands there (rock and ship are taller).
    byte[,] ground;
    // The top of each spot as the world was made, and as it is now after any mining. Mining can take it below zero.
    byte[,] baseTop;
    sbyte[,] top;
    // The blocks that have been mined away, by spot: the levels removed from that spot's column.
    Dictionary<Vector2Int, HashSet<int>> mined = new Dictionary<Vector2Int, HashSet<int>>();

    // The blocks the player has placed, by spot: the level each is at and what it is. A placed block can sit above
    // empty space, as the roof of a house does.
    Dictionary<Vector2Int, Dictionary<int, SideBlock>> placed = new Dictionary<Vector2Int, Dictionary<int, SideBlock>>();

    // Raised after a block is mined, with its spot (unwrapped, as given) and level.
    public event System.Action<int, int, int> BlockMined;
    // How many blocks deep the ice and the snow go at each spot, seen from the side.
    byte[,] iceDepth;
    byte[,] snowDepth;
    // How many blocks tall the mountain is at each spot, or 0 where there is none.
    byte[,] mountain;
    // 1 where algae covers the top of the ground. Null in a cave.
    byte[,] algae;
    Tilemap algaeGlow;
    DayNight sky;
    float glowShown = -1f;
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

    // The square the player starts on, just in front of the wrecked ship.
    public Vector2Int CrashSite { get; private set; }

    // How far each pole's ice sheet reaches at each column of the map, in squares from the pole.
    int[] northCap, southCap;
    // How far the South Pole's snowy cap reaches up a column of the map, in squares from the bottom: the first row of
    // the ocean beyond it.
    public int SouthCapEdge(int x) => southCap[((x % width) + width) % width];
    // How far the ocean round the South Pole reaches at each column of the map, in squares from the pole.
    int[] southOcean;

    // The squares of land being drawn around the player right now, in unwrapped positions.
    public RectInt LandWindow => window;

    void Awake()
    {
        Square = PaintSprite((x, y) => new Color32(255, 255, 255, 255));

        // Drawn pictures, where they exist, are used for the top of tiles here and for blocks in mining mode.
        snowPicture = LoadBlockPicture("Art/SnowBlock");
        snowPicture2 = LoadBlockPicture("Art/SnowBlock2");
        snowPicture3 = LoadBlockPicture("Art/SnowBlock3");
        icePicture = LoadBlockPicture("Art/IceBlock");
        icePictures = LoadBlockPictures("Art/IceBlocks");
        if (icePictures != null) icePicture = icePictures[0];
        LoadAlgae();
        for (int i = 0; icePictures != null && i < icePictures.Length && blankIce < 0; i++)
        {
            if (System.Array.TrueForAll(icePictures[i].Pixels, c => c.Equals(icePictures[i].Pixels[0]))) blankIce = i;
        }
        IceSideSprites = MakeIceSideSprites();
        shipPictured = CrashedShip.HasPicture;

        // All the writing on screen is in the game's own font.
        gameObject.AddComponent<GameFont>();

        // A fresh start, whether the game has just opened or has come back to the title screen from a game.
        Time.timeScale = 1f;
        GameInput.Blocked = false;
        GameInput.InShip = false;
        GameInput.PointerOverInventory = null;
        GameInput.PointerOverMap = null;
        GameInput.PointerOverTools = null;

        cam = Camera.main;
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

        // Landing on the other planet in the ship: straight into the game saved for the trip.
        if (travelling != null)
        {
            SaveGame.Loaded trip = travelling;
            travelling = null;
            var landing = gameObject.AddComponent<TitleScreen>();
            landing.Setup(this, cam);
            landing.Choose(trip.Creative, travellingSlot, trip, trip.Name);
            return;
        }
#if UNITY_EDITOR
        // Started again by Update Content on the pause screen: straight back into the game that was saved.
        string resume = PlayerPrefs.GetString(ResumeAfterUpdateKey, "");
        if (resume.Length > 0)
        {
            PlayerPrefs.DeleteKey(ResumeAfterUpdateKey);
            PlayerPrefs.Save();
            string[] parts = resume.Split(' ');
            bool creative = parts[0] == "creative";
            int slot = int.Parse(parts[1]);
            SaveGame.Loaded saved = SaveGame.Load(creative, slot);
            if (saved != null)
            {
                var resumed = gameObject.AddComponent<TitleScreen>();
                resumed.Setup(this, cam);
                resumed.Choose(creative, slot, saved, saved.Name);
                return;
            }
        }
#endif
        // The game opens on the title screen, and the world is made once a mode has been picked there. (Started
        // with -skipTitle on the command line, as the automatic checks are, it goes straight into survival mode;
        // with -creative as well, into creative mode. "-slot 3" plays in the third save slot, and -load with it
        // carries on the game saved there.)
        string[] arguments = System.Environment.GetCommandLineArgs();
        if (System.Array.IndexOf(arguments, "-skipTitle") >= 0)
        {
            bool creative = System.Array.IndexOf(arguments, "-creative") >= 0;
            int at = System.Array.IndexOf(arguments, "-slot");
            saveSlot = at >= 0 && at + 1 < arguments.Length ? int.Parse(arguments[at + 1]) - 1 : -1;
            if (saveSlot >= 0 && System.Array.IndexOf(arguments, "-load") >= 0) loading = SaveGame.Load(creative, saveSlot);
            saveName = loading != null ? loading.Name : "Test";
            MakeWorld(creative);
            FinishSetup(creative);
            return;
        }
        var title = gameObject.AddComponent<TitleScreen>();
        title.Setup(this, cam);
    }

    // Left by Update Content on the pause screen: which saved game to go straight back into when the game starts
    // again ("creative 2", or "survival 0").
    public const string ResumeAfterUpdateKey = "ResumeAfterUpdate";

    Camera cam;
    // What is being made right now, for the loading screen, and whether the game has begun.
    public volatile string LoadingStage = "";
    public bool Ready { get; private set; }
    // In creative mode, the colour of every square of the planet, for a map that starts fully drawn.
    Color32[] wholeMap;

    // The save slot this game is played in, counting from 0, or -1 for none; and the saved game being carried on, if
    // this is not a new one.
    int saveSlot = -1;
    SaveGame.Loaded loading;
    // The name the player gave this game's save.
    string saveName = "";

    // Begins a game from the title screen: survival, or creative, in a save slot. Given a saved game, it carries that
    // game on; otherwise it starts a new one.
    public void StartGame(bool creative, int slot = -1, SaveGame.Loaded saved = null, string name = "")
    {
        saveSlot = slot;
        loading = saved;
        saveName = saved != null ? saved.Name : name;
        StartCoroutine(Load(creative));
    }

    // The world is worked out away from the main thread, so the loading screen keeps moving while it is made.
    System.Collections.IEnumerator Load(bool creative)
    {
        var making = System.Threading.Tasks.Task.Run(() => MakeWorld(creative));
        while (!making.IsCompleted) yield return null;
        if (making.IsFaulted) Debug.LogException(making.Exception);
        LoadingStage = "Waking up at the ship";
        // Two frames, so that line is on screen before the last of the setting up, which holds the screen still.
        yield return null;
        yield return null;
        FinishSetup(creative);
    }

    // Works out the whole world: its land, its mountains and its caves. Nothing here draws anything or touches the
    // scene, so it can run away from the main thread.
    void MakeWorld(bool creative)
    {
        // Which planet: the saved game says, and a new game begins on Planet 1.
        Rocky = loading != null ? loading.Planet == 1 : StartRocky;
        width = Rocky ? RockyWidth : 5760;
        height = Rocky ? RockyHeight : 2880;
        // The ship came down in the southern half of the planet, half way between the equator and the South Pole.
        int cx = width / 2, cy = height / 4;
        CrashSite = new Vector2Int(cx, cy - 1);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var picker = new System.Random();
        // A saved game's world is made again from its own seed. A new game's world is picked at random.
        bool pick = randomWorld && loading == null && !Rocky;
        makeRoad = creative && roadToCapsInCreative && !Rocky;
        if (loading != null) seed = loading.Seed;
        if (pick) seed = picker.Next(1, 1000000);
        GenerateLand(cx, cy);
        float reachable = ReachableShare(cx, cy);
        // A random world is thrown away and picked again if rock walls off too much of it from the crash site.
        for (int tries = 0; pick && tries < 5 && reachable < MinReachableShare; tries++)
        {
            seed = picker.Next(1, 1000000);
            GenerateLand(cx, cy);
            reachable = ReachableShare(cx, cy);
        }
        // Logged so a world worth keeping can be built again by typing its seed in and unticking Random World.
        Debug.Log("World seed: " + seed + " (" + Mathf.RoundToInt(reachable * 100f) + "% of its snow and ice can be walked to, worked out in " + (timer.ElapsedMilliseconds / 1000f).ToString("F1") + " seconds)");
        LoadingStage = "Digging the frost caves";
        if (!Rocky) FindCaveEntrances();
        if (creative)
        {
            // In creative mode the whole map is drawn from the start.
            // (Which rows of the algae have lines is worked out first, before the map is drawn on many cores at once.)
            RowIsLined(0);
            LoadingStage = "Drawing the map";
            var colours = new Color32[width * height];
            System.Threading.Tasks.Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++) colours[y * width + x] = MapColorAt(x, y);
            });
            wholeMap = colours;
        }
    }

    // Puts the world on screen and sets the game going: the land, the player, and everything else in the game.
    void FinishSetup(bool creative)
    {
        int cx = width / 2, cy = height / 4;
        if (Rocky) MakeDesertPictures();
        BuildTerrain();

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
        sky = dayNight;
        mining.Setup(this, controller, follow, cam, dayNight);
        dayNight.Setup(cam, mining, Square);
        gameObject.AddComponent<Music>().Setup(dayNight);

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
        controller.WearingCleats = () => inventory.Boots != null && inventory.Boots == nook.Cleats;
        // Creative mode starts with the tools and blocks that never run out (and, below, every other item).
        if (startWithPickaxe || creative) inventory.AddTool(nook.Pickaxe);
        if (creative) inventory.AddTool(nook.Axe);
        inventory.Infinite = creative;
        int startBlocks = creative ? 999 : startWithBlocks;
        var pickaxeWork = gameObject.AddComponent<Mining>();
        pickaxeWork.Setup(this, controller, mining, inventory, hud, pickups, nook, gatherables, cam);
        if (startBlocks > 0)
        {
            var blocks = creative
                ? new[] { pickaxeWork.SnowItem, pickaxeWork.IceItem, pickaxeWork.StoneItem, pickaxeWork.AlgaeItem }
                : new[] { pickaxeWork.SnowItem, pickaxeWork.IceItem, pickaxeWork.StoneItem };
            foreach (Item block in blocks)
            {
                inventory.Add(block, startBlocks);
                int slot = System.Array.IndexOf(inventory.Hotbar, null);
                if (slot >= 0) inventory.SetSlot(slot, block);
            }
        }
        gameObject.AddComponent<Building>().Setup(this, controller, mining, inventory, hud, gatherables, cam);
        // The player wakes up beside the ship, where they started.
        var health = gameObject.AddComponent<Health>();
        health.Setup(this, controller, mining, inventory, pickups, hud, new Vector2(cx + 0.5f, cy - 0.5f));
        var spiders = gameObject.AddComponent<Spiders>();
        spiders.Setup(this, controller, mining, dayNight, health, inventory, nook, pickups, cam);
        // A thornam bush can only be cut down with the ice axe in hand.
        gatherables.AxeEquipped = () => nook.IsAxe(inventory.Equipped);
        gatherables.Say = text => hud.Say(text, 2f);
        // The maps mark the ship at the middle of its hull.
        var worldMap = gameObject.AddComponent<WorldMap>();
        worldMap.Setup(this, controller, new Vector2(cx + 0.5f, cy + 2f), wholeMap);
        wholeMap = null;
        // The frost caves in the mountains. Falling in one, the player wakes up at the ship as usual.
        var caves = gameObject.AddComponent<Caves>();
        caves.Setup(this, controller, mining, cam, dayNight, gatherables, spiders, pickups, worldMap);
        health.BeforeWaking = caves.LeaveNow;
        hud.HideHotbar = () => worldMap.Open;
        var weather = gameObject.AddComponent<Weather>();
        weather.Setup(this, controller, cam, dayNight, health, hud, Square);
        // The ice boss, which rises on the polar ice caps.
        gameObject.AddComponent<IceBoss>().Setup(this, controller, mining, health, inventory, nook, hud, cam, Square, weather, pickups, gatherables);
        // Creative mode has every item in the game, never running out.
        if (creative) giveEverything = () =>
        {
            if (!inventory.Tools.Contains(nook.ThornAxe)) inventory.AddTool(nook.ThornAxe);
            if (!inventory.Tools.Contains(nook.CrystalAxe)) inventory.AddTool(nook.CrystalAxe);
            if (!inventory.Tools.Contains(nook.ThornPickaxe)) inventory.AddTool(nook.ThornPickaxe);
            if (!inventory.Tools.Contains(nook.CrystalPickaxe)) inventory.AddTool(nook.CrystalPickaxe);
            foreach (Item item in new[]
            {
                gatherables.PlantItem, gatherables.ShardItem, gatherables.ClumpItem, gatherables.ThornItem, gatherables.VineItem,
                gatherables.StickItem, spiders.SilkItem, spiders.LegItem, spiders.FangItem, GetComponent<IceBoss>().OrbItem, nook.Cleats,
                nook.CrystalShard, nook.Battery,
            })
            {
                if (item != null && inventory.CountOf(item) == 0) inventory.Add(item, 999);
            }
        };
        giveEverything?.Invoke();
        // The wrecked ship as he drew it, smoking.
        var crashedShip = gameObject.AddComponent<CrashedShip>();
        crashedShip.Setup(this, nook, mining, new Vector2(cx + 0.5f, cy + 1f));
        nook.OnShip = crashedShip.Covers;
        // Getting in the fixed ship, and flying it.
        var flight = gameObject.AddComponent<ShipFlight>();
        // From the rocky planet, the way back is to Planet 1; and the ship stands on desert sand.
        if (Rocky)
        {
            flight.NextPlanetName = "Planet 1";
            flight.HomeName = "the rocky planet";
            flight.PaintNext = ShipFlight.PaintIcy;
            flight.PaintHome = ShipFlight.PaintRocky;
            flight.NextColor = new Color32(0xE4, 0xEE, 0xF4, 255);
            flight.GroundColor = Desert(SnowColors[0][0]);
            flight.GroundShade = Desert(SnowFace);
            flight.GroundSpeck = Desert(RockEdge);
        }
        flight.Setup(this, controller, mining, nook, crashedShip, hud, cam, dayNight);
        hud.HideHotbar = () => worldMap.Open || flight.Aboard;
        // The battery the ship needs, on the South Pole's cap.
        gameObject.AddComponent<SouthPoleBattery>().Setup(this, controller, mining, inventory, nook, hud);
        // The glowing crystals in the frost caves, and the caves' dark.
        var crystals = gameObject.AddComponent<CaveCrystals>();
        crystals.Setup(this, controller, mining, inventory, nook, hud, cam, pickups);
        pickaxeWork.ClickTaken = crystals.TakesClick;
        // Creative mode also has a panel for the player's speed, the weather and the time of day.
        if (creative) gameObject.AddComponent<CreativeTools>().Setup(controller, weather, dayNight);

        // Saved games and the pause screen. A saved game is put back now that everything is in place. A new game is
        // saved straight away, so its slot is taken.
        var saves = gameObject.AddComponent<SaveGame>();
        saves.Setup(this, controller, mining, dayNight, inventory, pickups, gatherables, nook, pickaxeWork, health, spiders, worldMap, caves, saveSlot, creative, saveName);
        gameObject.AddComponent<PauseMenu>().Setup(this, saves, cam, hud, worldMap, nook);
        // Reaching the other planet in the ship: the game is saved for the trip, and the other planet is made.
        flight.Arrived = () => TravelOnward(saves);
        if (loading != null) saves.Apply(loading);
        else if (saveSlot >= 0) saves.Save();
        // A creative game saved before every item was given out gets the ones it is missing.
        giveEverything?.Invoke();
        loading = null;
        // Just landed here in the ship: the player sits in it, seeing it from behind on the new ground.
        if (arriveAboard)
        {
            arriveAboard = false;
            flight.GetIn();
            hud.Say("You landed on " + (Rocky ? "the rocky planet" : "Planet 1") + "!", 4f);
        }
        Ready = true;
    }

    // Flies on to the other planet: saves the game as it will be there (the player in the ship in front of its
    // workbench), and starts the scene again to make that planet.
    void TravelOnward(SaveGame saves)
    {
        travelling = saves.Travel(!Rocky);
        travellingSlot = saveSlot;
        arriveAboard = true;
        GameSettings.Keep();
        Time.timeScale = 1f;
        GameInput.InShip = false;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
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
        // The player hides the algae's glow and light, which are drawn over the dark of night, so they are not drawn over
        // the player. (It is kept out of the sorted group, where it would only hide the player's own pictures.)
        var hider = new GameObject("Glow Hider");
        hider.transform.SetParent(player.transform, false);
        hider.transform.localScale = bodyVisual.transform.localScale;
        hider.AddComponent<SpriteMask>().sprite = Square;
        controller.GlowHider = hider.transform;
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
        LoadingStage = "Shaping the land";
        // The rocky planet is made from the same seed, mixed so it is not shaped like Planet 1.
        var rand = new System.Random(Rocky ? seed * 7919 + 13 : seed);
        int landSeed = rand.Next(), raisedSeed = rand.Next(), depthSeed = rand.Next();
        variantSeed = rand.Next();
        int mountainSeed = rand.Next();
        int capSeed = rand.Next();
        algaeSeed = rand.Next();
        // East of the crash site in some worlds, west in others.
        algaeX = cx + (rand.Next(2) == 0 ? AlgaeAway : -AlgaeAway);

        // How far each ice sheet reaches, column by column.
        northCap = new int[width];
        southCap = new int[width];
        southOcean = new int[width];
        for (int x = 0; x < width; x++)
        {
            northCap[x] = CapRadius + Mathf.RoundToInt((LoopingNoise(x, height / 2, CapBlobSize, capSeed, 2) * 2f - 1f) * CapWobble);
            southCap[x] = CapRadius + Mathf.RoundToInt((LoopingNoise(x, 0, CapBlobSize, capSeed, 2) * 2f - 1f) * CapWobble);
            southOcean[x] = southCap[x] + OceanWidth + Mathf.RoundToInt((LoopingNoise(x, 0, OceanBlobSize, capSeed + 7, 2) * 2f - 1f) * OceanWobble);
            // The rocky planet has no ice caps and no ocean.
            if (Rocky) northCap[x] = southCap[x] = southOcean[x] = 0;
        }

        kind = new byte[width, height];
        ground = new byte[width, height];
        baseTop = new byte[width, height];
        top = new sbyte[width, height];
        algae = new byte[width, height];
        mined.Clear();
        placed.Clear();
        // The rows are worked out side by side on all the computer's cores, since each spot only depends on itself.
        System.Threading.Tasks.Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                // Round each pole, flat at ground level: plain ice in the north, snow over ice in the south with
                // patches of bare ice.
                bool south = y < height / 2;
                if (FromPole(x, y) < CapAt(x, y))
                {
                    kind[x, y] = south && LoopingNoise(x, y, CapBareIceBlobSize, capSeed + 9, 2) <= CapBareIceAbove ? SnowOverIce : Ice;
                    continue;
                }
                // And round the South Pole's ice cap, the freezing ocean.
                if (south && y < southOcean[x])
                {
                    kind[x, y] = Water;
                    continue;
                }
                float fromCrash = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                // The algae biome, on the equator.
                float algaeAcross = Mathf.Abs(x - algaeX), algaeUp = y - height / 2;
                algaeAcross = Mathf.Min(algaeAcross, width - algaeAcross);
                if (!Rocky && algaeAcross < AlgaeRadius * 1.4f && Mathf.Abs(algaeUp) < AlgaeRadius * 1.4f)
                {
                    float reach = AlgaeRadius * (0.7f + 0.6f * LoopingNoise(x, y, AlgaeEdgeBlobSize, algaeSeed, 2));
                    if (algaeAcross * algaeAcross + algaeUp * algaeUp < reach * reach) algae[x, y] = 1;
                }
                float e = LoopingNoise(x, y, LandBlobSize, landSeed, 3);
                // Flatten the land around the crash site so the start is open snow.
                e = 0.5f + (e - 0.5f) * Mathf.Clamp01((fromCrash - 6f) / 6f);

                // (On the rocky planet there is no ice: the ground is all desert, with rocks.)
                kind[x, y] = e > RockAbove ? Rock : e < IceBelow && !Rocky ? Ice : Snow;
                // Patches of ground one block higher. The crash site itself stays at ground level.
                ground[x, y] = (byte)(fromCrash > 8f && LoopingNoise(x, y, RaisedBlobSize, raisedSeed, 3) > RaisedAbove ? 1 : 0);
            }
        });
        // The wrecked ship sits just above the spot where the player starts, six squares wide and two deep. (It was
        // five wide until Taivan drew it, 2026-10-17, then three wide; then nine wide and three deep when he made it 3
        // times bigger, and six when he made that 2 times, 2026-10-18.)
        for (int y = cy + 1; y < cy + 3; y++)
        {
            for (int x = cx - 2; x < cx + 4; x++) kind[x, y] = Ship;
        }

        LoadingStage = "Raising the mountains";
        if (Rocky) mountain = new byte[width, height];
        else RaiseMountains(mountainSeed);
        if (makeRoad) MakeRoadToCaps(cx, cy);

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
                    for (int nx = x - 1; nx <= x + 1; nx++) { Vector2Int n = Wrap(nx, ny); highest = Mathf.Max(highest, ground[n.x, n.y]); }
                }
                baseTop[x, y] = (byte)(highest + RockHeight);
                top[x, y] = (sbyte)baseTop[x, y];
            }
        }
        LoadingStage = "Freezing the lakes";
        ComputeGroundDepths(depthSeed);
        LoadingStage = "Checking the way is clear";
    }

    // Works out how tall the mountains are everywhere and turns the land under them to rock. Like the Himalayas:
    // foothills that ramp up, mountainsides broken into ledges and cliffs, and steep, pointed peaks along ridges.
    // They stand in a ring round each pole's ice sheet.
    void RaiseMountains(int mountainSeed)
    {
        mountain = new byte[width, height];
        // The rows the ring can reach, nearest the top of the map. There are none round the South Pole, where the
        // ocean is.
        int reach = CapRadius + CapWobble + RingDepth + 1;
        var rows = new int[reach];
        for (int i = 0; i < reach; i++) rows[i] = height - reach + i;

        System.Threading.Tasks.Parallel.For(0, rows.Length, i =>
        {
            int y = rows[i];
            for (int x = 0; x < width; x++)
            {
                // Full height through the middle of the ring, falling away toward the ice sheet and toward the
                // lowlands beyond.
                int into = FromPole(x, y) - CapAt(x, y);
                float strength = Mathf.Clamp01(Mathf.Min(into, RingDepth - into) / (float)MountainEdge);
                if (strength <= 0f) continue;
                float rise = Mathf.Clamp01((LoopingNoise(x, y, MountainBlobSize, mountainSeed, 2) - MountainAbove) / MountainRise) * strength;
                if (rise <= 0f) continue;
                // Ridges: sharp lines of high ground where the ridge noise is near its middle, with valleys between. This
                // only says how high a spot may be; how high it really is comes from how far it is from lower ground,
                // below, which gives every mountain its long foot and steep, pointed top.
                float ridge = 1f - Mathf.Abs(LoopingNoise(x, y, RidgeBlobSize, mountainSeed + 3, 3) * 2f - 1f);
                // Along a ridge, peaks stand where this noise is high, with saddles between them.
                float peak = LoopingNoise(x, y, PeakBlobSize, mountainSeed + 5, 2);
                float wanted = MountainHeight * Mathf.Clamp01(rise * 2f) * ridge * ridge * (1f - PeakShare + PeakShare * peak);
                int tall = Mathf.Clamp(Mathf.RoundToInt(wanted), 0, MountainHeight);
                // Kept for now as how far in from the foot of the mountain a spot this tall has to be.
                mountain[x, y] = (byte)RunTo(tall);
            }
        });

        // Wear the mountains into shape: no spot may be more than one square further in from the foot than the spot
        // next to it. That gives every mountain its foothills and steepening sides, and brings its top to a point.
        // Sweeping forwards and then backwards a few times settles the whole range.
        bool changed = true;
        for (int pass = 0; pass < 8 && changed; pass++)
        {
            changed = false;
            for (int i = 0; i < rows.Length; i++)
            {
                int y = rows[i];
                for (int x = 0; x < width; x++) changed |= WearDown(x, y, Wrap(x - 1, y), Wrap(x, y - 1));
            }
            for (int i = rows.Length - 1; i >= 0; i--)
            {
                int y = rows[i];
                for (int x = width - 1; x >= 0; x--) changed |= WearDown(x, y, Wrap(x + 1, y), Wrap(x, y + 1));
            }
        }

        // Turn how far in each spot is into how tall it is, cut the mountainsides into ledges, and turn the land under
        // the mountains into rock.
        System.Threading.Tasks.Parallel.For(0, rows.Length, i =>
        {
            int y = rows[i];
            for (int x = 0; x < width; x++)
            {
                if (mountain[x, y] == 0) continue;
                int tall = 0;
                while (tall < MountainHeight && RunTo(tall + 1) <= mountain[x, y]) tall++;
                bool wayUp = LoopingNoise(x, y, WayUpBlobSize, mountainSeed + 4, 1) > WayUpAbove;
                if (tall >= LedgesFrom && tall <= LedgesTo && !wayUp) tall = LedgesFrom + (tall - LedgesFrom) / LedgeStep * LedgeStep;
                mountain[x, y] = (byte)tall;
                if (tall > 0) kind[x, y] = Rock;
            }
        });
    }

    // For testing: a road 3 squares wide running straight north and south, just east of the crag in front of the
    // northern cave, from one ice cap to the other, and a short stretch from the crash site across to it; and (since
    // 2026-10-17) a road along the equator from it to the middle of the algae. Along them the land is flat at ground
    // level: rock is snow, and the ocean is frozen into ice.
    void MakeRoadToCaps(int cx, int cy)
    {
        int roadX = cx + RoadEast;
        void Flatten(int x, int y)
        {
            Vector2Int spot = Wrap(x, y);
            mountain[spot.x, spot.y] = 0;
            ground[spot.x, spot.y] = 0;
            if (kind[spot.x, spot.y] == Rock) kind[spot.x, spot.y] = Snow;
            if (kind[spot.x, spot.y] == Water) kind[spot.x, spot.y] = Ice;
        }
        for (int y = 0; y < height; y++)
        {
            for (int x = roadX; x < roadX + 3; x++) Flatten(x, y);
        }
        for (int y = cy - 3; y < cy; y++)
        {
            for (int x = cx; x < roadX; x++) Flatten(x, y);
        }
        // And along the equator from that road to the middle of the algae, the short way round.
        int toAlgae = algaeX - roadX;
        if (toAlgae > width / 2) toAlgae -= width;
        if (toAlgae < -width / 2) toAlgae += width;
        for (int y = height / 2 - 1; y <= height / 2 + 1; y++)
        {
            for (int step = 0; step != toAlgae; step += System.Math.Sign(toAlgae)) Flatten(roadX + step, y);
        }
    }

    // How far east of the crash site the road to the ice caps runs: clear of the crag in front of the northern cave.
    public const int RoadEast = 14;

    // How far in from the edge of a polar ice cap a square on the surface is, in squares: 0 or less off the caps (and
    // in the caves). North tells which cap.
    public int IntoIceCap(int x, int y, out bool north)
    {
        Vector2Int spot = Wrap(x, y);
        north = spot.y >= height / 2;
        return InCave ? 0 : CapAt(spot.x, spot.y) - FromPole(spot.x, spot.y);
    }

    // True for a square on one of the polar ice caps, on the surface: north tells which.
    public bool OnIceCap(int x, int y, out bool north)
    {
        Vector2Int spot = Wrap(x, y);
        north = spot.y >= height / 2;
        return !InCave && FromPole(spot.x, spot.y) < CapAt(spot.x, spot.y);
    }

    // How many squares a spot is from the nearer pole: from the bottom of the map, or from the top.
    int FromPole(int x, int y)
    {
        return y < height / 2 ? y : height - 1 - y;
    }

    // How far the ice sheet of the nearer pole reaches at a spot's column, in squares from the pole.
    int CapAt(int x, int y)
    {
        return y < height / 2 ? southCap[x] : northCap[x];
    }

    // How far in from the foot of a mountain, in sixths of a square, its side has to go to be a number of blocks tall.
    static int RunTo(int tall)
    {
        int run = 0;
        for (int level = 1; level <= tall; level++)
        {
            run += SlopeAt(level);
        }
        return run;
    }

    // How wide one block of rise is at a height of the mountainside, in sixths of a square.
    static int SlopeAt(int level)
    {
        for (int band = 0; band < SlopeBandTops.Length; band++)
        {
            if (level <= SlopeBandTops[band]) return SlopeBandWidths[band];
        }
        return SlopeBandWidths[SlopeBandWidths.Length - 1];
    }

    // Lowers a spot of mountain to at most one square further in than its lower neighbour to the side or above or
    // below.
    bool WearDown(int x, int y, Vector2Int side, Vector2Int next)
    {
        int limit = Mathf.Min(mountain[side.x, side.y], mountain[next.x, next.y]) + SlopeUnitsPerSquare;
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
            // Only land that is walked on counts toward how much can be reached, not the mountains crossed on the way.
            if (mountain[p.x, p.y] == 0) reached++;
            foreach (Vector2Int step in steps)
            {
                Vector2Int n = Wrap(p + step);
                // Mountains can be climbed or dug through, so they do not wall anything off.
                if (seen[n.x, n.y] || (kind[n.x, n.y] == Rock && mountain[n.x, n.y] == 0) || kind[n.x, n.y] == Ship) continue;
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

        // Algae glows at night: a bright copy of its top is drawn over the dark of night, as solid as the night is deep.
        algaeGlow = MakeTilemap(grid, "Algae Glow", 101);
        algaeGlow.tileAnchor = terrain.tileAnchor;
        algaeGlow.color = new Color(1f, 1f, 1f, 0f);
        // Its lines flashing on the drums' booms, and the light running along them, over the glow.
        algaeFlash = MakeTilemap(grid, "Algae Flash", 102);
        algaeFlash.tileAnchor = terrain.tileAnchor;
        algaeFlash.color = new Color(1f, 1f, 1f, 0f);
        algaeLight = MakeTilemap(grid, "Algae Light", 103);
        algaeLight.tileAnchor = terrain.tileAnchor;
        // None of them is drawn over the player or the things growing on the algae: those hide them.
        foreach (Tilemap layer in new[] { algaeGlow, algaeFlash, algaeLight }) layer.GetComponent<TilemapRenderer>().maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
    }

    void Update()
    {
        if (topDownRoot == null || !topDownRoot.activeSelf) return;
        ShowLandAround(Vector2Int.FloorToInt(player.GroundPosition));
        ShowUnderRoof();
        float glow = sky != null && !InCave ? Mathf.Round(sky.Night * 32f) / 32f * AlgaeGlow : 0f;
        if (glow != glowShown)
        {
            glowShown = glow;
            algaeGlow.color = new Color(1f, 1f, 1f, glow);
        }
        LightUpAlgae();
    }

    // Loads his algae blocks and works out where their bright lines are.
    void LoadAlgae()
    {
        algaePictures = LoadBlockPictures("Art/AlgaeBlocks");
        if (algaePictures == null) return;
        Color32[] pixels = ReadPixels(Resources.Load<Texture2D>("Art/AlgaeBlocks"));
        algaeLooks = algaePictures.Length;
        algaeStripTall = algaePictures[0].Height;
        algaeStripWide = algaeStripTall * algaeLooks;
        // The algae is the commonest colour; everything else is the lines.
        var counts = new Dictionary<Color32, int>();
        foreach (Color32 c in pixels)
        {
            counts.TryGetValue(c, out int n);
            counts[c] = n + 1;
        }
        Color32 algae = default;
        int most = 0;
        foreach (KeyValuePair<Color32, int> entry in counts)
        {
            if (entry.Value > most) { most = entry.Value; algae = entry.Key; }
        }
        algaeLineMask = new bool[algaeStripWide * algaeStripTall];
        int sheetWide = pixels.Length / algaeStripTall;
        for (int y = 0; y < algaeStripTall; y++)
        {
            for (int x = 0; x < algaeStripWide; x++) algaeLineMask[y * algaeStripWide + x] = !pixels[y * sheetWide + x].Equals(algae);
        }
        algaeBase = algae;
        // And for the rows between the lined ones, the algae as it was before his blocks: speckled, with no lines, in
        // its three looks. Kept after his blocks. Each speck is drawn as big as it always was (two of the old tile's
        // sixteen pixels), at his blocks' size.
        System.Array.Resize(ref algaePictures, algaeLooks + PlainAlgaeLooks);
        int scale = algaeStripTall / TilePixels;
        for (int look = 0; look < PlainAlgaeLooks; look++)
        {
            var plain = new Texture2D(algaeStripTall, algaeStripTall, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var fill = new Color32[algaeStripTall * algaeStripTall];
            for (int i = 0; i < fill.Length; i++) fill[i] = AlgaePixel(look, (i % algaeStripTall) / scale, (algaeStripTall - 1 - i / algaeStripTall) / scale);
            plain.SetPixels32(fill);
            plain.Apply();
            algaePictures[algaeLooks + look] = LoadBlockPicture(plain);
        }
    }

    // Whether each row of the planet has lines in its algae: a lined row, then one to three plain rows (how many is
    // picked at random), then a lined row again, and so on. Worked out for each world the first time it is needed.
    bool[] linedRows;
    int linedRowsSeed;
    const int PlainRowsFrom = 1, PlainRowsTo = 3;
    // The plain algae has this many looks: the speckled algae drawn before his blocks.
    const int PlainAlgaeLooks = 3;

    bool RowIsLined(int y)
    {
        if (linedRows == null || linedRows.Length != height || linedRowsSeed != variantSeed)
        {
            linedRows = new bool[height];
            linedRowsSeed = variantSeed;
            var rand = new System.Random(variantSeed + 5303);
            for (int row = 0; row < height; row += 1 + rand.Next(PlainRowsFrom, PlainRowsTo + 1)) linedRows[row] = true;
        }
        return linedRows[((y % height) + height) % height];
    }

    // How many of his pixels up from the bottom of a square of algae something nearer covers it: a rock, a cliff or a
    // raised block in front, drawn further up the screen than the ground behind it.
    int AlgaeCovered(int x, int y, int level)
    {
        float bottom = y + level * HeightScale, covered = 0f;
        for (int k = 1; k <= CoverRows; k++) covered = Mathf.Max(covered, y - k + 1 + DrawnTopAt(x, y - k) * HeightScale - bottom);
        return Mathf.Clamp(Mathf.CeilToInt(covered * algaeStripTall - 0.001f), 0, algaeStripTall);
    }

    // One square of one of the algae's layers: kind 0 is its glow (his picture), 1 its flash (its lines, white), and
    // 2 the light in its lines (white, as bright as the light is there). See-through below the covered rows, and at the
    // height of the square's top.
    Tile AlgaeLayerTile(int kind, int level, int look, int covered, int phase, int shade = 0)
    {
        long key = ((long)kind << 56) | ((long)(level + 64) << 40) | ((long)look << 32) | ((long)shade << 24) | ((long)covered << 16) | (long)phase;
        if (algaeLayerTiles.TryGetValue(key, out Tile tile)) return tile;
        int size = algaeStripTall;
        var pixels = new Color32[size * size];
        for (int y = covered; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int along = look * size + x;
                if (kind == 0) pixels[y * size + x] = ShadedAlgae(look, shade).Pixels[y * size + x];
                // Plain algae has no lines to flash or light.
                else if (look >= algaeLooks) continue;
                else if (kind == 1) { if (algaeLineMask[y * algaeStripWide + along]) pixels[y * size + x] = new Color32(255, 255, 255, 255); }
                else if (algaeLineMask[y * algaeStripWide + along])
                {
                    // The patches of light along the lines, each brightest in its middle.
                    float from = Mathf.Repeat(along - phase, LightApart);
                    float light = Mathf.Clamp01(1f - Mathf.Min(from, LightApart - from) / LightHalfLength);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(light * 255f));
                }
            }
        }
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply();
        // Raised the same way as the top of a tile (see TerrainTile).
        var pivot = new Vector2(0.5f, (TilePixels - level * FacePixels) / (float)TilePixels);
        tile = MakeTile(Color.white, Sprite.Create(texture, new Rect(0, 0, size, size), pivot, size), false);
        algaeLayerTiles[key] = tile;
        return tile;
    }

    // Puts a square's glow, flash and light on their layers, or takes them away if it is not algae on top.
    void ShowAlgaeLayers(Vector2Int cell)
    {
        int level = DrawnTopAt(cell.x, cell.y);
        var at = (Vector3Int)cell;
        if (MaterialOf(SideBlockAt(cell.x, cell.y, level - 1)) != Algae)
        {
            algaeTops.Remove(cell);
            algaeGlow.SetTile(at, null);
            algaeFlash.SetTile(at, null);
            algaeLight.SetTile(at, null);
            return;
        }
        int covered = AlgaeCovered(cell.x, cell.y, level), look = AlgaeLookAt(cell.x, cell.y);
        algaeTops[cell] = new Vector2Int(level, covered);
        algaeGlow.SetTile(at, AlgaeLayerTile(0, level, look, covered, 0, AlgaeShadeAt(cell.x, cell.y)));
        algaeFlash.SetTile(at, AlgaeLayerTile(1, level, look, covered, 0));
        algaeLight.SetTile(at, AlgaeLayerTile(2, level, look, covered, lightShown));
    }

    // Listens to the music: how loud it is now against how loud it has been lately, and whether the low notes just
    // boomed.
    void HearMusic()
    {
        float dt = Time.unscaledDeltaTime;
        // Heard as it plays, before the music volume turns it down, so it is the same however loud the music is set.
        float level = Music.Level;
        slowLevel = Mathf.Lerp(slowLevel, level, Mathf.Clamp01(dt * 0.5f));
        fastLevel = Mathf.Lerp(fastLevel, level, Mathf.Clamp01(dt * 12f));
        loudness = slowLevel > 0.0001f ? Mathf.Clamp01(fastLevel / (slowLevel * 2f)) : 0f;
        // The low notes, where the drums boom.
        float bass = Music.Bass;
        slowBass = Mathf.Lerp(slowBass, bass, Mathf.Clamp01(dt * 0.7f));
        fastBass = Mathf.Lerp(fastBass, bass, Mathf.Clamp01(dt * 20f));
        // To the day theme the lines flash on its big booms only: nearly as loud as the loudest of the last few seconds.
        // To the night theme they flash on every drum hit.
        bool nightSong = sky != null && sky.Night >= 0.5f;
        peakBass = Mathf.Max(peakBass * Mathf.Exp(-dt * 0.3f), fastBass);
        if (!nightSong && fastBass > slowBass * BoomJump && fastBass >= peakBass * 0.9f && fastBass > 0.01f && Time.time >= nextBoomAt)
        {
            boom = 1f;
            nextBoomAt = Time.time + 0.4f;
        }
        if (nightSong != followingNightSong)
        {
            followingNightSong = nightSong;
            beats.Forget();
        }
        if (beats.Hear(bass, Time.unscaledTime, dt) && nightSong) boom = 1f;
        boom = Mathf.Max(0f, boom - BoomFade * dt);
    }

    // Runs the light along the algae's lines and flashes it on the booms.
    void LightUpAlgae()
    {
        if (algaePictures == null) return;
        HearMusic();
        lightPhase = Mathf.Repeat(lightPhase + Mathf.Lerp(LightRunsQuiet, LightRunsLoud, loudness) * Time.deltaTime, LightApart);
        int phase = Mathf.FloorToInt(lightPhase) % (int)LightApart;
        if (phase != lightShown)
        {
            lightShown = phase;
            var cells = new Vector3Int[algaeTops.Count];
            var tiles = new TileBase[algaeTops.Count];
            int i = 0;
            foreach (KeyValuePair<Vector2Int, Vector2Int> entry in algaeTops)
            {
                cells[i] = (Vector3Int)entry.Key;
                tiles[i] = AlgaeLayerTile(2, entry.Value.x, AlgaeLookAt(entry.Key.x, entry.Key.y), entry.Value.y, phase);
                i++;
            }
            algaeLight.SetTiles(cells, tiles);
        }
        bool outside = !InCave;
        algaeFlash.color = new Color(0.92f, 1f, 0.98f, outside ? boom * BoomFlash : 0f);
        algaeLight.color = new Color(1f, 1f, 1f, outside ? 0.85f : 0f);
    }

    // For checking: how far the light has run, how loud the music is, and the flash of the last boom.
    public float AlgaeLightPhase => lightPhase;
    public float MusicLoudness => loudness;
    public float MusicBoom => boom;
    public BeatFollower Beats => beats;

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
        // Any blocks drawn over the player turn see-through, however much of the player they hide: down in the
        // ground, and up on the surface behind a wall, a mountain or a step up.
        ShowCovering(at, level);
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
                    Fade(wall, TerrainTile(MaterialOf(block), 0, level + 1, 0, 0, 0), WallFootprintTint);
                }
            }
        }
    }

    // Below the ground, in a hole or a tunnel, the player is drawn as deep as they really are, so the higher ground
    // just south of them, which is drawn in front, would cover them. On the surface, a wall, a mountain or higher
    // ground south of the player is drawn in front of them too. The blocks that cover them turn see-through,
    // and beneath each one the land is shown cut through at the player's depth: open space as its floor, solid
    // ground as a darker block, so the player can see the sides of the hole or tunnel they are in.
    void ShowCovering(Vector2Int at, int level)
    {
        // Where the player is drawn on screen, with a little room round it.
        Vector2 ground = player.GroundPosition;
        float bottom = ground.y - 0.2f + player.FeetHeight * HeightScale - CoverMargin;
        float topEdge = bottom + PlayerController.Height + CoverMargin * 2f;
        float leftEdge = ground.x - PlayerController.Width / 2f - CoverMargin, rightEdge = ground.x + PlayerController.Width / 2f + CoverMargin;

        // Only squares south of the player are drawn in front of them. A square's picture reaches from the foot of
        // its south face to the far edge of its top.
        // Tall things are drawn far up the screen, so they can cover the player from a long way south.
        for (int y = at.y - 1; y >= at.y - BuildLimit / 2 - 2; y--)
        {
            for (int x = Mathf.FloorToInt(leftEdge); x <= Mathf.FloorToInt(rightEdge); x++)
            {
                int top = DrawnTopAt(x, y);
                if (top <= level) continue;
                // Water is see-through already.
                if (SideBlockAt(x, y, top - 1) == SideBlock.Water) continue;
                float from = y + (top - FaceDrop(top, DrawnTopAt(x, y - 1))) * HeightScale, to = y + 1f + top * HeightScale;
                if (to <= bottom || from >= topEdge) continue;
                var cell = new Vector2Int(x, y);
                SideBlock block = SideBlockAt(x, y, level);
                if (block == SideBlock.Air) Fade(cell, TunnelFloorTile(x, y, level + 1), Color.white);
                else Fade(cell, TerrainTile(MaterialOf(block), 0, level + 1, 0, 0, 0), WallFootprintTint);
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
        // It is drawn at the height of its own top, down in the ground, so the player stands on it.
        return TerrainTile(MaterialOf(SideBlockAt(x, y, floor)), 0, floor + 1, 0, 0, 0);
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
            algaeGlow.SetTile((Vector3Int)cell, null);
            algaeFlash.SetTile((Vector3Int)cell, null);
            algaeLight.SetTile((Vector3Int)cell, null);
            algaeTops.Remove(cell);
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
        int variant = !pictured && rims == 0 && drop == 0 ? (int)(HashAt(x, y, variantSeed) * looks) : 0;
        if (material == Snow && snowPicture != null) variant = SnowLookAt(x, y, level - 1);
        if (material == Ice && icePictures != null) variant = IceLookAt(x, y, level - 1);
        // Taivan's picture of the ship is drawn over its squares, so from above they are just the snow it stands on.
        if (material == Ship && shipPictured) return TerrainTile(Snow, 0, 0, 0, 0, snowPicture != null ? SnowLookAt(x, y, -1) : 0);
        // Which of his blocks, and in which shade: the shade goes above the block's number.
        if (material == Algae && algaePictures != null) variant = AlgaeLookAt(x, y) | (AlgaeShadeAt(x, y) << 4);

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
        Vector2Int spot = Wrap(x, y);
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
        int drawnTop = DrawnTopAt(cell.x, cell.y);
        byte surface = MaterialOf(SideBlockAt(cell.x, cell.y, drawnTop - 1));
        if (algaePictures != null)
        {
            ShowAlgaeLayers(cell);
            // This square may cover algae further back, or no longer cover it.
            for (int k = 1; k <= CoverRows; k++)
            {
                var behind = new Vector2Int(cell.x, cell.y + k);
                if (algaeTops.ContainsKey(behind)) ShowAlgaeLayers(behind);
            }
        }
        else algaeGlow.SetTile((Vector3Int)cell, surface == Algae ? TerrainTile(Algae, Mathf.Clamp(drawnTop, 0, 1), drawnTop, 0, 0, 0) : null);
        bool ice = surface == Ice;
        terrain.SetTile((Vector3Int)cell, TileAt(cell.x, cell.y, ice));
        // The ocean is see-through too, with its stone bed drawn beneath it.
        if (surface == Water)
        {
            if (seabedTile == null) seabedTile = MakeTile(SeabedTint, TerrainTile(Stone, 0, 0, 0, 0, 0).sprite, false);
            iceBeneath.SetTile((Vector3Int)cell, seabedTile);
        }
        else iceBeneath.SetTile((Vector3Int)cell, ice ? TileAt(cell.x, cell.y, false) : null);
        doorFloors.SetTile((Vector3Int)cell, FloorInsideAt(cell.x, cell.y));
    }

    // One tile of the top-down view: the top of the block, plus the cliff face below it when the ground to the south
    // is lower. FaceBlocks says what each block of that face is; see FaceBlocks above.
    // Glassy is for ice that reflections show through.
    Tile TerrainTile(byte material, int groundLevel, int level, int drop, int rims, int variant, long faceBlocks = 0, bool glassy = false)
    {
        // The south face can be up to MaxFaceBlocks tall, which is too much to fit in one number with the rest.
        var key = ((long)(material | (groundLevel << 3) | ((level + 32) << 4) | (drop << 11) | (rims << 16) | (glassy ? 1 << 23 : 0)) | ((long)variant << 32), faceBlocks);
        if (terrainTiles.TryGetValue(key, out Tile tile)) return tile;

        // Snow has three looks, picked square by square: Taivan's three snow pictures.
        // So has ice: Taivan's ice blocks.
        BlockPicture picture = material == Snow && snowPicture != null ? SnowPicture(variant) : material == Ice && icePictures != null ? icePictures[variant] : material == Algae && algaePictures != null ? ShadedAlgae(variant & 15, variant >> 4) : PictureFor(material);
        bool pictured = picture != null;
        Color32 topColor, rimColor;
        switch (material)
        {
            case Ice: topColor = IceBase[groundLevel]; rimColor = IceRim; break;
            case Rock: topColor = RockColors[variant % RockColors.Length]; rimColor = RockEdge; break;
            case Ship: topColor = ShipColor; rimColor = ShipRim; break;
            case Stone: topColor = StoneColor; rimColor = StoneRim; break;
            case Water: topColor = WaterColor; rimColor = WaterRim; break;
            case Algae: topColor = AlgaeColor; rimColor = AlgaeRim; break;
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
        // On the rocky planet every colour is turned to the desert's.
        Sprite sprite = PaintSprite(TilePixels * detail, spriteHeight * detail, pivot, TilePixels * detail, fine, (fineX, fineY) => DesertIf(TilePixel(fineX, fineY)));
        Color32 TilePixel(int fineX, int fineY)
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
            if (pictured)
            {
                // A picture of ice is a little see-through too where reflections show beneath it.
                Color32 drawn = PicturePixel(picture, fineX, fineY);
                if (glassy) drawn.a = IceGlassAlpha;
                return drawn;
            }
            if (material == Water) return WaterPixel(variant, x, y);
            if (material == Algae) return AlgaePixel(variant, x, y);
            if (material != Ice) return topColor;
            // The top of ice is a little see-through when it is glassy, so reflections drawn beneath it show.
            Color32 ice = IcePixel(groundLevel, variant, fineX, fineY);
            if (glassy) ice.a = IceGlassAlpha;
            return ice;
        }
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

    // Which of the three looks a block of snow has: Taivan's three snow pictures, numbered 0 to 2.
    // It is picked at random for each block, but always the same for the same block.
    public int SnowLookAt(int x, int y, int level)
    {
        return Mathf.Min(SnowLooks - 1, (int)(HashAt(x, y, variantSeed + 7919 * (level + 64)) * SnowLooks));
    }

    // Which of Taivan's ice blocks a block of ice looks like, numbered from 0. Picked at random for each block, but
    // always the same for the same block, with the plain one more often than the rest. 0 if there are none.
    public int IceLookAt(int x, int y, int level)
    {
        if (icePictures == null) return 0;
        float roll = HashAt(x, y, variantSeed + 6007 * (level + 64));
        if (blankIce < 0 || icePictures.Length < 2) return Mathf.Min(icePictures.Length - 1, (int)(roll * icePictures.Length));
        if (roll < BlankIceShare) return blankIce;
        // One of the others, skipping over the plain one.
        int other = Mathf.Min(icePictures.Length - 2, (int)((roll - BlankIceShare) / (1f - BlankIceShare) * (icePictures.Length - 1)));
        return other >= blankIce ? other + 1 : other;
    }

    // Which of Taivan's algae blocks a square of algae shows: along a row they go in order from west to east, so the
    // lines run on unbroken, but each row starts at a different one of them, so a column is not all the same block.
    // 0 if there are none.
    public int AlgaeLookAt(int x, int y)
    {
        if (algaePictures == null) return 0;
        // The old speckled algae, without lines, on the rows between the lined ones.
        if (!RowIsLined(y)) return algaeLooks + Mathf.Min(PlainAlgaeLooks - 1, (int)(HashAt(x, y, variantSeed + 7177) * PlainAlgaeLooks));
        int start = (int)(HashAt(0, y, variantSeed + 4241) * algaeLooks);
        return ((x + start) % algaeLooks + algaeLooks) % algaeLooks;
    }

    // The picture for a look of algae, or null if there are no algae pictures.
    public Sprite AlgaeLookSprite(int look)
    {
        return algaePictures != null ? algaePictures[look].Sprite : null;
    }

    // The picture for a look of ice, or null if there are no ice pictures.
    public Sprite IceLookSprite(int look)
    {
        return icePictures != null ? icePictures[look].Sprite : null;
    }

    // The picture for a look of snow, or null for plain white (only if the third picture is missing).
    public Sprite SnowLookSprite(int look)
    {
        return SnowPicture(look)?.Sprite;
    }

    BlockPicture SnowPicture(int look)
    {
        // If the second picture is missing, its share of the snow uses the first one.
        return look == 0 ? snowPicture : look == 1 ? snowPicture2 ?? snowPicture : snowPicture3;
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
        return texture == null ? null : LoadBlockPicture(texture);
    }

    // Loads a picture of several blocks side by side, each one square, as one block picture each, cut out without
    // changing a pixel. Returns null if there is no picture of that name.
    static BlockPicture[] LoadBlockPictures(string path)
    {
        var sheet = Resources.Load<Texture2D>(path);
        if (sheet == null) return null;
        Color32[] pixels = ReadPixels(sheet);
        int size = sheet.height, count = sheet.width / size;
        var pictures = new BlockPicture[count];
        for (int i = 0; i < count; i++)
        {
            var cut = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++) cut[y * size + x] = pixels[y * sheet.width + i * size + x];
            }
            var frame = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            frame.SetPixels32(cut);
            frame.Apply();
            pictures[i] = LoadBlockPicture(frame);
        }
        return pictures;
    }

    static BlockPicture LoadBlockPicture(Texture2D texture)
    {
        Color32[] pixels = ReadPixels(texture);

        // A block is solid, so any see-through pixel left in a picture (a stray one or two can slip in while drawing)
        // is filled with white instead of showing a hole. Everything else is used exactly as drawn.
        bool filled = false;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a == 255) continue;
            pixels[i] = Color32.Lerp(new Color32(255, 255, 255, 255), new Color32(pixels[i].r, pixels[i].g, pixels[i].b, 255), pixels[i].a / 255f);
            filled = true;
        }
        if (filled)
        {
            var solid = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false) { filterMode = texture.filterMode, wrapMode = TextureWrapMode.Clamp };
            solid.SetPixels32(pixels);
            solid.Apply();
            texture = solid;
        }
        return new BlockPicture
        {
            Sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width),
            Pixels = pixels,
            Width = texture.width,
            Height = texture.height,
        };
    }

    // Reads the pixels of a picture whether or not Unity imported it as readable, by drawing it and reading the drawing back.
    public static Color32[] ReadPixels(Texture2D picture)
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

    // One pixel of the top of the ocean: dark, cold water, with a few short lighter ripples on two of its three looks.
    static Color32 WaterPixel(int variant, int x, int y)
    {
        foreach (int[] ripple in WaterRipples[variant])
        {
            if (y == ripple[1] && x >= ripple[0] && x < ripple[0] + ripple[2]) return WaterLightClear;
        }
        return WaterClear;
    }

    // One pixel of the top of a block of algae: teal-green, mottled with lighter and darker flecks. Each of its
    // three looks has its flecks in different places.
    public static Color32 AlgaePixel(int variant, int x, int y)
    {
        // The flecks are two pixels across.
        int h = (x / 2 * 73856093) ^ (y / 2 * 19349663) ^ (variant * 83492791);
        h = (h ^ (h >> 13)) * 0x5bd1e995;
        int roll = (h >> 15) & 31;
        return roll < 2 ? AlgaeLight : roll < 6 ? AlgaeDark : AlgaeColor;
    }

    // The south-facing side of a raised block. y counts down from the top of the face.
    static Color32 PaintFace(byte material, int x, int y, int facePixels)
    {
        bool foot = y >= facePixels - 2;
        switch (material)
        {
            case Ice: return foot ? IceFoot : IceFace;
            case Ship: return ShipSideColor;
            case Water: return foot ? WaterFoot : WaterFace;
            case Algae: return y < 2 ? AlgaeRim : foot ? AlgaeFoot : AlgaeFace;
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

        // Rows are worked out side by side on all the computer's cores, since each spot only depends on itself.
        System.Threading.Tasks.Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                if (kind[x, y] == Ice) iceDepth[x, y] = (byte)Mathf.Min(distance[x, y], MaxIceDepth);
                // Snow is 4 or 5 blocks deep, changing gently from place to place.
                snowDepth[x, y] = (byte)(LoopingNoise(x, y, DepthBlobSize, depthSeed, 1) > 0.5f ? 5 : 4);
            }
        });
    }

    // ---- Frost caves ----
    // Caves are not part of the land above. Each is a world of its own, made the first time it is visited, and
    // digging or building down there changes nothing up top. Going in swaps the land being shown and walked on for
    // the cave's; coming out swaps it back.

    // One stretch of land: the planet's surface, or a cave.
    class Land
    {
        public int Width, Height;
        public byte[,] Kind, Ground, BaseTop, IceDepth, SnowDepth, Mountain, Algae;
        public sbyte[,] Top;
        public Dictionary<Vector2Int, HashSet<int>> Mined;
        public Dictionary<Vector2Int, Dictionary<int, SideBlock>> Placed;
        // In a cave, the way out: a tunnel dug into the north wall of the square the player arrives on.
        public CaveHole Exit;
    }

    // How many caves there are, all in the ring of mountains round the North Pole, and how far apart their mouths are
    // at the least, in squares.
    public const int CaveCount = 6;
    const int CaveSpacing = 350;
    // Cave mouths are on the inner side of the ring of mountains, at most this many squares out from the edge of the
    // pole's ice sheet.
    const int CaveNearIce = 70;
    // A cave mouth is on a mountain ledge at least this high, which nobody can reach by walking and jumping, with a
    // cliff at least this many blocks tall behind it (to its north), where the hole is.
    const int CaveMouthFrom = 12, CaveMouthTo = 22, CaveCliff = 3;
    // Nothing may stand in front of a mouth, as seen from above, for this many squares south of its ledge.
    const int CaveClearView = 8;
    // The cave that is always due north of the crash site has its crag this many squares out from the North Pole's ice.
    const int CragFromIce = 10;
    // That crag is roughly this many squares from its middle to its edge, east to west and north to south.
    const float CragHalfWidth = 7.5f, CragHalfDepth = 6f;
    // A cave is this many squares across, and its stone walls are this many blocks tall: too tall to jump.
    public const int CaveSize = 160;
    const int CaveWallHeight = 4;
    // The cave is dug by a few tunnels that wander from the way in, branching now and then and opening out into
    // rooms. They stay this far from the cave's edges.
    const int CaveMargin = 14, CaveTunnelSteps = 1800, MostCaveTunnels = 14;

    Land surface;
    Land[] caveLands = new Land[CaveCount];
    int caveIndex = -1;

    // The squares of the cave mouths on the planet's surface, each in front of the hole in the cliff north of it,
    // and the level of the ledge each is on.
    public Vector2Int[] CaveMouths { get; private set; } = new Vector2Int[0];
    public int[] CaveMouthLevels { get; private set; } = new int[0];
    // The mouths themselves, dug into the cliffs; and in a cave, the way out.
    public CaveHole[] CaveHoles { get; private set; } = new CaveHole[0];
    public CaveHole CaveExit => InCave ? caveLands[caveIndex].Exit : null;
    // True while the player is in a cave, and which one.
    public bool InCave => caveIndex >= 0;
    public int CaveIndex => caveIndex;
    // Where the player comes out in a cave: the square in front of the hole they came in by, in its north wall.
    public static Vector2Int CaveArrival => new Vector2Int(CaveSize / 2, CaveSize / 2);

    // Picks the cave mouths: ledges high on the mountains that walking and jumping can never reach, however the
    // player goes. Only digging or building gets there.
    void FindCaveEntrances()
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        bool[,] reached = WalkableFromLowlands();
        PickCaveEntrances(reached, timer);
    }

    // Everywhere that can be reached on foot from the lowlands. From one square to the next the player can go
    // down any distance, and up at most one block (a jump is one and a half). To be safe this allows two, in
    // case something like a fern is stood on to get the extra block.
    bool[,] WalkableFromLowlands()
    {
        var reached = new bool[width, height];
        var queue = new Queue<Vector2Int>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (mountain[x, y] > 0) continue;
                reached[x, y] = true;
                // The search only needs to start from the lowland squares at the foot of a mountain.
                bool foot = false;
                foreach (Vector2Int side in Sides)
                {
                    Vector2Int n = Wrap(x + side.x, y + side.y);
                    foot |= mountain[n.x, n.y] > 0;
                }
                if (foot) queue.Enqueue(new Vector2Int(x, y));
            }
        }
        while (queue.Count > 0)
        {
            Vector2Int p = queue.Dequeue();
            int from = top[p.x, p.y];
            foreach (Vector2Int side in Sides)
            {
                Vector2Int n = Wrap(p + side);
                if (reached[n.x, n.y] || top[n.x, n.y] > from + 2) continue;
                reached[n.x, n.y] = true;
                queue.Enqueue(n);
            }
        }
        return reached;
    }

    void PickCaveEntrances(bool[,] reached, System.Diagnostics.Stopwatch timer)
    {
        // Every ledge that would do, in a random order that is the same for the same world.
        var spots = new List<Vector2Int>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int level = top[x, y];
                if (reached[x, y] || mountain[x, y] == 0 || level < CaveMouthFrom || level > CaveMouthTo) continue;
                Vector2Int north = Wrap(x, y + 1), south = Wrap(x, y - 1);
                if (top[north.x, north.y] < level + CaveCliff || top[south.x, south.y] > level) continue;
                // The mouth is four squares wide, so the ledge in front of it is made four wide too (see ShapeMouth).
                // That must not open a way up: nothing that can be walked to may be within a jump of that ledge.
                bool safe = true;
                for (int lx = x - 2; lx <= x + 3 && safe; lx++)
                {
                    // The squares west, east and south of the four ledge squares.
                    Vector2Int beside = lx < x - 1 || lx > x + 2 ? Wrap(lx, y) : Wrap(lx, y - 1);
                    if (reached[beside.x, beside.y] && top[beside.x, beside.y] >= level - 2) safe = false;
                }
                if (!safe) continue;
                // Nothing south of the mouth may stand in front of it on screen: the ground just south of the ledge
                // no higher than the ledge, and further south no more than two blocks higher for each square away.
                bool clear = true;
                for (int lx = x - 1; lx <= x + 2 && clear; lx++)
                {
                    for (int away = 1; away <= CaveClearView && clear; away++)
                    {
                        Vector2Int inFront = Wrap(lx, y - away);
                        if (top[inFront.x, inFront.y] > level + 2 * (away - 1)) clear = false;
                    }
                }
                if (!clear) continue;
                if (FromPole(x, y) - CapAt(x, y) > CaveNearIce) continue;
                spots.Add(new Vector2Int(x, y));
            }
        }
        var rand = new System.Random(seed + 11);
        for (int i = spots.Count - 1; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            (spots[i], spots[j]) = (spots[j], spots[i]);
        }

        // All the caves are in the northern mountains (the south has the ocean instead), well apart from each other.
        // One of them is always in the same place: due north of the crash site, right at the edge of the North Pole's
        // ice. It comes first, so the others keep their distance from it.
        var mouths = new List<Vector2Int> { MakeNorthCrag() };
        foreach (bool north in new[] { true })
        {
            // A couple more than are needed are picked, in case shaping one opens a way up to it (see below).
            int wanted = CaveCount + 2;
            foreach (Vector2Int spot in spots)
            {
                if (mouths.Count >= wanted) break;
                if ((spot.y >= height / 2) != north) continue;
                bool apart = true;
                foreach (Vector2Int other in mouths)
                {
                    if (Vector2.Distance(Nearest(spot, other), other) < CaveSpacing) apart = false;
                }
                if (apart) mouths.Add(spot);
            }
        }
        // The ledge and cliff at each mouth are made wide and tall enough for the opening. Then the whole check is
        // done again, and any mouth whose ledge can now be walked to after all is left out.
        var levels = new List<int>();
        foreach (Vector2Int mouth in mouths) levels.Add(top[mouth.x, mouth.y]);
        for (int i = 0; i < mouths.Count; i++) ShapeMouth(mouths[i], levels[i]);
        bool[,] after = WalkableFromLowlands();
        int leftOut = 0;
        for (int i = mouths.Count - 1; i >= 0; i--)
        {
            bool walkable = false;
            for (int x = mouths[i].x - 1; x <= mouths[i].x + 2; x++)
            {
                Vector2Int ledge = Wrap(x, mouths[i].y);
                walkable |= after[ledge.x, ledge.y];
            }
            if (!walkable) continue;
            // The one due north of the crash site is always kept: its crag is built to stand clear of everything.
            if (i == 0)
            {
                Debug.LogWarning("Frost caves: the cave due north of the crash site can be walked to");
                continue;
            }
            mouths.RemoveAt(i);
            levels.RemoveAt(i);
            leftOut++;
        }
        if (leftOut > 0) Debug.Log("Frost caves: " + leftOut + " mouths left out because shaping them opened a way up");
        // The spare ones are not used.
        while (mouths.Count > CaveCount)
        {
            mouths.RemoveAt(mouths.Count - 1);
            levels.RemoveAt(levels.Count - 1);
        }
        if (caveByCrashSite)
        {
            Vector2Int test = MakeCliffByCrashSite();
            mouths.Add(test);
            levels.Add(top[test.x, test.y]);
            ShapeMouth(test, top[test.x, test.y]);
        }
        CaveMouths = mouths.ToArray();
        CaveMouthLevels = levels.ToArray();
        CaveHoles = new CaveHole[mouths.Count];
        Land here = SaveLand();
        for (int i = 0; i < mouths.Count; i++) CaveHoles[i] = CarveHole(here, mouths[i], CaveMouthLevels[i], rand);
        caveLands = new Land[mouths.Count];
        Debug.Log("Frost caves: " + mouths.Count + " mouths picked from " + spots.Count + " ledges nobody can walk to, in " + (timer.ElapsedMilliseconds / 1000f).ToString("F1") + " seconds");
    }

    // For testing: a little cliff of rock a few squares east of the crash site, with a ledge 2 blocks up in front of
    // a cliff 5 blocks tall, and the cave mouth on the ledge. With blocks to build a step it is a quick way into a
    // cave. Gives the mouth.
    Vector2Int MakeCliffByCrashSite()
    {
        var mouth = new Vector2Int(CrashSite.x + 7, CrashSite.y);
        for (int x = mouth.x - 1; x <= mouth.x + 2; x++)
        {
            for (int y = mouth.y; y <= mouth.y + 3; y++)
            {
                int tall = y == mouth.y ? 2 : 6;
                kind[x, y] = Rock;
                mountain[x, y] = (byte)tall;
                baseTop[x, y] = (byte)tall;
                top[x, y] = (sbyte)tall;
            }
        }
        return mouth;
    }

    // Builds the crag for the cave that is always due north of the crash site: a natural-looking outcrop of rock
    // standing just outside the North Pole's ice. It is roughly round with a ragged edge, and rises in tiers: a low
    // skirt of rock round the outside, a shoulder above that, and uneven peaks in the middle tall enough to carry
    // snow. The cave mouth is set into its south face, on a ledge above the skirt.
    //
    // Nothing can be walked or jumped onto it: the skirt stands at least 3 blocks above everything round the crag,
    // all the way round, and the ledge at least 3 above the skirt (and at least 12 blocks up).
    // Gives the mouth's square: the western of the two in the middle of the ledge.
    Vector2Int MakeNorthCrag()
    {
        int x0 = CrashSite.x;
        int y0 = height - 1 - (northCap[x0] + CragFromIce);
        // The middle of the crag is a little behind the mouth.
        float middleX = x0 + 1f, middleY = y0 + 1.5f;
        int reachX = Mathf.CeilToInt(CragHalfWidth) + 1, reachY = Mathf.CeilToInt(CragHalfDepth) + 1;

        int around = 0;
        for (int y = y0 - reachY - 2; y <= y0 + reachY + 4; y++)
        {
            for (int x = x0 - reachX - 2; x <= x0 + reachX + 3; x++)
            {
                Vector2Int spot = Wrap(x, y);
                around = Mathf.Max(around, top[spot.x, spot.y]);
            }
        }
        int skirt = around + 3;
        int level = Mathf.Max(CaveMouthFrom, skirt + 3);

        for (int y = y0 - reachY - 1; y <= y0 + reachY + 3; y++)
        {
            for (int x = x0 - reachX - 1; x <= x0 + reachX + 2; x++)
            {
                // How far out from the middle this square is: 0 at the middle, 1 at the edge. A little is added or
                // taken away square by square, which makes the edge and the tiers ragged.
                float dx = (x + 0.5f - middleX) / CragHalfWidth, dy = (y + 0.5f - middleY) / CragHalfDepth;
                float away = Mathf.Sqrt(dx * dx + dy * dy) + (Hash(x, y, seed + 23) - 0.5f) * 0.2f;
                if (away > 1f) continue;

                int tall;
                // The peaks: uneven, and tall enough for snow.
                if (away < 0.5f) tall = level + 5 + Mathf.RoundToInt(LoopingNoise(x, y, 4f, seed + 29, 1) * 4f);
                // The shoulder, as high as the ledge or a block or two more.
                else if (away < 0.78f) tall = level + Mathf.RoundToInt(Hash(x, y, seed + 31) * 2f);
                else tall = skirt + (Hash(x, y, seed + 37) > 0.7f ? 1 : 0);

                // In front of the mouth the rock steps down to the skirt, so the mouth can be seen and nothing stands
                // over its ledge.
                bool inFront = y < y0 && x >= x0 - 2 && x <= x0 + 3;
                if (inFront) tall = Mathf.Min(tall, y == y0 - 1 ? level - 2 : skirt + 1);

                Vector2Int spot = Wrap(x, y);
                kind[spot.x, spot.y] = Rock;
                mountain[spot.x, spot.y] = (byte)tall;
                baseTop[spot.x, spot.y] = (byte)tall;
                top[spot.x, spot.y] = (sbyte)tall;
            }
        }
        // The ledge itself, and the cliff over it, are finished off with every other mouth (see ShapeMouth).
        return new Vector2Int(x0, y0);
    }

    // Makes room for a cave mouth: the ledge in front is made four squares wide at its own level, and the cliff
    // behind is built up where it is too low to hold the opening and the tunnel under a roof. The square given is
    // the western of the two middle ones.
    void ShapeMouth(Vector2Int front, int level)
    {
        void SetRock(int x, int y, int tall, bool atLeast)
        {
            Vector2Int spot = Wrap(x, y);
            if (atLeast && top[spot.x, spot.y] >= tall) return;
            kind[spot.x, spot.y] = Rock;
            mountain[spot.x, spot.y] = (byte)tall;
            baseTop[spot.x, spot.y] = (byte)tall;
            top[spot.x, spot.y] = (sbyte)tall;
        }

        for (int x = front.x - 1; x <= front.x + 2; x++)
        {
            bool middle = x == front.x || x == front.x + 1;
            SetRock(x, front.y, level, false);
            // The opening is three blocks tall in the middle and two at the sides, with a block of roof over it.
            SetRock(x, front.y + 1, level + (middle ? 4 : 3), true);
            SetRock(x, front.y + 2, level + 3, true);
            SetRock(x, front.y + 3, level + 2, true);
        }
    }

    // A cave mouth, dug out of real blocks: an uneven opening in the cliff behind a ledge, and a short dark tunnel
    // going into the rock. Walking in as far as Inside takes the player through.
    public class CaveHole
    {
        // The square on the ledge in front of the opening, and the level the ledge is at.
        public Vector2Int Front;
        public int Level;
        // The square in the tunnel that leads through.
        public Vector2Int Inside;
        // The blocks dug out whose south sides face the open air: what is seen of the opening from outside, as
        // (x, y, level).
        public List<Vector3Int> Opening = new List<Vector3Int>();
        // Every square of the tunnel.
        public List<Vector2Int> Squares = new List<Vector2Int>();
    }

    // Digs a cave mouth into the cliff north of a square on a ledge, in the shape Taivan drew: four blocks wide and
    // two tall at the bottom, with a part two blocks wide and one tall on top of that in the middle. The square given
    // is the western of the two middle ones. Behind the opening the tunnel goes on into the rock, two squares wide
    // and then one. A roof of rock is always left over it, so where the cliff is too low a block is left in place.
    static CaveHole CarveHole(Land land, Vector2Int front, int level, System.Random rand)
    {
        var hole = new CaveHole { Front = front, Level = level, Inside = new Vector2Int(front.x, front.y + 2) };
        var dug = new HashSet<Vector3Int>();

        void Dig(int x, int y, int from, int to)
        {
            x = ((x % land.Width) + land.Width) % land.Width;
            for (int l = from; l <= to; l++)
            {
                // Always a block of roof left above.
                if (l + 1 >= land.Top[x, y]) return;
                var spot = new Vector2Int(x, y);
                if (!land.Mined.TryGetValue(spot, out HashSet<int> gone)) land.Mined[spot] = gone = new HashSet<int>();
                gone.Add(l);
                dug.Add(new Vector3Int(x, y, l));
                if (!hole.Squares.Contains(spot)) hole.Squares.Add(spot);
            }
        }

        int x0 = front.x, y1 = front.y + 1;
        Dig(x0 - 1, y1, level, level + 1);
        Dig(x0, y1, level, level + 2);
        Dig(x0 + 1, y1, level, level + 2);
        Dig(x0 + 2, y1, level, level + 1);
        Dig(x0, y1 + 1, level, level + 1);
        Dig(x0 + 1, y1 + 1, level, level + 1);
        Dig(x0, y1 + 2, level, level);

        // The parts of the opening seen from outside: dug blocks with open air (not more of the tunnel) south of them.
        foreach (Vector3Int block in dug)
        {
            int southX = block.x, southY = block.y - 1;
            if (dug.Contains(new Vector3Int(southX, southY, block.z))) continue;
            if (land.Top[southX, southY] > block.z) continue;
            hole.Opening.Add(block);
        }
        return hole;
    }

    // Takes the player's land away into a cave, or (with -1) back up to the surface.
    public void UseLand(int cave)
    {
        if (cave == caveIndex) return;
        if (caveIndex < 0) surface = SaveLand();
        else caveLands[caveIndex] = SaveLand();
        if (cave >= 0 && caveLands[cave] == null) caveLands[cave] = MakeCave(cave);
        LoadLand(cave < 0 ? surface : caveLands[cave]);
        caveIndex = cave;

        // Everything drawn was the other land's, so it all goes and is drawn again.
        RedrawAll();
    }

    // Takes every tile off the screen, so the land round the player is drawn afresh.
    void RedrawAll()
    {
        foreach (Vector2Int cell in seeThrough) terrain.SetColor((Vector3Int)cell, Color.white);
        seeThrough.Clear();
        terrain.ClearAllTiles();
        underground.ClearAllTiles();
        doorFloors.ClearAllTiles();
        iceBeneath.ClearAllTiles();
        algaeGlow.ClearAllTiles();
        algaeFlash.ClearAllTiles();
        algaeLight.ClearAllTiles();
        algaeTops.Clear();
        window = new RectInt();
    }

    // Writes, for a saved game, every block that has been dug out or built: on the surface, and in each cave that
    // has been visited. (The blocks dug out when the world was made, for the cave mouths, are among them.)
    public void WriteChanges(System.IO.BinaryWriter w)
    {
        w.Write(caveLands.Length);
        for (int land = -1; land < caveLands.Length; land++)
        {
            // The land the player is in is the one whose blocks are in use right now.
            Dictionary<Vector2Int, HashSet<int>> dug = land == caveIndex ? mined : land < 0 ? surface.Mined : caveLands[land]?.Mined;
            Dictionary<Vector2Int, Dictionary<int, SideBlock>> built = land == caveIndex ? placed : land < 0 ? surface.Placed : caveLands[land]?.Placed;
            w.Write(dug != null);
            if (dug == null) continue;
            w.Write(dug.Count);
            foreach (KeyValuePair<Vector2Int, HashSet<int>> spot in dug)
            {
                w.Write(spot.Key.x);
                w.Write(spot.Key.y);
                w.Write(spot.Value.Count);
                foreach (int level in spot.Value) w.Write((short)level);
            }
            w.Write(built.Count);
            foreach (KeyValuePair<Vector2Int, Dictionary<int, SideBlock>> spot in built)
            {
                w.Write(spot.Key.x);
                w.Write(spot.Key.y);
                w.Write(spot.Value.Count);
                foreach (KeyValuePair<int, SideBlock> block in spot.Value)
                {
                    w.Write((short)block.Key);
                    w.Write((byte)block.Value);
                }
            }
        }
    }

    // Reads them back into a world newly made from the same seed, with the player on the surface.
    public void ReadChanges(System.IO.BinaryReader r)
    {
        int caves = r.ReadInt32();
        for (int land = -1; land < caves; land++)
        {
            if (!r.ReadBoolean()) continue;
            var dug = new Dictionary<Vector2Int, HashSet<int>>();
            int spots = r.ReadInt32();
            for (int i = 0; i < spots; i++)
            {
                var spot = new Vector2Int(r.ReadInt32(), r.ReadInt32());
                var levels = new HashSet<int>();
                int count = r.ReadInt32();
                for (int j = 0; j < count; j++) levels.Add(r.ReadInt16());
                dug[spot] = levels;
            }
            var built = new Dictionary<Vector2Int, Dictionary<int, SideBlock>>();
            spots = r.ReadInt32();
            for (int i = 0; i < spots; i++)
            {
                var spot = new Vector2Int(r.ReadInt32(), r.ReadInt32());
                var blocks = new Dictionary<int, SideBlock>();
                int count = r.ReadInt32();
                for (int j = 0; j < count; j++)
                {
                    int level = r.ReadInt16();
                    blocks[level] = (SideBlock)r.ReadByte();
                }
                built[spot] = blocks;
            }
            if (land >= caveLands.Length) continue;

            // A cave's blocks are put back with that cave as the land in use, which also makes it if need be.
            if (land >= 0) UseLand(land);
            mined = dug;
            placed = built;
            // The top of every changed spot is worked out again: the highest block still there.
            var changed = new HashSet<Vector2Int>(dug.Keys);
            changed.UnionWith(built.Keys);
            foreach (Vector2Int spot in changed)
            {
                int highest = baseTop[spot.x, spot.y];
                if (built.TryGetValue(spot, out Dictionary<int, SideBlock> put))
                {
                    foreach (int level in put.Keys) highest = Mathf.Max(highest, level + 1);
                }
                while (highest > -GroundDepth && SideBlockAt(spot.x, spot.y, highest - 1) == SideBlock.Air) highest--;
                top[spot.x, spot.y] = (sbyte)highest;
            }
            if (land >= 0) UseLand(-1);
        }
        RedrawAll();
    }

    Land SaveLand()
    {
        return new Land
        {
            Width = width, Height = height, Kind = kind, Ground = ground, BaseTop = baseTop, Top = top,
            IceDepth = iceDepth, SnowDepth = snowDepth, Mountain = mountain, Algae = algae, Mined = mined, Placed = placed,
        };
    }

    void LoadLand(Land land)
    {
        width = land.Width;
        height = land.Height;
        kind = land.Kind;
        ground = land.Ground;
        baseTop = land.BaseTop;
        top = land.Top;
        iceDepth = land.IceDepth;
        snowDepth = land.SnowDepth;
        algae = land.Algae;
        mountain = land.Mountain;
        mined = land.Mined;
        placed = land.Placed;
    }

    // Makes one cave: solid rock walls with stone tunnels and rooms dug through them, all on one level, open to the
    // sky with no roof drawn. The way in is a hole in the north wall of the square the player arrives on.
    Land MakeCave(int index)
    {
        int size = CaveSize;
        var land = new Land
        {
            Width = size, Height = size,
            Kind = new byte[size, size], Ground = new byte[size, size], BaseTop = new byte[size, size], Top = new sbyte[size, size],
            IceDepth = new byte[size, size], SnowDepth = new byte[size, size], Mountain = new byte[size, size],
            Mined = new Dictionary<Vector2Int, HashSet<int>>(), Placed = new Dictionary<Vector2Int, Dictionary<int, SideBlock>>(),
        };
        // To begin with it is all wall.
        var open = new bool[size, size];
        var rand = new System.Random(seed * 31 + index * 977 + 5);
        Vector2Int arrival = CaveArrival;

        // The tunnels: each wanders a square at a time, turning a little each step, sometimes opening into a room
        // and sometimes splitting off a branch. The first heads south, away from the way in.
        var tunnels = new List<(Vector2 at, float heading)> { (arrival, -Mathf.PI / 2f) };
        int steps = 0;
        for (int t = 0; t < tunnels.Count && steps < CaveTunnelSteps; t++)
        {
            Vector2 at = tunnels[t].at;
            float heading = tunnels[t].heading;
            int length = 80 + rand.Next(180);
            float radius = 0.9f + (float)rand.NextDouble() * 0.7f;
            for (int i = 0; i < length && steps < CaveTunnelSteps; i++, steps++)
            {
                heading += ((float)rand.NextDouble() - 0.5f) * 0.7f;
                // Turned back toward the middle when it wanders near an edge.
                Vector2 toMiddle = new Vector2(size / 2f, size / 2f) - at;
                if (at.x < CaveMargin || at.x > size - CaveMargin || at.y < CaveMargin || at.y > size - CaveMargin)
                    heading = Mathf.Atan2(toMiddle.y, toMiddle.x) + ((float)rand.NextDouble() - 0.5f);
                at += new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
                Dig(open, at, radius, radius);
                if (rand.NextDouble() < 0.012) Dig(open, at, 3.5f + (float)rand.NextDouble() * 3.5f, 3f + (float)rand.NextDouble() * 3f);
                if (rand.NextDouble() < 0.02 && tunnels.Count < MostCaveTunnels)
                    tunnels.Add((at, heading + (rand.NextDouble() < 0.5 ? 1f : -1f) * (Mathf.PI / 2f + ((float)rand.NextDouble() - 0.5f))));
            }
        }
        // A little room where the player arrives, with solid wall behind it to the north for the way out.
        Dig(open, arrival, 1.6f, 1.6f);
        for (int x = arrival.x - 2; x <= arrival.x + 3; x++)
        {
            for (int y = arrival.y + 1; y <= arrival.y + 4; y++) open[x, y] = false;
        }
        open[arrival.x, arrival.y] = true;
        open[arrival.x + 1, arrival.y] = true;

        // Any bit of tunnel that filling in the wall cut off from the way in is filled in too.
        var joined = new bool[size, size];
        var queue = new Queue<Vector2Int>();
        joined[arrival.x, arrival.y] = true;
        queue.Enqueue(arrival);
        while (queue.Count > 0)
        {
            Vector2Int p = queue.Dequeue();
            foreach (Vector2Int side in Sides)
            {
                Vector2Int n = p + side;
                if (n.x < 0 || n.y < 0 || n.x >= size || n.y >= size || joined[n.x, n.y] || !open[n.x, n.y]) continue;
                joined[n.x, n.y] = true;
                queue.Enqueue(n);
            }
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (joined[x, y])
                {
                    // Floor: bare stone, all the way down.
                    land.Kind[x, y] = Stone;
                    continue;
                }
                // Wall: rock up to the wall's height, stone underneath.
                land.Kind[x, y] = Rock;
                land.Mountain[x, y] = CaveWallHeight;
                land.BaseTop[x, y] = CaveWallHeight;
                land.Top[x, y] = CaveWallHeight;
            }
        }
        // The way out: dug into the wall like the mouths outside.
        land.Exit = CarveHole(land, arrival, 0, rand);
        return land;
    }

    // Opens up an oval of a cave around a point.
    static void Dig(bool[,] open, Vector2 at, float halfWidth, float halfHeight)
    {
        int size = open.GetLength(0);
        for (int y = Mathf.FloorToInt(at.y - halfHeight); y <= Mathf.CeilToInt(at.y + halfHeight); y++)
        {
            for (int x = Mathf.FloorToInt(at.x - halfWidth); x <= Mathf.CeilToInt(at.x + halfWidth); x++)
            {
                if (x < 1 || y < 1 || x >= size - 1 || y >= size - 1) continue;
                float dx = (x + 0.5f - at.x) / halfWidth, dy = (y + 0.5f - at.y) / halfHeight;
                if (dx * dx + dy * dy <= 1f) open[x, y] = true;
            }
        }
    }

    // Brings a square back into the planet, the way the Earth works. Going off the east edge comes back on the west.
    // Going off the top of the map, over the North Pole, comes down on the far side of the planet, half way round:
    // the rows past the top are the rows just below it, counting back down, half the planet's width along. The South
    // Pole works the same way. So past a pole, the land is the far side of the planet, seen upside down.
    public Vector2Int Wrap(int x, int y)
    {
        int sheet = Mathf.FloorToInt(y / (float)height);
        y -= sheet * height;
        if ((sheet & 1) != 0)
        {
            y = height - 1 - y;
            x += width / 2;
        }
        x %= width;
        return new Vector2Int(x < 0 ? x + width : x, y);
    }

    public Vector2Int Wrap(Vector2Int cell)
    {
        return Wrap(cell.x, cell.y);
    }

    // The same for a point: where it is on the planet, from 0 up to the planet's width and height.
    public Vector2 WrapPoint(Vector2 point)
    {
        float x = point.x, y = Mathf.Repeat(point.y, height * 2f);
        if (y >= height)
        {
            y = Mathf.Min(height * 2f - y, height - 0.001f);
            x += width / 2f;
        }
        return new Vector2(Mathf.Repeat(x, width), y);
    }

    // True if a point is past the top or bottom of the map, over a pole. Then across is the same place on the
    // planet, on the near side of the pole, half way round: someone walking north over the pole comes out there,
    // walking south.
    public bool OverPole(Vector2 point, out Vector2 across)
    {
        across = point;
        if (point.y >= 0f && point.y < height) return false;
        // The position across the pole is brought back onto the map from east to west. Positions that only grow
        // lose their exactness, and crossing back and forth many times made everything shake.
        across = new Vector2(Mathf.Repeat(point.x + width / 2f, width), point.y < 0f ? -point.y : height * 2f - point.y);
        return true;
    }

    // Because the planet joins up with itself, every spot can be found again round the planet to the east and west,
    // and upside down past each pole. This gives the copy of a spot that is closest to another position.
    public Vector2 Nearest(Vector2 spot, Vector2 from)
    {
        float period = height * 2f;
        var straight = new Vector2(NearestRepeat(spot.x, from.x, width), NearestRepeat(spot.y, from.y, period));
        var overPole = new Vector2(NearestRepeat(spot.x + width / 2f, from.x, width), NearestRepeat(-spot.y, from.y, period));
        return (straight - from).sqrMagnitude <= (overPole - from).sqrMagnitude ? straight : overPole;
    }

    // The copy of a value, repeating every period, that is closest to another value.
    static float NearestRepeat(float value, float from, float period)
    {
        return from + Mathf.Repeat(value - from + period / 2f, period) - period / 2f;
    }

    // Everything that belongs to the top-down view hangs under this.
    public Transform TopDownRoot => topDownRoot.transform;

    // The colour a spot is shown in on the maps: white snow, blue ice, grey rock and ship, each paler where raised.
    public Color32 MapColorAt(int x, int y) => DesertIf(PlanetMapColorAt(x, y));

    Color32 PlanetMapColorAt(int x, int y)
    {
        Vector2Int spot = Wrap(x, y);
        x = spot.x;
        y = spot.y;
        switch (SurfaceMaterial(x, y))
        {
            case Stone: return StoneColor;
            case Water: return WaterColor;
            case Algae:
            {
                float across = Mathf.Abs(x - algaeX), up = y - height / 2;
                across = Mathf.Min(across, width - across);
                float reach = AlgaeRadius * (0.7f + 0.6f * LoopingNoise(x, y, AlgaeEdgeBlobSize, algaeSeed, 2));
                float patch = LoopingNoise(x, y, AlgaeMapPatchSize, algaeSeed + 11, 3);
                return AlgaeMapColor(Mathf.InverseLerp(0.25f, 0.75f, patch), algaePictures != null && RowIsLined(y), Mathf.Sqrt(across * across + up * up) / reach);
            }
            case Ice: return IceBase[ground[x, y]];
            case Rock: return RockColors[0];
            case Ship: return ShipColor;
            default: return ground[x, y] > 0 ? new Color32(255, 255, 255, 255) : new Color32(222, 230, 238, 255);
        }
    }

    public bool IsSnow(int x, int y)
    {
        Vector2Int spot = Wrap(x, y);
        return kind[spot.x, spot.y] == Snow;
    }

    // True where the land was made as ice, whatever has been dug or built there since.
    public bool IsIceAt(int x, int y)
    {
        Vector2Int spot = Wrap(x, y);
        return kind[spot.x, spot.y] == Ice;
    }

    // True if a block-sized space of the world is water.
    public bool WaterAt(int x, int y, int level)
    {
        return SideBlockAt(x, y, level) == SideBlock.Water;
    }

    // True where the top of the land is the ocean.
    // True where the top of the land is algae.
    public bool IsAlgae(int x, int y)
    {
        return SurfaceMaterial(x, y) == Algae;
    }

    public bool IsWater(Vector2 position)
    {
        return SurfaceMaterial(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y)) == Water;
    }

    public bool IsIce(Vector2 position)
    {
        return SurfaceMaterial(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y)) == Ice;
    }

    // Height in blocks of whatever is at a spot.
    public int TopAt(int x, int y)
    {
        Vector2Int spot = Wrap(x, y);
        return top[spot.x, spot.y];
    }

    // Height of the snow or ice surface at a spot, ignoring any rock or ship standing on it.
    public int GroundAt(int x, int y)
    {
        Vector2Int spot = Wrap(x, y);
        return ground[spot.x, spot.y];
    }

    // The block at one spot of a row seen from the side. Level 0 is the first block above the lowest ground,
    // and negative levels are underground.
    public SideBlock SideBlockAt(int x, int row, int level)
    {
        Vector2Int spot = Wrap(x, row);
        x = spot.x;
        row = spot.y;
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
        if (k == Water) return depth <= OceanDepth ? SideBlock.Water : SideBlock.Stone;
        // In the algae biome the top block of the ground is algae, with the usual snow, ice and stone under it.
        if (depth == 1 && algae != null && algae[x, row] != 0) return SideBlock.Algae;
        if (k == SnowOverIce) return depth == 1 ? SideBlock.Snow : depth <= 1 + CapIceDepth ? SideBlock.Ice : SideBlock.Stone;
        if (k == Ice && depth <= iceDepth[x, row]) return SideBlock.Ice;
        return depth <= snowDepth[x, row] ? SideBlock.Snow : SideBlock.Stone;
    }

    // Set by whatever stands on the ground and gets in the way, such as ferns and ice shards: true for a spot that
    // has one. It is given the spot's position within the planet.
    public System.Func<int, int, bool> Obstacle;

    // True if something stands on the ground at a spot and blocks the way.
    public bool ObstacleAt(int x, int y)
    {
        if (Obstacle == null) return false;
        Vector2Int spot = Wrap(x, y);
        return Obstacle(spot.x, spot.y);
    }

    // True if the player cannot be in a space: there is a block there, or something standing on the ground there.
    // A thing on the ground fills the one block of space above the ground it stands on.
    public bool SolidAt(int x, int y, int level)
    {
        SideBlock block = SideBlockAt(x, y, level);
        // Water is not solid: the player sinks through it.
        return (block != SideBlock.Air && block != SideBlock.Water) || (level == GroundAt(x, y) && ObstacleAt(x, y));
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
            case SideBlock.Water: return Water;
            case SideBlock.Algae: return Algae;
            default: return Rock;
        }
    }

    // Mines the block at one level of a spot. Returns what the block was, or Air if there was nothing there to mine:
    // empty space, part of the ship, or the unbreakable bottom layer of the ground.
    public SideBlock Mine(int x, int row, int level)
    {
        SideBlock was = SideBlockAt(x, row, level);
        if (was == SideBlock.Air || was == SideBlock.Ship || was == SideBlock.Water || level <= -GroundDepth) return SideBlock.Air;

        Vector2Int spot = Wrap(x, row);
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

        Vector2Int spot = Wrap(x, row);
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
            // The nearest copy of the spot, which can be across a pole.
            Vector2 near = Nearest(new Vector2(x + 0.5f, y + 0.5f), cell + new Vector2(0.5f, 0.5f));
            if (Mathf.Abs(near.x - cell.x - 0.5f) <= reach + 0.01f && Mathf.Abs(near.y - cell.y - 0.5f) <= reach + 0.01f) SetLandTile(cell);
        }
    }

    // Hides or shows everything that belongs to the top-down view (the player and camera are shared).
    public void SetTopDownVisible(bool visible)
    {
        topDownRoot.SetActive(visible);
    }

    // Pixel position inside one tile, with (0, 0) at the top left. Rims are the lighter edges of a rock top.
    // The rocky planet's colours: Planet 1's colours, from dark to light, turned to red desert rock, from a deep
    // red-brown through rusty red to pale orange sand. (Snow becomes sand, rock and stone become red rock.)
    static readonly Color32[] DesertSteps =
    {
        new Color32(0x3A, 0x14, 0x10, 255), new Color32(0x6A, 0x24, 0x18, 255), new Color32(0x8E, 0x34, 0x20, 255),
        new Color32(0xB0, 0x4A, 0x2C, 255), new Color32(0xC8, 0x62, 0x36, 255), new Color32(0xD8, 0x80, 0x4C, 255),
    };

    public static Color32 Desert(Color32 c)
    {
        float light = (0.3f * c.r + 0.59f * c.g + 0.11f * c.b) / 255f;
        float along = Mathf.Clamp01(light) * (DesertSteps.Length - 1);
        int low = Mathf.Min(DesertSteps.Length - 2, (int)along);
        Color32 desert = Color32.Lerp(DesertSteps[low], DesertSteps[low + 1], along - low);
        desert.a = c.a;
        return desert;
    }

    // A colour as it is on the planet being played.
    public static Color32 DesertIf(Color32 c) => Rocky ? Desert(c) : c;

    // On the rocky planet, his snow pictures (seen from the side, and in the inventory) are turned to desert sand.
    // From above they are turned as each tile is painted.
    void MakeDesertPictures()
    {
        foreach (BlockPicture picture in new[] { snowPicture, snowPicture2, snowPicture3 })
        {
            if (picture == null) continue;
            var sand = new Color32[picture.Pixels.Length];
            for (int i = 0; i < sand.Length; i++) sand[i] = Desert(picture.Pixels[i]);
            var texture = new Texture2D(picture.Width, picture.Height, TextureFormat.RGBA32, false) { filterMode = picture.Sprite.texture.filterMode, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(sand);
            texture.Apply();
            picture.Sprite = Sprite.Create(texture, new Rect(0, 0, picture.Width, picture.Height), new Vector2(0.5f, 0.5f), picture.Width);
        }
    }

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

    // A repeatable random value for a square of the planet, the same for every copy of it.
    float HashAt(int x, int y, int hashSeed)
    {
        Vector2Int spot = Wrap(x, y);
        return Hash(spot.x, spot.y, hashSeed);
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
