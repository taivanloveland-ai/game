using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Ice spiders: pale creatures about a block wide that live on the ice. They wander until the player comes near, then
// scuttle after them and bite when they catch up. Each bite takes some of the player's health.
// They are drawn from above in the top-down view and from the side in the side-scroller, after Taivan's drawing:
// a pale ice-blue body, eight jointed legs, the same on both sides.
public class Spiders : MonoBehaviour
{
    // How much of the ice has a spider on it when the world is made.
    const double Density = 0.0012;
    // No spider starts this close to where the player starts.
    const int ClearOfStart = 14;
    // Only spiders this close to the player move and are drawn.
    const float ActiveRadius = 40f;
    // A spider notices the player from this many squares away, and gives up the chase past the second distance.
    const float NoticeRadius = 7f, GiveUpRadius = 11f;
    // Squares per second when chasing and when wandering. The player's own walking speed is 3.
    const float ChaseSpeed = 2.4f, WanderSpeed = 0.9f;
    // It bites when it is this close, and one bite takes this long.
    const float BiteDistance = 0.85f * Size, BiteSeconds = 0.5f;
    // How much health one bite takes, and how far into the bite it lands. A spider bites every 0.8 seconds.
    const float BiteDamage = 8f, BiteLandsAt = BiteSeconds * 0.55f;
    // A spider takes 5 hits from the ice axe. The axe reaches this many blocks, swings this often when the button
    // is held, and knocks a spider back this far.
    const float MaxHealth = 50f, AxeDamage = 10f, AxeReach = 2.2f, SwingSeconds = 0.45f, KnockBack = 0.5f;
    // A dead spider leaves 1 silk, this many legs and 1 fang, which slide to the player from this far away.
    const int LegsDropped = 2;
    const float DropPullRadius = 3f, DropScale = 0.8f;
    // How far the pictures of those reach from their middle, in blocks.
    const float LooseHalf = 0.4f;
    // Seen from above, it bites when its middle is this far from the edge of the player, which puts its fangs
    // just short of them. The players picture starts this far below the spot they stand on.
    const float TopBiteGap = 0.4f * Size, FeetBelow = 0.2f;
    // How far a spider walks in one full cycle of its legs.
    const float StrideDistance = 0.55f * Size;
    // How big a spider is drawn, against the size its pictures are painted at (about a block across, legs and all).
    const float Size = 1.3f;

    // The pictures are painted at the same fine scale as the snow picture.
    const int Pixels = 128;
    const int WalkFrames = 8, BiteFrames = 5;
    // How far each picture reaches from its middle, in blocks.
    const float TopHalf = 0.62f, SideHalfWidth = 0.66f, SideHeight = 0.6f;
    // The darker edge round every part of the spider, in blocks.
    const float Rim = 0.016f;

    static readonly Color BodyColor = new Color(0.86f, 0.96f, 0.99f), BodyLight = new Color(0.95f, 0.99f, 1f);
    static readonly Color BodyMark = new Color(0.68f, 0.86f, 0.95f), LegColor = new Color(0.74f, 0.9f, 0.97f);
    static readonly Color JointColor = new Color(0.88f, 0.97f, 1f), FangColor = new Color(0.5f, 0.74f, 0.9f);
    static readonly Color EyeColor = new Color(0.1f, 0.22f, 0.42f), RimColor = new Color(0.36f, 0.6f, 0.78f);
    static readonly Color HurtTint = new Color(1f, 0.45f, 0.45f), SilkColor = new Color(0.95f, 0.98f, 1f);

    // One round or sausage-shaped part of a spider's picture. Later parts are painted over earlier ones.
    struct Part
    {
        public bool Round;
        public Vector2 A, B;
        public float RadiusA, RadiusB;
        public Color Color;
        // Markings and eyes sit on top of the body and have no edge of their own.
        public bool Flat;
    }

    class Spider
    {
        // Where it is within the planet, and which way it faces.
        public Vector2 Position;
        // The square it started on, which is the same every time a world is made from its seed.
        public Vector2Int Origin;
        public Vector2 Heading = Vector2.up;
        // How far through a cycle of its legs it is, from 0 to 1.
        public float Gait;
        // How far through a bite it is in seconds, or less than 0 when it is not biting.
        public float Bite = -1f;
        public bool Chasing;
        public Vector2 WanderDirection;
        public float DecideAt;
        public SpriteRenderer Renderer;
        public SortingGroup Sorting;
        public float Health = MaxHealth;
        // It is drawn red until this time, after being hit.
        public float HurtUntil;
    }

    PlanetOne planet;
    PlayerController player;
    MiningMode mining;
    DayNight dayNight;
    Health health;
    Inventory inventory;
    CraftingNook nook;
    Pickups pickups;
    Camera cam;
    float nextSwingAt;
    readonly List<Spider> spiders = new List<Spider>();
    // True while the player is in a cave. Spiders live on the surface, so none are shown or move then.
    public bool Underground;
    Sprite[] topWalk, topBite, sideWalk, sideBite;
    System.Random rand;

    public int Count => spiders.Count;
    // What a dead spider leaves behind.
    // The starting squares of the spiders that have been killed, for saved games.
    readonly HashSet<Vector2Int> killed = new HashSet<Vector2Int>();

    public void Write(System.IO.BinaryWriter w)
    {
        w.Write(killed.Count);
        foreach (Vector2Int cell in killed)
        {
            w.Write(cell.x);
            w.Write(cell.y);
        }
    }

    public void Read(System.IO.BinaryReader r)
    {
        int count = r.ReadInt32();
        for (int i = 0; i < count; i++) killed.Add(new Vector2Int(r.ReadInt32(), r.ReadInt32()));
        spiders.RemoveAll(spider => killed.Contains(spider.Origin));
    }

    public Item SilkItem { get; private set; }
    public Item LegItem { get; private set; }
    public Item FangItem { get; private set; }

    public enum Strike { Missed, TooFar, Hit, Killed }

    public void Setup(PlanetOne planet, PlayerController player, MiningMode mining, DayNight dayNight, Health health, Inventory inventory, CraftingNook nook, Pickups pickups, Camera cam)
    {
        this.planet = planet;
        this.player = player;
        this.mining = mining;
        this.dayNight = dayNight;
        this.health = health;
        this.inventory = inventory;
        this.nook = nook;
        this.pickups = pickups;
        this.cam = cam;

        SilkItem = new Item("Spider silk", Paint(SilkParts(), LooseHalf, 0.04f, LooseHalf * 2f - 0.04f));
        LegItem = new Item("Spider leg", Paint(LegParts(), LooseHalf, 0.04f, LooseHalf * 2f - 0.04f));
        FangItem = new Item("Spider fang", Paint(FangParts(), LooseHalf, 0.04f, LooseHalf * 2f - 0.04f));

        topWalk = new Sprite[WalkFrames];
        sideWalk = new Sprite[WalkFrames];
        for (int i = 0; i < WalkFrames; i++)
        {
            topWalk[i] = Paint(TopParts(i / (float)WalkFrames, 0f, 0f), TopHalf, TopHalf, TopHalf);
            sideWalk[i] = Paint(SideParts(i / (float)WalkFrames, 0f, 0f), SideHalfWidth, 0f, SideHeight);
        }
        topBite = new Sprite[BiteFrames];
        sideBite = new Sprite[BiteFrames];
        for (int i = 0; i < BiteFrames; i++)
        {
            // A bite: rear back with the fangs wide, lunge, snap them shut, and settle.
            float through = i / (float)(BiteFrames - 1);
            float lunge = Mathf.Sin(Mathf.Clamp01((through - 0.15f) / 0.85f) * Mathf.PI);
            float open = through < 0.5f ? 1f : Mathf.Clamp01(1f - (through - 0.5f) / 0.2f);
            topBite[i] = Paint(TopParts(0f, lunge, open), TopHalf, TopHalf, TopHalf);
            sideBite[i] = Paint(SideParts(0f, lunge, open), SideHalfWidth, 0f, SideHeight);
        }

        rand = new System.Random(planet.seed + 7);
        Vector2Int start = planet.CrashSite;
        for (int y = 0; y < planet.height; y++)
        {
            for (int x = 0; x < planet.width; x++)
            {
                if (rand.NextDouble() >= Density) continue;
                if (Mathf.Abs(x - start.x) <= ClearOfStart && Mathf.Abs(y - start.y) <= ClearOfStart) continue;
                if (!CanStand(new Vector2Int(x, y), planet.TopAt(x, y))) continue;
                spiders.Add(new Spider { Position = new Vector2(x + 0.5f, y + 0.5f), Origin = new Vector2Int(x, y), Heading = RandomDirection() });
            }
        }
    }

    Vector2 RandomDirection()
    {
        float angle = (float)(rand.NextDouble() * Mathf.PI * 2f);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    // A spider keeps to the ice. It can go up or down a block, but not through an ice shard.
    bool CanStand(Vector2Int cell, int fromLevel)
    {
        return planet.IsIce(new Vector2(cell.x + 0.5f, cell.y + 0.5f)) && !planet.ObstacleAt(cell.x, cell.y) && Mathf.Abs(planet.TopAt(cell.x, cell.y) - fromLevel) <= 1;
    }

    void Update()
    {
        if (Underground)
        {
            foreach (Spider spider in spiders)
            {
                if (spider.Renderer != null) spider.Renderer.enabled = false;
            }
            return;
        }
        Vector2 playerAt = player.GroundPosition;
        float playerFeet = player.FeetHeight;

        // With the ice axe equipped, a left click swings it at the spider under the mouse. Holding the button keeps
        // swinging at a steady pace.
        if (Time.time >= nextSwingAt && inventory.Equipped == nook.Axe && !health.Down && (GameInput.LeftClickPressed() || GameInput.LeftClickHeld()))
        {
            if (StrikeAt(cam.ScreenToWorldPoint(GameInput.PointerPosition())) >= Strike.Hit) nextSwingAt = Time.time + SwingSeconds;
        }

        foreach (Spider spider in spiders)
        {
            // The planet repeats, so work with the copy of the spider that is nearest the player.
            Vector2 at = planet.Nearest(spider.Position, playerAt);
            Vector2 toPlayer = playerAt - at;
            float distance = toPlayer.magnitude;
            if (distance > ActiveRadius)
            {
                if (spider.Renderer != null) spider.Renderer.enabled = false;
                continue;
            }

            Vector2Int cell = Vector2Int.FloorToInt(at);
            int level = planet.TopAt(cell.x, cell.y);
            bool sameHeight = Mathf.Abs(playerFeet - level) < 1.6f;
            // It loses interest once the player has fallen.
            spider.Chasing = sameHeight && !health.Down && distance < (spider.Chasing ? GiveUpRadius : NoticeRadius);

            // What the spider goes for is the edge of the player, not the spot under their feet, so that it stops
            // beside them instead of underneath. Seen from above the player stands up the screen from their feet.
            Vector2 body = playerAt, toBody = toPlayer;
            float reach = distance, biteAt = BiteDistance;
            if (!mining.Active)
            {
                float bottom = playerAt.y - FeetBelow, halfWidth = PlayerController.Width / 2f;
                body = new Vector2(playerAt.x, bottom + PlayerController.Height / 2f);
                var nearest = new Vector2(Mathf.Clamp(at.x, playerAt.x - halfWidth, playerAt.x + halfWidth), Mathf.Clamp(at.y, bottom, bottom + PlayerController.Height));
                toBody = nearest - at;
                reach = toBody.magnitude;
                biteAt = TopBiteGap;
            }

            float moved = 0f;
            if (spider.Chasing && reach < biteAt * 0.6f)
            {
                // Too close, as when the player walks into it: back away so it is never underneath them.
                Vector2 away = at - body;
                Vector2 before = at;
                at = Step(at, (away.sqrMagnitude > 0.0001f ? away.normalized : spider.Heading * -1f) * (ChaseSpeed * Time.deltaTime), level);
                moved = Vector2.Distance(before, at);
            }
            else if (spider.Chasing && reach <= biteAt)
            {
                // Close enough: face the player and bite, over and over.
                if (reach > 0.01f) spider.Heading = toBody / reach;
                float before = spider.Bite;
                spider.Bite = spider.Bite < 0f ? 0f : (spider.Bite + Time.deltaTime) % (BiteSeconds * 1.6f);
                // The bite lands at the end of the lunge.
                if (before < BiteLandsAt && spider.Bite >= BiteLandsAt) health.Hurt(BiteDamage);
            }
            else
            {
                // A bite that has started is finished before the spider moves on.
                if (spider.Bite >= 0f)
                {
                    spider.Bite += Time.deltaTime;
                    if (spider.Bite >= BiteSeconds) spider.Bite = -1f;
                }
                if (spider.Bite < 0f)
                {
                    Vector2 direction = !spider.Chasing ? Wander(spider) : reach > 0.001f ? toBody / reach : Vector2.zero;
                    if (direction != Vector2.zero)
                    {
                        Vector2 before = at;
                        at = Step(at, direction * ((spider.Chasing ? ChaseSpeed : WanderSpeed) * Time.deltaTime), level);
                        moved = Vector2.Distance(before, at);
                        if (moved > 0.0001f) spider.Heading = Vector2.Lerp(spider.Heading, (at - before) / moved, 12f * Time.deltaTime).normalized;
                        // A wandering spider that walks into something thinks again.
                        else if (!spider.Chasing) spider.DecideAt = 0f;
                    }
                }
            }
            spider.Gait = Mathf.Repeat(spider.Gait + moved / StrideDistance, 1f);
            spider.Position = planet.WrapPoint(at);
            Draw(spider, at);
        }
    }

    // One swing of the ice axe at a point on screen (given in world units). It hits the spider drawn there, if the
    // player is close enough to reach it.
    public Strike StrikeAt(Vector2 point)
    {
        Vector2 playerAt = player.GroundPosition;
        Spider struck = null;
        float nearest = 0.6f * Size;
        foreach (Spider spider in spiders)
        {
            if (spider.Renderer == null || !spider.Renderer.enabled) continue;
            // From the side a spider's picture stands on its feet, so its middle is a little way up.
            Vector2 drawnAt = (Vector2)spider.Renderer.transform.position + (mining.Active ? new Vector2(0f, 0.2f * Size) : Vector2.zero);
            float away = Vector2.Distance(point, drawnAt);
            if (away > nearest) continue;
            nearest = away;
            struck = spider;
        }
        if (struck == null) return Strike.Missed;

        Vector2 at = planet.Nearest(struck.Position, playerAt);
        if (Vector2.Distance(at, playerAt) > AxeReach) return Strike.TooFar;

        struck.Health -= AxeDamage;
        struck.HurtUntil = Time.time + 0.15f;
        struck.Bite = -1f;
        if (struck.Health <= 0f)
        {
            // It leaves its silk, a couple of legs and a fang where it died.
            Vector2Int cell = Vector2Int.FloorToInt(at);
            float level = planet.TopAt(cell.x, cell.y);
            pickups.Drop(SilkItem, at, level, DropPullRadius, DropScale);
            for (int i = 0; i < LegsDropped; i++) pickups.Drop(LegItem, at + new Vector2(i == 0 ? -0.3f : 0.3f, 0.1f), level, DropPullRadius, DropScale);
            pickups.Drop(FangItem, at + new Vector2(0f, -0.25f), level, DropPullRadius, DropScale);
            Destroy(struck.Sorting.gameObject);
            spiders.Remove(struck);
            killed.Add(struck.Origin);
            return Strike.Killed;
        }

        // The blow knocks it back, away from the player.
        Vector2 back = at - playerAt;
        if (back.sqrMagnitude > 0.0001f)
        {
            Vector2Int cell = Vector2Int.FloorToInt(at);
            at = Step(at, back.normalized * KnockBack, planet.TopAt(cell.x, cell.y));
            struck.Position = planet.WrapPoint(at);
        }
        return Strike.Hit;
    }

    // What a dead spider leaves: a skein of silk, a jointed leg, and a curved fang.
    static List<Part> SilkParts()
    {
        var parts = new List<Part>();
        parts.Add(Sausage(new Vector2(0.2f, 0.26f), new Vector2(0.33f, 0.1f), 0.02f, 0.008f, SilkColor));
        parts.Add(Ball(new Vector2(0f, 0.32f), 0.27f, 0.17f, SilkColor));
        for (int i = -2; i <= 2; i++) parts.Add(Ball(new Vector2(0f, 0.32f + i * 0.055f), 0.25f - Mathf.Abs(i) * 0.035f, 0.01f, BodyMark, true));
        parts.Add(Sausage(new Vector2(0f, 0.17f), new Vector2(0f, 0.47f), 0.04f, 0.04f, LegColor));
        return parts;
    }

    static List<Part> LegParts()
    {
        var parts = new List<Part>();
        var hip = new Vector2(-0.28f, 0.14f);
        var knee = new Vector2(-0.02f, 0.52f);
        var foot = new Vector2(0.3f, 0.08f);
        parts.Add(Sausage(hip, knee, 0.065f, 0.052f, LegColor));
        parts.Add(Sausage(knee, foot, 0.048f, 0.014f, LegColor));
        parts.Add(Ball(knee, 0.062f, 0.062f, Color.Lerp(LegColor, JointColor, 0.7f)));
        parts.Add(Ball(hip, 0.075f, 0.075f, BodyColor));
        return parts;
    }

    static List<Part> FangParts()
    {
        var parts = new List<Part>();
        parts.Add(Sausage(new Vector2(-0.1f, 0.54f), new Vector2(0.02f, 0.32f), 0.09f, 0.06f, FangColor));
        parts.Add(Sausage(new Vector2(0.02f, 0.32f), new Vector2(0.15f, 0.1f), 0.06f, 0.012f, FangColor));
        parts.Add(Ball(new Vector2(-0.12f, 0.58f), 0.12f, 0.085f, BodyColor));
        return parts;
    }

    // Wandering: walk one way for a while, or stand still for a while, then choose again.
    Vector2 Wander(Spider spider)
    {
        if (Time.time >= spider.DecideAt)
        {
            spider.WanderDirection = rand.NextDouble() < 0.6 ? RandomDirection() : Vector2.zero;
            spider.DecideAt = Time.time + 1.2f + (float)rand.NextDouble() * 2.5f;
        }
        return spider.WanderDirection;
    }

    // Moves a spider as far as the ice lets it, sliding along an edge instead of stopping dead.
    Vector2 Step(Vector2 from, Vector2 by, int level)
    {
        Vector2 whole = from + by;
        if (CanStand(Vector2Int.FloorToInt(whole), level)) return whole;
        var across = new Vector2(from.x + by.x, from.y);
        if (by.x != 0f && CanStand(Vector2Int.FloorToInt(across), level)) return across;
        var along = new Vector2(from.x, from.y + by.y);
        if (by.y != 0f && CanStand(Vector2Int.FloorToInt(along), level)) return along;
        return from;
    }

    // Shows a spider where it is, in whichever view is on screen.
    void Draw(Spider spider, Vector2 at)
    {
        if (spider.Renderer == null)
        {
            // The picture hangs under a holder. In the top-down view things are drawn in order of how far up the
            // screen they are, and it is the holder's place that counts, so the picture can be put in the right
            // order without being moved.
            var holder = new GameObject("Ice Spider");
            holder.transform.SetParent(transform, false);
            spider.Sorting = holder.AddComponent<SortingGroup>();
            var go = new GameObject("Picture");
            go.transform.SetParent(holder.transform, false);
            spider.Renderer = go.AddComponent<SpriteRenderer>();
            go.transform.localScale = new Vector3(Size, Size, 1f);
        }
        SpriteRenderer renderer = spider.Renderer;
        Transform holderAt = spider.Sorting.transform;
        Vector2Int cell = Vector2Int.FloorToInt(at);
        int level = planet.TopAt(cell.x, cell.y);
        bool biting = spider.Bite >= 0f && spider.Bite < BiteSeconds;
        int frame = biting
            ? Mathf.Min(BiteFrames - 1, (int)(spider.Bite / BiteSeconds * BiteFrames))
            : Mathf.Min(WalkFrames - 1, (int)(spider.Gait * WalkFrames));

        if (mining.Active)
        {
            // From the side, only the spiders in the row or column on screen are seen. They face left or right.
            renderer.enabled = mining.Shows(cell);
            if (!renderer.enabled) return;
            float facing = mining.Column ? spider.Heading.y : spider.Heading.x;
            renderer.sprite = biting ? sideBite[frame] : sideWalk[frame];
            renderer.flipX = facing < 0f;
            holderAt.position = new Vector3(mining.Along(at), level, 0f);
            renderer.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            spider.Sorting.sortingOrder = MiningMode.ObjectSortingOrder;
            renderer.color = Time.time < spider.HurtUntil ? HurtTint : dayNight.WorldTint;
            return;
        }

        // From above, the picture is turned to face the way the spider is heading.
        renderer.enabled = true;
        renderer.sprite = biting ? topBite[frame] : topWalk[frame];
        renderer.flipX = false;
        float turn = Mathf.Atan2(spider.Heading.y, spider.Heading.x) * Mathf.Rad2Deg - 90f;
        float sortAt = SortHeight(at, cell, level);
        holderAt.position = new Vector3(at.x, sortAt, 0f);
        renderer.transform.SetLocalPositionAndRotation(new Vector3(0f, at.y + level * PlanetOne.HeightScale - sortAt, 0f), Quaternion.Euler(0f, 0f, turn));
        spider.Sorting.sortingOrder = 0;
        renderer.color = Time.time < spider.HurtUntil ? HurtTint : Color.white;
    }

    // How far up the screen a spider counts as being, for deciding what is drawn over what in the top-down view.
    // A spider lies flat on the ground and reaches over the squares south of it. Each square of ground is drawn as
    // if it stood at its far edge, so a spider placed by its middle would be covered by the flat ground just south
    // of it. Instead it counts as being at its own southern edge, in front of all the ground it lies on. The one
    // thing that should cover it is ground to the south that stands higher than it does, so it is kept just behind
    // the nearest row with any of that.
    float SortHeight(Vector2 at, Vector2Int cell, int level)
    {
        float reach = TopHalf * Size;
        float sortAt = at.y - reach;
        for (int row = cell.y - 1; row >= Mathf.FloorToInt(at.y - reach); row--)
        {
            bool higher = false;
            for (int x = Mathf.FloorToInt(at.x - reach); x <= Mathf.FloorToInt(at.x + reach) && !higher; x++) higher = planet.TopAt(x, row) > level;
            if (!higher) continue;
            // A square is drawn as if at its far edge, which is one above its row.
            return Mathf.Max(sortAt, row + 1.01f);
        }
        return sortAt;
    }

    // The spider seen from above, facing up the picture. Distances are in blocks from its middle. Gait is how far
    // through a cycle of its legs it is; lunge and open (0 to 1) are how far it has thrust forward and how wide its
    // fangs are, for biting. Everything is made for one side and mirrored, so the two sides always match.
    static List<Part> TopParts(float gait, float lunge, float open)
    {
        var parts = new List<Part>();
        float forward = lunge * 0.07f - (open > 0f && lunge < 0.2f ? 0.02f : 0f);
        var body = new Vector2(0f, forward);
        float[] footOut = { 0.3f, 0.48f, 0.5f, 0.36f }, footUp = { 0.47f, 0.2f, -0.1f, -0.4f };

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 4; i++)
            {
                // The legs move in two sets of four, each set swinging forward while the other pushes back.
                float phase = (gait + ((i + (side > 0 ? 0 : 1)) % 2 == 0 ? 0f : 0.5f)) * Mathf.PI * 2f;
                float swing = Mathf.Max(0f, Mathf.Cos(phase));
                var hip = body + new Vector2(side * 0.085f, 0.16f - i * 0.055f);
                var foot = new Vector2(side * footOut[i] * (1f - 0.07f * swing), footUp[i] + 0.075f * Mathf.Sin(phase));
                Vector2 middle = (hip + foot) / 2f;
                Vector2 knee = middle + (middle - body).normalized * 0.055f;
                AddLeg(parts, hip, knee, foot, LegColor);
            }
        }
        for (int side = -1; side <= 1; side += 2)
        {
            // Fangs, and the two short feelers beside them.
            parts.Add(Sausage(body + new Vector2(side * 0.035f, 0.18f), body + new Vector2(side * (0.016f + 0.07f * open), 0.3f), 0.026f, 0.008f, FangColor));
            parts.Add(Sausage(body + new Vector2(side * 0.07f, 0.17f), body + new Vector2(side * 0.1f, 0.25f), 0.016f, 0.01f, LegColor));
        }

        parts.Add(Ball(body + new Vector2(0f, -0.15f), 0.16f, 0.2f, BodyColor));
        parts.Add(Ball(body + new Vector2(0f, -0.13f), 0.085f, 0.12f, BodyLight, true));
        parts.Add(Ball(body + new Vector2(0f, -0.1f), 0.06f, 0.014f, BodyMark, true));
        parts.Add(Ball(body + new Vector2(0f, -0.17f), 0.08f, 0.014f, BodyMark, true));
        parts.Add(Ball(body + new Vector2(0f, -0.24f), 0.065f, 0.014f, BodyMark, true));
        parts.Add(Ball(body + new Vector2(0f, 0.09f), 0.115f, 0.12f, BodyColor));
        parts.Add(Ball(body + new Vector2(0f, 0.07f), 0.06f, 0.065f, BodyLight, true));
        for (int side = -1; side <= 1; side += 2)
        {
            parts.Add(Ball(body + new Vector2(side * 0.028f, 0.165f), 0.016f, 0.016f, EyeColor, true));
            parts.Add(Ball(body + new Vector2(side * 0.066f, 0.14f), 0.012f, 0.012f, EyeColor, true));
        }
        return parts;
    }

    // The spider seen from the side, facing right, standing on the bottom of the picture.
    static List<Part> SideParts(float gait, float lunge, float open)
    {
        var parts = new List<Part>();
        float forward = lunge * 0.08f - (open > 0f && lunge < 0.2f ? 0.02f : 0f);
        float height = 0.2f + 0.008f * Mathf.Sin(gait * Mathf.PI * 4f);
        var body = new Vector2(forward, height);
        float[] footAlong = { 0.5f, 0.3f, -0.14f, -0.44f };
        Color farLeg = LegColor * 0.8f;
        farLeg.a = 1f;

        // The four legs on the far side first, a shade darker, then the body, then the four near legs.
        for (int near = 0; near <= 1; near++)
        {
            if (near == 1)
            {
                parts.Add(Sausage(body + new Vector2(0.19f, -0.02f), body + new Vector2(0.22f + 0.05f * open, -0.12f + 0.03f * open), 0.022f, 0.006f, FangColor));
                parts.Add(Ball(body + new Vector2(-0.18f, 0.035f), 0.19f, 0.13f, BodyColor));
                parts.Add(Ball(body + new Vector2(-0.2f, 0.07f), 0.12f, 0.06f, BodyLight, true));
                parts.Add(Ball(body + new Vector2(-0.19f, 0.0f), 0.12f, 0.012f, BodyMark, true));
                parts.Add(Ball(body + new Vector2(0.1f, 0f), 0.125f, 0.09f, BodyColor));
                parts.Add(Ball(body + new Vector2(0.09f, 0.03f), 0.07f, 0.035f, BodyLight, true));
                parts.Add(Ball(body + new Vector2(0.185f, 0.03f), 0.016f, 0.016f, EyeColor, true));
                parts.Add(Ball(body + new Vector2(0.15f, 0.055f), 0.012f, 0.012f, EyeColor, true));
            }
            for (int i = 0; i < 4; i++)
            {
                float phase = (gait + ((i + near) % 2 == 0 ? 0f : 0.5f)) * Mathf.PI * 2f;
                float lift = 0.06f * Mathf.Max(0f, Mathf.Cos(phase));
                var hip = body + new Vector2(0.09f - i * 0.05f, 0.02f);
                var foot = new Vector2(footAlong[i] + (near == 1 ? 0f : 0.03f) + 0.075f * Mathf.Sin(phase), 0.012f + lift);
                // The knee stands high above the body, as a spider's does.
                var knee = new Vector2(Mathf.Lerp(hip.x, foot.x, 0.55f), height + 0.15f + lift * 0.5f);
                AddLeg(parts, hip, knee, foot, near == 1 ? LegColor : farLeg);
            }
        }
        return parts;
    }

    // A leg in two pieces, thick at the hip and thin at the foot, with a pale joint at the knee.
    static void AddLeg(List<Part> parts, Vector2 hip, Vector2 knee, Vector2 foot, Color color)
    {
        parts.Add(Sausage(hip, knee, 0.034f, 0.027f, color));
        parts.Add(Sausage(knee, foot, 0.026f, 0.01f, color));
        Color joint = Color.Lerp(color, JointColor, 0.7f);
        parts.Add(Ball(knee, 0.03f, 0.03f, joint));
    }

    static Part Ball(Vector2 centre, float across, float up, Color color, bool flat = false)
    {
        return new Part { Round = true, A = centre, RadiusA = across, RadiusB = up, Color = color, Flat = flat };
    }

    static Part Sausage(Vector2 from, Vector2 to, float radiusFrom, float radiusTo, Color color)
    {
        return new Part { A = from, B = to, RadiusA = radiusFrom, RadiusB = radiusTo, Color = color };
    }

    // How far a point is outside a part (negative inside it), in blocks.
    static float Outside(Part part, Vector2 point)
    {
        if (part.Round)
        {
            Vector2 d = point - part.A;
            float reach = Mathf.Sqrt(d.x * d.x / (part.RadiusA * part.RadiusA) + d.y * d.y / (part.RadiusB * part.RadiusB));
            return (reach - 1f) * Mathf.Min(part.RadiusA, part.RadiusB);
        }
        Vector2 line = part.B - part.A;
        float along = Mathf.Clamp01(Vector2.Dot(point - part.A, line) / line.sqrMagnitude);
        return Vector2.Distance(point, part.A + line * along) - Mathf.Lerp(part.RadiusA, part.RadiusB, along);
    }

    // Paints a picture from its parts. The picture reaches halfWidth blocks either side of the middle, and from
    // `below` blocks under the middle to `above` blocks over it. The sprite's pivot is at the middle.
    static Sprite Paint(List<Part> parts, float halfWidth, float below, float above)
    {
        int pixelWidth = Mathf.CeilToInt(halfWidth * 2f * Pixels), pixelHeight = Mathf.CeilToInt((below + above) * Pixels);
        float pixel = 1f / Pixels;
        var pivot = new Vector2(0.5f, below / (below + above));
        return PlanetOne.PaintSprite(pixelWidth, pixelHeight, pivot, Pixels, true, (x, y) =>
        {
            var point = new Vector2((x + 0.5f) * pixel - halfWidth, above - (y + 0.5f) * pixel);
            Color result = Color.clear;
            foreach (Part part in parts)
            {
                float outside = Outside(part, point);
                if (outside > pixel) continue;
                // Each part has a darker edge, which gives the pale spider its shape against pale ice. Markings
                // and eyes are left as they are.
                Color color = part.Flat ? part.Color : Color.Lerp(part.Color, RimColor, Mathf.Clamp01((outside + Rim) / Rim));
                float cover = Mathf.Clamp01(0.5f - outside / pixel);
                float alpha = cover + result.a * (1f - cover);
                result = Color.Lerp(result, color, alpha > 0f ? cover / alpha : 0f);
                result.a = alpha;
            }
            return result;
        });
    }
}
