using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Small things on the ground the player can collect: plants on the snow and sharp ice shards on the ice.
// They show in both views. Left clicking one collects it if the player is at most a block away;
// otherwise the player walks to it in a straight line first, jumping where needed. If a rock or the ship is in the way, nothing happens.
public class Gatherables : MonoBehaviour
{
    // The fern's picture is a little taller than the player, which makes the tall frond itself about the player's height.
    const float PlantHeight = PlayerController.Height * 1.1f;
    // How much of the snow has a plant on it, and how much of the ice has a shard.
    const double PlantDensity = 0.004;
    const double ShardDensity = 0.009;
    // Across the whole planet there are this many ice shards for every this many ferns.
    const int FernsPerSet = 8, ShardsPerSet = 5;
    // There is one thornam bush for every this many ferns. Bushes only grow on snow that is right next to ice.
    const int FernsPerBush = 13;
    // A thornam bush stands as tall as the player. These say where its trunk meets the ground in Taivan's picture,
    // as shares of the picture's width (from the left) and height (from the bottom), and how much of the
    // picture's height the bush itself takes up.
    const float BushHeight = PlayerController.Height;
    const float BushFootAcross = 0.492f, BushFootUp = 0.1875f, BushDrawnShare = 0.75f;
    // Loose thorns, vines and sticks are painted at the fine scale, this many pixels a side, and are this big in blocks.
    const int LoosePixels = 128;
    const float LooseSize = 0.8f;
    // How far across the drawn fern picture the foot of its stem is, from the left edge, as a share of its width.
    const float FernFootAcross = 0.472f;
    // How far up the picture the ground line is, from the bottom edge, as a share of its height.
    // The snow mound and shadow at the fern's foot sit around and just below this line.
    const float FernFootUp = 0.088f;
    // How big the picture of a loose ice shard is, in blocks, before the dropped item is shrunk a little.
    const float LooseShardSize = 0.9f;
    // How far up the loose ice shards picture its lowest shard reaches, as a share of the picture's height. It
    // sits on the ground there.
    const float LooseShardFootUp = 16f / 128f;
    // Where the middle of the icicle's base is in Taivan's picture: across from the left edge as a share of the width,
    // and up from the bottom in pixels. How many rows at the top of the picture are left out, and how wide and
    // tall the icicle itself is, in pixels.
    const float IcicleFootAcross = 44.5f / 128f, IcicleFootUp = 7f;
    const int IcicleTopRowsLeftOut = 1;
    // The icicle is drawn this many times as tall as in the picture, the same width.
    const float IcicleStretch = 1.6f;
    // The three blues of the icicle picture (the same as the ice's), and what the icicle is coloured instead: frosty
    // white at the foot, a clear pale blue up the middle, nearly white at the tip, and a deeper blue for the shaded side.
    static readonly Color32 IcicleBody = new Color32(146, 202, 232, 255), IcicleGlint = new Color32(206, 235, 248, 255), IcicleShade = new Color32(112, 174, 212, 255);
    static readonly Color IcicleFrost = new Color32(232, 245, 252, 255), IcicleClear = new Color32(178, 226, 247, 255), IcicleTip = new Color32(236, 249, 255, 255);
    static readonly Color IcicleDeep = new Color32(96, 158, 206, 255);
    // The soft shadow drawn beside a thing that would otherwise blend into the ground: where it sits, and its colour.
    static readonly Vector3 ShadowOffset = new Vector3(0.09f, -0.03f, 0f);
    static readonly Color ShadowColor = new Color(0.25f, 0.42f, 0.6f, 0.6f);
    const float IcicleWidePixels = 44f, IcicleTallPixels = 100f;
    // Where in its square a thing stands, measured up from the near edge.
    const float BaseOffset = 0.3f;
    // "A block away at max": the thing is in the player's square or one next to it.
    const float Reach = 1.6f;
    // When planning a walk, squares count as close enough only if they are comfortably inside the reach.
    const float PlannedReach = 1.3f;
    // How far outside the picture a click still counts.
    const float ClickPadding = 0.12f;
    // A walk is given up if the player stops making progress for this long.
    const float StuckSeconds = 1.5f;

    static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    static readonly Color32 FernDark = new Color32(22, 104, 112, 255), FernLight = new Color32(72, 188, 170, 255);
    // The colours of the thornam bush in Taivan's picture, used for the things it drops: its teal vines, its olive
    // thorns and its brown trunk.
    static readonly Color32 BushLight = new Color32(48, 192, 168, 255), BushDark = new Color32(48, 124, 122, 255);
    static readonly Color32 ThornLight = new Color32(158, 180, 66, 255), ThornDark = new Color32(112, 134, 44, 255);
    static readonly Color32 StickLight = new Color32(134, 102, 64, 255), BushTrunk = new Color32(96, 72, 48, 255);
    // The plant, one text row per pixel row from the top: a bluish green fiddlehead fern, a stalk whose tip is
    // still curled into a spiral. D is the darker outer side, L the lighter inner side.
    static readonly string[] FernPicture =
    {
        "..DDDD....",
        ".DLLLLD...",
        "DL....LD..",
        "DL.DD..LD.",
        "DL.DLD.LD.",
        "DL..LD.LD.",
        ".DL...LLD.",
        "..DDLLLD..",
        "....DLLD..",
        "....DLD...",
        "....DLD...",
        "....DLD...",
        "...DLLD...",
        "...DLD....",
        "...DLD....",
        "...DLD....",
    };
    // The same blues as the ice it grows from, so a shard reads as part of the ice: a lit side a little paler than
    // the ice, a middle that matches it, and a far side a little deeper.
    // These follow the ice it grows from: the ice's glint colour, a blue a little paler than the ice so the shard
    // shows against it, and a deeper blue.
    static readonly Color32 ShardLight = new Color32(214, 239, 250, 255), ShardShade = new Color32(174, 219, 242, 255), ShardDark = new Color32(104, 166, 208, 255);
    // The standing ice shard is painted at the same fine scale as the snow picture: 128 pixels to a block.
    const int ShardPixelsWide = 40;
    const int ShardPixelsTall = 96;
    const float ShardPixelsPerUnit = 128f;

    // One pixel of the standing ice shard. It is long and gently curved, thick at the base and tapering to a point,
    // like the pick of a pickaxe, and it spreads out a little where it joins the ice. y counts down from the top.
    static Color32 ShardPixel(int x, int y)
    {
        // How far up the shard this row is: 0 at the base, 1 at the tip.
        float up = 1f - (y + 0.5f) / ShardPixelsTall;
        // The middle of the shard leans to the left as it rises, and it narrows all the way up.
        float middle = 25f - 15f * Mathf.Pow(up, 1.6f);
        float halfWidth = Mathf.Lerp(12f, 1.2f, Mathf.Pow(up, 0.8f)) + 4f * Mathf.Clamp01(1f - up / 0.07f);
        float across = (x + 0.5f - middle) / halfWidth;
        if (across < -1f || across > 1f) return Clear;
        // Lit down its left side, deeper blue down its right, and the ice's own blue between.
        return across < -0.3f ? ShardLight : across > 0.45f ? ShardDark : ShardShade;
    }

    class Kind
    {
        public Item Item;
        // One sprite per ground height: on raised ground the picture is drawn higher above the same spot.
        public Sprite[] Sprites;
        public float Width, Height;
        // What comes loose when it is collected, and how many of each. Left empty, it is one of the item above.
        public Item[] Drops;
        public int[] DropCounts;
        // True for things that can only be collected with the ice axe equipped.
        public bool NeedsAxe;
        // True for things drawn with a soft shadow beside them, so they show against ground of the same colour.
        public bool Shadow;
    }


    public Item PlantItem { get; private set; }
    public Item ShardItem { get; private set; }
    public Item ThornItem { get; private set; }
    public Item VineItem { get; private set; }
    public Item StickItem { get; private set; }
    // Set from outside: whether the player has the ice axe in hand, and a way to show them a line of text.
    public System.Func<bool> AxeEquipped;
    public System.Action<string> Say;
    // True while the player is walking toward something they clicked.
    public bool Walking => target.HasValue;

    // Drops any walk in progress, for when the player has clicked on something else.
    public void CancelWalk()
    {
        target = null;
    }

    PlanetOne planet;
    PlayerController player;
    MiningMode mining;
    DayNight dayNight;
    Pickups pickups;
    Camera cam;
    // What stands on each square of the planet that has something, by the square's position within the planet.
    readonly Dictionary<Vector2Int, Kind> things = new Dictionary<Vector2Int, Kind>();
    // The top-down pictures of the things near the player, by the position they are drawn at. Because the planet
    // repeats, that position can be many planet-widths away from the square's position within the planet.
    readonly Dictionary<Vector2Int, GameObject> shown = new Dictionary<Vector2Int, GameObject>();
    readonly List<Vector2Int> outOfRange = new List<Vector2Int>();
    RectInt shownWindow;
    readonly List<SpriteRenderer> sideView = new List<SpriteRenderer>();

    // The thing being walked to, and the view and row or column the walk started in.
    Vector2Int? target;
    bool walkInSideView;
    bool walkColumn;
    int walkSlice;
    Vector2 lastSpot;
    float lastMovedAt;

    public void Setup(PlanetOne planet, PlayerController player, MiningMode mining, DayNight dayNight, Pickups pickups, Camera cam)
    {
        this.planet = planet;
        this.player = player;
        this.mining = mining;
        this.dayNight = dayNight;
        this.pickups = pickups;
        this.cam = cam;

        // The fern's picture is scaled so it stands exactly PlantHeight tall.
        Kind plant = MakeKind("Fiddlehead fern", FernPicture[0].Length, FernPicture.Length, FernPicture.Length / PlantHeight, false, (x, y) =>
        {
            char c = FernPicture[y][x];
            return c == 'D' ? FernDark : c == 'L' ? FernLight : Clear;
        });
        // The drawn fern picture replaces the pixel one above, standing in the world as well as loose and in the inventory.
        var fernPicture = Resources.Load<Texture2D>("Art/Fern");
        if (fernPicture != null)
        {
            float pixelsPerUnit = fernPicture.height / PlantHeight;
            var whole = new Rect(0, 0, fernPicture.width, fernPicture.height);
            for (int level = 0; level < plant.Sprites.Length; level++)
            {
                // The pivot is the foot of the stem. Dropping it below the picture draws the fern higher, for raised ground.
                var pivot = new Vector2(FernFootAcross, FernFootUp - level * PlanetOne.HeightScale / PlantHeight);
                plant.Sprites[level] = Sprite.Create(fernPicture, whole, pivot, pixelsPerUnit);
            }
            plant.Item = new Item(plant.Item.Name, plant.Sprites[0]);
            plant.Width = fernPicture.width / pixelsPerUnit;
        }
        Kind shard = MakeKind("Ice shard", ShardPixelsWide, ShardPixelsTall, ShardPixelsPerUnit, true, ShardPixel);
        // Standing on the ice, a shard looks like Taivan's icicle picture at the fine scale. He asked for it taller and
        // looking different from the ice, so it is stretched upward and recoloured paler, frostier blues.
        Texture2D icicle = MakeIcicle();
        if (icicle != null)
        {
            var whole = new Rect(0, 0, icicle.width, icicle.height);
            float pictureHeight = icicle.height / ShardPixelsPerUnit;
            for (int level = 0; level < shard.Sprites.Length; level++)
            {
                // The pivot is the middle of its base. Dropping it draws the icicle higher, for raised ground.
                var pivot = new Vector2(IcicleFootAcross, IcicleFootUp * IcicleStretch / icicle.height - level * PlanetOne.HeightScale / pictureHeight);
                shard.Sprites[level] = Sprite.Create(icicle, whole, pivot, ShardPixelsPerUnit);
            }
            shard.Width = IcicleWidePixels / ShardPixelsPerUnit;
            shard.Height = IcicleTallPixels * IcicleStretch / ShardPixelsPerUnit;
        }
        // Once it has been knocked loose, an ice shard looks like the drawn picture, both on the ground and in the inventory.
        var shardPicture = Resources.Load<Texture2D>("Art/IceShardItem");
        if (shardPicture != null)
        {
            var whole = new Rect(0, 0, shardPicture.width, shardPicture.height);
            shard.Item = new Item(shard.Item.Name, Sprite.Create(shardPicture, whole, new Vector2(0.5f, LooseShardFootUp), shardPicture.height / LooseShardSize));
        }
        PlantItem = plant.Item;
        ShardItem = shard.Item;

        var rand = new System.Random(planet.seed + 1);
        for (int y = 0; y < planet.height; y++)
        {
            for (int x = 0; x < planet.width; x++)
            {
                double roll = rand.NextDouble();
                // Nothing grows right where the player starts, since things on the ground cannot be walked through.
                if (Mathf.Abs(x - planet.width / 2) <= 1 && Mathf.Abs(y - (planet.height / 2 - 1)) <= 1) continue;
                if (planet.IsSnow(x, y) && roll < PlantDensity) things[new Vector2Int(x, y)] = plant;
                else if (planet.IsIce(new Vector2(x + 0.5f, y + 0.5f)) && roll < ShardDensity) things[new Vector2Int(x, y)] = shard;
            }
        }
        int ferns = KeepRatio(plant, shard, rand);
        GrowBushes(MakeBush(), ferns / FernsPerBush, rand);
        mining.RowBuilt += BuildSideView;
        planet.BlockMined += OnBlockMined;
        // Ferns and ice shards are in the way: the player walks round them, or jumps onto them.
        planet.Obstacle = (x, y) => things.ContainsKey(new Vector2Int(x, y));
        ShowNearby();
    }

    // Taivan's icicle picture, made taller and recoloured to look like a real icicle instead of matching the ice it
    // stands on: frosted white where it meets the ground, clearing to a slightly see-through pale blue up its length,
    // and nearly white at the tip. His pale stripe and shaded
    // side are kept, as a bright glint and a deeper blue. The top row of the picture is left out, since it has a stray
    // mark in one corner. Returns null if there is no picture. His file itself is not changed.
    static Texture2D MakeIcicle()
    {
        var picture = Resources.Load<Texture2D>("Art/Icicle");
        if (picture == null || !picture.isReadable) return null;
        Color32[] pixels = picture.GetPixels32();
        int width = picture.width, rows = picture.height - IcicleTopRowsLeftOut;
        int height = Mathf.RoundToInt(rows * IcicleStretch);
        var stretched = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            // Each row of the taller picture is taken from the row of the original at the same share of the way up.
            int from = Mathf.Min(rows - 1, (int)(y / IcicleStretch));
            for (int x = 0; x < width; x++) stretched[y * width + x] = Recolour(pixels[from * width + x], y / (float)height);
        }
        var icicle = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = picture.filterMode, wrapMode = TextureWrapMode.Clamp };
        icicle.SetPixels32(stretched);
        icicle.Apply();
        return icicle;
    }

    // One pixel of the icicle, from a pixel of Taivan's picture. Up is how far up the icicle it is, from 0 at the
    // ground to 1 at the tip.
    static Color32 Recolour(Color32 c, float up)
    {
        if (c.a == 0) return c;
        // Frosted near the ground, clear and a little see-through up the middle, and paler again toward the tip.
        Color body = up < 0.15f
            ? Color.Lerp(IcicleFrost, IcicleClear, up / 0.15f)
            : Color.Lerp(IcicleClear, IcicleTip, Mathf.Pow((up - 0.15f) / 0.85f, 2f));
        float solid = up < 0.15f ? Mathf.Lerp(0.97f, 0.84f, up / 0.15f) : 0.84f;

        if (c.r == IcicleGlint.r && c.g == IcicleGlint.g && c.b == IcicleGlint.b) { body = Color.white; solid = 1f; }
        else if (c.r == IcicleShade.r && c.g == IcicleShade.g && c.b == IcicleShade.b) { body = Color.Lerp(body, IcicleDeep, 0.75f); solid = Mathf.Max(solid, 0.9f); }
        else if (!(c.r == IcicleBody.r && c.g == IcicleBody.g && c.b == IcicleBody.b)) return c;
        body.a = solid * c.a / 255f;
        return body;
    }


    // The thornam bush: Taivan's picture of a tangle of teal vines and thorns on a short brown trunk. Collecting one
    // gives 3 thorns, 3 thornam bush vines and 1 thornam stick.
    Kind MakeBush()
    {
        ThornItem = new Item("Thorn", LooseSprite((x, y) => ThornPixel(x, y)));
        VineItem = new Item("Thornam bush vine", LooseSprite((x, y) => VinePixel(x, y)));
        StickItem = new Item("Thornam stick", LooseSprite((x, y) => StickPixel(x, y)));

        // Without the picture, a plain round bush in the same colours stands in for it.
        Kind bush = MakeKind("Thornam bush", 16, 16, 16 / BushHeight, false, (x, y) =>
            (x - 7.5f) * (x - 7.5f) + (y - 7f) * (y - 7f) < 46f ? BushLight : y > 9 && x >= 7 && x <= 8 ? BushTrunk : Clear);
        var picture = Resources.Load<Texture2D>("Art/ThornamBush");
        if (picture != null)
        {
            // The picture is used exactly as drawn, scaled so the bush stands as tall as the player.
            float pixelsPerUnit = picture.height * BushDrawnShare / BushHeight;
            float pictureHeight = picture.height / pixelsPerUnit;
            var whole = new Rect(0, 0, picture.width, picture.height);
            for (int level = 0; level < bush.Sprites.Length; level++)
            {
                // The pivot is where the trunk meets the ground. Dropping it draws the bush higher, for raised ground.
                var pivot = new Vector2(BushFootAcross, BushFootUp - level * PlanetOne.HeightScale / pictureHeight);
                bush.Sprites[level] = Sprite.Create(picture, whole, pivot, pixelsPerUnit);
            }
            bush.Item = new Item(bush.Item.Name, bush.Sprites[0]);
            bush.Width = picture.width / pixelsPerUnit;
            bush.Height = BushHeight;
        }
        bush.Drops = new[] { ThornItem, VineItem, StickItem };
        bush.DropCounts = new[] { 3, 3, 1 };
        bush.NeedsAxe = true;
        return bush;
    }

    // Plants a number of thornam bushes, picked at random from the snow squares that are right next to ice.
    void GrowBushes(Kind bush, int count, System.Random rand)
    {
        var shore = new List<Vector2Int>();
        for (int y = 0; y < planet.height; y++)
        {
            for (int x = 0; x < planet.width; x++)
            {
                if (!planet.IsSnow(x, y) || things.ContainsKey(new Vector2Int(x, y))) continue;
                if (Mathf.Abs(x - planet.width / 2) <= 1 && Mathf.Abs(y - (planet.height / 2 - 1)) <= 1) continue;
                if (planet.IsIceAt(x - 1, y) || planet.IsIceAt(x + 1, y) || planet.IsIceAt(x, y - 1) || planet.IsIceAt(x, y + 1)) shore.Add(new Vector2Int(x, y));
            }
        }
        for (int i = 0; i < count && shore.Count > 0; i++)
        {
            int pick = rand.Next(shore.Count);
            things[shore[pick]] = bush;
            shore[pick] = shore[shore.Count - 1];
            shore.RemoveAt(shore.Count - 1);
        }
    }

    static Sprite LooseSprite(System.Func<int, int, Color32> paint)
    {
        return PlanetOne.PaintSprite(LoosePixels, LoosePixels, new Vector2(0.5f, 0.06f), LoosePixels / LooseSize, true, paint);
    }

    // A thorn: a curved olive spike, thick at the base and sharp at the tip.
    static Color32 ThornPixel(int x, int y)
    {
        var line = new Vector2[9];
        for (int i = 0; i < line.Length; i++)
        {
            float t = i / (float)(line.Length - 1);
            // A gentle curve from the base at the bottom left to the tip at the top right.
            line[i] = Vector2.Lerp(Vector2.Lerp(new Vector2(42f, 106f), new Vector2(50f, 60f), t), Vector2.Lerp(new Vector2(50f, 60f), new Vector2(90f, 24f), t), t);
        }
        return StrokePixel(x, y, line, 8f, 0.6f, ThornLight, ThornDark);
    }

    // A length of thornam vine: a wavy teal strand with three small thorns still on it.
    static Color32 VinePixel(int x, int y)
    {
        var line = new Vector2[13];
        for (int i = 0; i < line.Length; i++) line[i] = new Vector2(14f + i * 8.3f, 104f - i * 6.5f + 12f * Mathf.Sin(i * 0.9f));
        for (int i = 3; i <= 9; i += 3)
        {
            // Each thorn sticks out sideways from the vine.
            Vector2 along = (line[i + 1] - line[i - 1]).normalized;
            Vector2 sideways = new Vector2(along.y, -along.x) * (i == 6 ? -1f : 1f);
            Color32 thorn = StrokePixel(x, y, new[] { line[i], line[i] + sideways * 16f + along * 4f }, 4f, 0.6f, ThornLight, ThornDark);
            if (thorn.a > 0) return thorn;
        }
        return StrokePixel(x, y, line, 7f, 4.5f, BushLight, BushDark);
    }

    // A thornam stick: a short brown branch with a small fork.
    static Color32 StickPixel(int x, int y)
    {
        Color32 main = StrokePixel(x, y, new[] { new Vector2(22f, 108f), new Vector2(58f, 70f), new Vector2(106f, 20f) }, 7.5f, 5f, StickLight, BushTrunk);
        if (main.a > 0) return main;
        return StrokePixel(x, y, new[] { new Vector2(62f, 66f), new Vector2(84f, 62f), new Vector2(100f, 66f) }, 4.5f, 2.2f, StickLight, BushTrunk);
    }

    // One pixel of a line drawn through some points, thick at the start and thin at the end: lit down one side and
    // darker down the other. See-through if the pixel is not on the line.
    static Color32 StrokePixel(int x, int y, Vector2[] line, float startWidth, float endWidth, Color32 light, Color32 dark)
    {
        var p = new Vector2(x + 0.5f, y + 0.5f);
        float best = float.MaxValue, bestAlong = 0f, bestSide = 0f;
        for (int i = 0; i < line.Length - 1; i++)
        {
            Vector2 step = line[i + 1] - line[i];
            float t = Mathf.Clamp01(Vector2.Dot(p - line[i], step) / step.sqrMagnitude);
            float distance = Vector2.Distance(p, line[i] + step * t);
            if (distance >= best) continue;
            best = distance;
            bestAlong = (i + t) / (line.Length - 1);
            bestSide = step.x * (p.y - line[i].y) - step.y * (p.x - line[i].x);
        }
        if (best > Mathf.Lerp(startWidth, endWidth, bestAlong)) return Clear;
        return bestSide < 0f ? light : dark;
    }

    // Makes the planet hold 5 ice shards for every 8 ferns, the amounts the pickaxe is made from. How many of each
    // a world starts with depends on how much snow and ice it has, so whichever there is too much of is thinned out,
    // picked at random from all over the planet.
    int KeepRatio(Kind plant, Kind shard, System.Random rand)
    {
        var plants = new List<Vector2Int>();
        var shards = new List<Vector2Int>();
        foreach (KeyValuePair<Vector2Int, Kind> thing in things) (thing.Value == plant ? plants : shards).Add(thing.Key);

        // The most whole sets of 8 ferns and 5 shards that both can make up.
        int sets = Mathf.Min(plants.Count / FernsPerSet, shards.Count / ShardsPerSet);
        Thin(plants, sets * FernsPerSet, rand);
        Thin(shards, sets * ShardsPerSet, rand);
        return plants.Count;
    }

    // Takes things away at random until only so many are left.
    void Thin(List<Vector2Int> cells, int keep, System.Random rand)
    {
        while (cells.Count > keep)
        {
            int pick = rand.Next(cells.Count);
            things.Remove(cells[pick]);
            cells[pick] = cells[cells.Count - 1];
            cells.RemoveAt(cells.Count - 1);
        }
    }

    // When the block a thing is standing on is mined out from under it, the thing comes loose as if it had been collected.
    void OnBlockMined(int x, int row, int level)
    {
        var cell = new Vector2Int(x, row);
        if (level == planet.GroundAt(x, row) - 1 && things.ContainsKey(planet.Wrap(cell))) Collect(cell);
    }

    // Keeps a top-down picture for every thing on the land that is currently drawn, and none for the rest.
    void ShowNearby()
    {
        RectInt window = planet.LandWindow;
        if (window.Equals(shownWindow)) return;
        shownWindow = window;

        outOfRange.Clear();
        foreach (Vector2Int cell in shown.Keys)
        {
            if (!window.Contains(cell)) outOfRange.Add(cell);
        }
        foreach (Vector2Int cell in outOfRange)
        {
            Destroy(shown[cell]);
            shown.Remove(cell);
        }
        foreach (Vector2Int cell in window.allPositionsWithin)
        {
            if (!shown.ContainsKey(cell) && things.TryGetValue(planet.Wrap(cell), out Kind kind)) Show(kind, cell);
        }
    }

    // Makes a kind of thing from a small picture. The pivot is at its foot. For raised ground, empty space is added
    // underneath so it is drawn that much higher while still being sorted by where it actually stands.
    // Fine is for pictures painted at the snow picture's scale, which are shown smaller than they are painted.
    static Kind MakeKind(string name, int pixelsWide, int pixelsTall, float pixelsPerUnit, bool fine, System.Func<int, int, Color32> paint)
    {
        var sprites = new Sprite[2];
        for (int level = 0; level < sprites.Length; level++)
        {
            int lift = Mathf.RoundToInt(level * PlanetOne.HeightScale * pixelsPerUnit);
            sprites[level] = PlanetOne.PaintSprite(pixelsWide, pixelsTall + lift, new Vector2(0.5f, 0f), pixelsPerUnit, fine,
                (x, y) => y < pixelsTall ? paint(x, y) : Clear);
        }
        return new Kind
        {
            Item = new Item(name, sprites[0]),
            Sprites = sprites,
            Width = pixelsWide / pixelsPerUnit,
            Height = pixelsTall / pixelsPerUnit,
        };
    }

    void Show(Kind kind, Vector2Int cell)
    {
        var go = new GameObject(kind.Item.Name);
        go.transform.SetParent(planet.TopDownRoot, false);
        go.transform.position = Foot(cell);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = kind.Sprites[planet.GroundAt(cell.x, cell.y)];
        // Sorted by its foot, so the player walks in front of or behind it correctly.
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        if (kind.Shadow)
        {
            // The shadow is the same picture in a darker blue, a little to the side, drawn just behind the thing.
            // The two are put in order as one, at the thing's foot, so the shadow is never in front of anything else.
            go.AddComponent<SortingGroup>();
            renderer.sortingOrder = 1;
            var shade = new GameObject("Shadow");
            shade.transform.SetParent(go.transform, false);
            shade.transform.localPosition = ShadowOffset;
            var shaded = shade.AddComponent<SpriteRenderer>();
            shaded.sprite = renderer.sprite;
            shaded.color = ShadowColor;
        }

        // Its reflection: the same picture upside down beneath its foot, drawn under the land. It shows wherever the
        // ground there is ice, as with an ice shard, or a bush growing at the edge of a frozen lake.
        var mirror = new GameObject("Reflection");
        mirror.transform.SetParent(go.transform, false);
        mirror.transform.localPosition = new Vector3(0f, planet.GroundAt(cell.x, cell.y) * PlanetOne.HeightScale, 0f);
        mirror.transform.localScale = new Vector3(1f, PlanetOne.ReflectionSquash, 1f);
        var mirrored = mirror.AddComponent<SpriteRenderer>();
        mirrored.sprite = kind.Sprites[0];
        mirrored.flipY = true;
        mirrored.color = PlanetOne.ReflectionTint;
        mirrored.sortingOrder = PlanetOne.ReflectionOrder;
        shown[cell] = go;
    }

    // Where a thing stands in the world as seen from above.
    static Vector2 Foot(Vector2Int cell)
    {
        return new Vector2(cell.x + 0.5f, cell.y + BaseOffset);
    }

    // Shows the things that stand in the row or column on screen in the side-scroller.
    void BuildSideView()
    {
        foreach (SpriteRenderer renderer in sideView) Destroy(renderer.gameObject);
        sideView.Clear();
        for (int along = mining.WindowStart; along < mining.WindowStart + MiningMode.WindowColumns; along++)
        {
            Vector2Int cell = mining.Cell(along);
            if (!things.TryGetValue(planet.Wrap(cell), out Kind kind)) continue;
            var go = new GameObject(kind.Item.Name);
            go.transform.SetParent(mining.Root, false);
            go.transform.position = new Vector3(along + 0.5f, planet.GroundAt(cell.x, cell.y), 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = kind.Sprites[0];
            renderer.sortingOrder = MiningMode.ObjectSortingOrder;
            // It fills its block of space as far as walking goes, the same as it does in the top-down view.
            var solid = go.AddComponent<BoxCollider2D>();
            solid.size = Vector2.one;
            solid.offset = new Vector2(0f, 0.5f);
            sideView.Add(renderer);
        }
    }

    void Update()
    {
        if (mining.Active)
        {
            foreach (SpriteRenderer renderer in sideView) renderer.color = dayNight.WorldTint;
        }
        else
        {
            ShowNearby();
        }
        if (GameInput.LeftClickPressed()) ClickAt(cam.ScreenToWorldPoint(GameInput.PointerPosition()));
        if (target.HasValue) Walk();
    }

    // Handles a left click at a point on screen (given in world units). Collects the thing there if it is in reach,
    // or starts walking to it if there is a way. Returns false, doing nothing, if there is no thing there or no way to it.
    public bool ClickAt(Vector2 point)
    {
        if (!FindThingAt(point, out Vector2Int cell)) return false;
        if (things[planet.Wrap(cell)].NeedsAxe && (AxeEquipped == null || !AxeEquipped()))
        {
            Say?.Invoke("You need an ice axe to cut down a thornam bush");
            return false;
        }
        if (InReach(cell))
        {
            Collect(cell);
            return true;
        }
        return mining.Active ? PlanSideWalk(cell) : PlanTopDownWalk(cell);
    }

    bool InReach(Vector2Int cell)
    {
        float ground = planet.GroundAt(cell.x, cell.y);
        return Vector2.Distance(player.GroundPosition, Foot(cell)) <= Reach && Mathf.Abs(player.FeetHeight - ground) <= Reach;
    }

    void Collect(Vector2Int cell)
    {
        Vector2Int onPlanet = planet.Wrap(cell);
        Kind kind = things[onPlanet];
        things.Remove(onPlanet);
        if (shown.TryGetValue(cell, out GameObject picture))
        {
            Destroy(picture);
            shown.Remove(cell);
        }
        if (mining.Active) BuildSideView();
        if (kind.Drops == null)
        {
            pickups.Drop(kind.Item, Foot(cell));
        }
        else
        {
            // Several things come loose at once, scattered a little so they can be told apart as they slide over.
            int dropped = 0;
            for (int i = 0; i < kind.Drops.Length; i++)
            {
                for (int n = 0; n < kind.DropCounts[i]; n++, dropped++)
                {
                    float angle = dropped * 2.4f;
                    pickups.Drop(kind.Drops[i], Foot(cell) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f) * (0.12f + 0.05f * dropped));
                }
            }
        }
        target = null;
    }

    // True if a fern or ice shard stands on a square of the world.
    public bool HasThing(Vector2Int cell)
    {
        return things.ContainsKey(planet.Wrap(cell));
    }

    // True if the picture of a fern or ice shard is under a point on screen (given in world units).
    public bool ThingUnder(Vector2 point)
    {
        return FindThingAt(point, out _);
    }

    // Finds the thing whose picture is under a point, in whichever view is showing.
    bool FindThingAt(Vector2 point, out Vector2Int found)
    {
        bool side = mining.Active;
        int px = Mathf.FloorToInt(point.x), py = Mathf.FloorToInt(point.y);
        // Top-down, a thing can be drawn up to a square above where it stands, so look at the squares below the point too.
        // In the side-scroller only the row or column on screen is looked at.
        int firstRow = side ? 0 : py - 2, lastRow = side ? 0 : py + 1;
        for (int y = firstRow; y <= lastRow; y++)
        {
            for (int x = px - 1; x <= px + 1; x++)
            {
                Vector2Int cell = side ? mining.Cell(x) : new Vector2Int(x, y);
                if (!things.TryGetValue(planet.Wrap(cell), out Kind kind)) continue;
                float ground = planet.GroundAt(cell.x, cell.y);
                float bottom = side ? ground : y + BaseOffset + ground * PlanetOne.HeightScale;
                if (Mathf.Abs(point.x - (x + 0.5f)) > kind.Width / 2f + ClickPadding) continue;
                if (point.y < bottom - ClickPadding || point.y > bottom + kind.Height + ClickPadding) continue;
                found = cell;
                return true;
            }
        }
        found = default;
        return false;
    }

    // The player can stand on snow and ice at either height, but not on grey rock or the ship, and cannot walk
    // through another fern or ice shard.
    bool Walkable(int x, int y)
    {
        return planet.TopAt(x, y) <= 1 && !planet.ObstacleAt(x, y);
    }

    // Top-down: the walk is a straight line to the thing, so it works as long as no rock or ship is on that line.
    // Raised ground is fine: the player jumps up and drops down.
    bool PlanTopDownWalk(Vector2Int goal)
    {
        Vector2 from = player.GroundPosition, foot = Foot(goal);
        Vector2 direction = (foot - from).normalized;
        // Check the line the middle of the player follows, and the lines their left and right edges follow.
        Vector2 side = new Vector2(-direction.y, direction.x) * (PlayerController.Width / 2f);
        float length = Vector2.Distance(from, foot) - PlannedReach;
        for (float d = 0f; d <= length; d += 0.2f)
        {
            Vector2 p = from + direction * d;
            if (!WalkableAt(p) || !WalkableAt(p + side) || !WalkableAt(p - side)) return false;
        }
        StartWalk(goal, false);
        return true;
    }

    bool WalkableAt(Vector2 point)
    {
        return Walkable(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
    }

    // Side-scroller: the walk is straight along the row or column, so it works as long as no rock or ship is in between.
    bool PlanSideWalk(Vector2Int goal)
    {
        int from = Mathf.FloorToInt(mining.Along(player.GroundPosition)), to = Mathf.FloorToInt(mining.Along(goal));
        int direction = to > from ? 1 : -1;
        for (int along = from; along != to; along += direction)
        {
            Vector2Int cell = mining.Cell(along);
            if (!Walkable(cell.x, cell.y)) return false;
        }
        StartWalk(goal, true);
        return true;
    }

    void StartWalk(Vector2Int goal, bool inSideView)
    {
        target = goal;
        walkInSideView = inSideView;
        walkColumn = mining.Column;
        walkSlice = mining.Slice;
        lastSpot = player.transform.position;
        lastMovedAt = Time.time;
    }

    // Steers the player one frame further along the walk, and collects the thing on arrival.
    void Walk()
    {
        Vector2Int goal = target.Value;
        // Give up if the thing is gone, the view or the row or column changed, or the player took over the controls.
        bool sameView = mining.Active == walkInSideView && (!walkInSideView || (mining.Column == walkColumn && mining.Slice == walkSlice));
        if (!things.ContainsKey(planet.Wrap(goal)) || !sameView || player.HasManualInput)
        {
            target = null;
            return;
        }
        if (InReach(goal))
        {
            Collect(goal);
            return;
        }

        Vector2 spot = player.transform.position;
        if (Vector2.Distance(spot, lastSpot) > 0.05f)
        {
            lastSpot = spot;
            lastMovedAt = Time.time;
        }
        else if (Time.time - lastMovedAt > StuckSeconds)
        {
            target = null;
            return;
        }

        if (walkInSideView)
        {
            float direction = Mathf.Sign(mining.Along(goal) + 0.5f - spot.x);
            // Jump when the next block along is higher than the player's feet.
            Vector2Int ahead = mining.Cell(Mathf.FloorToInt(spot.x + direction * (PlayerController.Width / 2f + 0.15f)));
            player.Steer(new Vector2(direction, 0f), planet.TopAt(ahead.x, ahead.y) > player.FeetHeight + 0.05f);
            return;
        }

        // Top-down: head straight for it, jumping when the ground just ahead is higher than the player's feet.
        Vector2 heading = (Foot(goal) - player.GroundPosition).normalized;
        Vector2 justAhead = player.GroundPosition + heading * 0.55f;
        int topAhead = planet.TopAt(Mathf.FloorToInt(justAhead.x), Mathf.FloorToInt(justAhead.y));
        player.Steer(heading, topAhead > player.FeetHeight + 0.05f);
    }
}
