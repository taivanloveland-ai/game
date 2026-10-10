using System.Collections.Generic;
using UnityEngine;

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
    // How far across the drawn fern picture the foot of its stem is, from the left edge, as a share of its width.
    const float FernFootAcross = 0.472f;
    // How far up the picture the ground line is, from the bottom edge, as a share of its height.
    // The snow mound and shadow at the fern's foot sit around and just below this line.
    const float FernFootUp = 0.088f;
    // How big the picture of a loose ice shard is, in blocks, before the dropped item is shrunk a little.
    const float LooseShardSize = 0.9f;
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
    }


    public Item PlantItem { get; private set; }
    public Item ShardItem { get; private set; }
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
        // Once it has been knocked loose, an ice shard looks like the drawn picture, both on the ground and in the inventory.
        var shardPicture = Resources.Load<Texture2D>("Art/IceShardItem");
        if (shardPicture != null)
        {
            var whole = new Rect(0, 0, shardPicture.width, shardPicture.height);
            shard.Item = new Item(shard.Item.Name, Sprite.Create(shardPicture, whole, new Vector2(0.5f, 0.06f), shardPicture.height / LooseShardSize));
        }
        PlantItem = plant.Item;
        ShardItem = shard.Item;

        var rand = new System.Random(planet.seed + 1);
        for (int y = 0; y < planet.height; y++)
        {
            for (int x = 0; x < planet.width; x++)
            {
                double roll = rand.NextDouble();
                if (planet.IsSnow(x, y) && roll < PlantDensity) things[new Vector2Int(x, y)] = plant;
                else if (planet.IsIce(new Vector2(x + 0.5f, y + 0.5f)) && roll < ShardDensity) things[new Vector2Int(x, y)] = shard;
            }
        }
        mining.RowBuilt += BuildSideView;
        planet.BlockMined += OnBlockMined;
        ShowNearby();
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
        pickups.Drop(kind.Item, Foot(cell));
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

    // The player can stand on snow and ice at either height, but not on grey rock or the ship.
    bool Walkable(int x, int y)
    {
        return planet.TopAt(x, y) <= 1;
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
