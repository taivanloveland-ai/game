using System.Collections.Generic;
using UnityEngine;

// Small things on the ground the player can collect: plants on the snow and sharp ice shards on the ice.
// They show in both views. Right clicking one collects it if the player is at most a block away;
// otherwise the player walks to it in a straight line first, jumping where needed. If a rock or the ship is in the way, nothing happens.
public class Gatherables : MonoBehaviour
{
    // A plant is three quarters as tall as the player.
    const float PlantHeight = PlayerController.Height * 0.75f;
    // How many picture pixels make one block for the ice shard. Its picture is 13 pixels tall, so it stands about 0.73 blocks.
    const float ShardPixelsPerUnit = 8f / (PlayerController.Height * 0.5f);
    // How much of the snow has a plant on it, and how much of the ice has a shard.
    const double PlantDensity = 0.025;
    const double ShardDensity = 0.03;
    // How far across the drawn fern picture the foot of its stem is, from the left edge, as a share of its width.
    const float FernFootAcross = 0.559f;
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
    static readonly Color32 ShardLight = new Color32(204, 231, 244, 255), ShardShade = new Color32(159, 201, 224, 255), ShardDark = new Color32(122, 170, 201, 255);
    // The ice shard, one text row per pixel row from the top. It is long and gently curved, thick at the base and
    // tapering to a point, like the pick of a pickaxe. The base spreads out where it joins the ice.
    // L is the lit side, S the middle, D the far side.
    static readonly string[] ShardPicture =
    {
        "L....",
        "L....",
        "LS...",
        ".LS..",
        ".LS..",
        ".LSD.",
        "..LSD",
        "..LSD",
        "..LSD",
        ".LLSD",
        ".LLSD",
        ".LLSD",
        "LLSSD",
    };

    class Kind
    {
        public Item Item;
        // One sprite per ground height: on raised ground the picture is drawn higher above the same spot.
        public Sprite[] Sprites;
        public float Width, Height;
    }

    class Thing
    {
        public Kind Kind;
        public GameObject TopDown;
    }

    public Item PlantItem { get; private set; }
    public Item ShardItem { get; private set; }
    // True while the player is walking toward something they clicked.
    public bool Walking => target.HasValue;

    PlanetOne planet;
    PlayerController player;
    MiningMode mining;
    DayNight dayNight;
    Pickups pickups;
    Camera cam;
    readonly Dictionary<Vector2Int, Thing> things = new Dictionary<Vector2Int, Thing>();
    readonly List<SpriteRenderer> sideView = new List<SpriteRenderer>();

    // The thing being walked to, and the view and row the walk started in.
    Vector2Int? target;
    bool walkInSideView;
    int walkRow;
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
        Kind plant = MakeKind("Fiddlehead fern", FernPicture[0].Length, FernPicture.Length, FernPicture.Length / PlantHeight, (x, y) =>
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
                var pivot = new Vector2(FernFootAcross, -level * PlanetOne.HeightScale / PlantHeight);
                plant.Sprites[level] = Sprite.Create(fernPicture, whole, pivot, pixelsPerUnit);
            }
            plant.Item = new Item(plant.Item.Name, plant.Sprites[0]);
            plant.Width = fernPicture.width / pixelsPerUnit;
        }
        Kind shard = MakeKind("Ice shard", ShardPicture[0].Length, ShardPicture.Length, ShardPixelsPerUnit, (x, y) =>
        {
            char c = ShardPicture[y][x];
            return c == 'L' ? ShardLight : c == 'S' ? ShardShade : c == 'D' ? ShardDark : Clear;
        });
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
                if (planet.IsSnow(x, y) && roll < PlantDensity) Place(plant, x, y);
                else if (planet.IsIce(new Vector2(x + 0.5f, y + 0.5f)) && roll < ShardDensity) Place(shard, x, y);
            }
        }
        mining.RowBuilt += BuildSideView;
    }

    // Makes a kind of thing from a small picture. The pivot is at its foot. For raised ground, empty space is added
    // underneath so it is drawn that much higher while still being sorted by where it actually stands.
    static Kind MakeKind(string name, int pixelsWide, int pixelsTall, float pixelsPerUnit, System.Func<int, int, Color32> paint)
    {
        var sprites = new Sprite[2];
        for (int level = 0; level < sprites.Length; level++)
        {
            int lift = Mathf.RoundToInt(level * PlanetOne.HeightScale * pixelsPerUnit);
            sprites[level] = PlanetOne.PaintSprite(pixelsWide, pixelsTall + lift, new Vector2(0.5f, 0f), pixelsPerUnit,
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

    void Place(Kind kind, int x, int y)
    {
        var go = new GameObject(kind.Item.Name);
        go.transform.SetParent(planet.TopDownRoot, false);
        go.transform.position = Foot(new Vector2Int(x, y));
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = kind.Sprites[planet.GroundAt(x, y)];
        // Sorted by its foot, so the player walks in front of or behind it correctly.
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        things[new Vector2Int(x, y)] = new Thing { Kind = kind, TopDown = go };
    }

    // Where a thing stands in the world as seen from above.
    static Vector2 Foot(Vector2Int cell)
    {
        return new Vector2(cell.x + 0.5f, cell.y + BaseOffset);
    }

    // Shows the things that stand in the row on screen in the side-scroller.
    void BuildSideView()
    {
        foreach (SpriteRenderer renderer in sideView) Destroy(renderer.gameObject);
        sideView.Clear();
        for (int x = 0; x < planet.width; x++)
        {
            if (!things.TryGetValue(new Vector2Int(x, mining.Row), out Thing thing)) continue;
            var go = new GameObject(thing.Kind.Item.Name);
            go.transform.SetParent(mining.Root, false);
            go.transform.position = new Vector3(x + 0.5f, planet.GroundAt(x, mining.Row), 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = thing.Kind.Sprites[0];
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
        if (GameInput.HarvestPressed()) ClickAt(cam.ScreenToWorldPoint(GameInput.PointerPosition()));
        if (target.HasValue) Walk();
    }

    // Handles a right click at a point on screen (given in world units). Collects the thing there if it is in reach,
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
        Thing thing = things[cell];
        Destroy(thing.TopDown);
        things.Remove(cell);
        if (mining.Active) BuildSideView();
        pickups.Drop(thing.Kind.Item, Foot(cell));
        target = null;
    }

    // Finds the thing whose picture is under a point, in whichever view is showing.
    bool FindThingAt(Vector2 point, out Vector2Int found)
    {
        bool side = mining.Active;
        int px = Mathf.FloorToInt(point.x), py = Mathf.FloorToInt(point.y);
        // Top-down, a thing can be drawn up to a square above where it stands, so look at the squares below the point too.
        int firstRow = side ? mining.Row : py - 2, lastRow = side ? mining.Row : py + 1;
        for (int y = firstRow; y <= lastRow; y++)
        {
            for (int x = px - 1; x <= px + 1; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!things.TryGetValue(cell, out Thing thing)) continue;
                float ground = planet.GroundAt(x, y);
                float bottom = side ? ground : y + BaseOffset + ground * PlanetOne.HeightScale;
                if (Mathf.Abs(point.x - (x + 0.5f)) > thing.Kind.Width / 2f + ClickPadding) continue;
                if (point.y < bottom - ClickPadding || point.y > bottom + thing.Kind.Height + ClickPadding) continue;
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

    // Side-scroller: the walk is straight along the row, so it works as long as no rock or ship is in between.
    bool PlanSideWalk(Vector2Int goal)
    {
        int from = Mathf.FloorToInt(player.GroundPosition.x);
        int direction = goal.x > from ? 1 : -1;
        for (int x = from; x != goal.x; x += direction)
        {
            if (!Walkable(x, goal.y)) return false;
        }
        StartWalk(goal, true);
        return true;
    }

    void StartWalk(Vector2Int goal, bool inSideView)
    {
        target = goal;
        walkInSideView = inSideView;
        walkRow = mining.Row;
        lastSpot = player.transform.position;
        lastMovedAt = Time.time;
    }

    // Steers the player one frame further along the walk, and collects the thing on arrival.
    void Walk()
    {
        Vector2Int goal = target.Value;
        // Give up if the thing is gone, the view or row changed, or the player took over the controls.
        bool sameView = mining.Active == walkInSideView && (!walkInSideView || mining.Row == walkRow);
        if (!things.ContainsKey(goal) || !sameView || player.HasManualInput)
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
            float direction = Mathf.Sign(goal.x + 0.5f - spot.x);
            // Jump when the next block along is higher than the player's feet.
            int ahead = Mathf.FloorToInt(spot.x + direction * (PlayerController.Width / 2f + 0.15f));
            player.Steer(new Vector2(direction, 0f), planet.TopAt(ahead, goal.y) > player.FeetHeight + 0.05f);
            return;
        }

        // Top-down: head straight for it, jumping when the ground just ahead is higher than the player's feet.
        Vector2 heading = (Foot(goal) - player.GroundPosition).normalized;
        Vector2 justAhead = player.GroundPosition + heading * 0.55f;
        int topAhead = planet.TopAt(Mathf.FloorToInt(justAhead.x), Mathf.FloorToInt(justAhead.y));
        player.Steer(heading, topAhead > player.FeetHeight + 0.05f);
    }
}
