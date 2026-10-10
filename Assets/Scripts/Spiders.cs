using System.Collections.Generic;
using UnityEngine;

// Ice spiders: pale creatures about a block wide that live on the ice. They wander until the player comes near, then
// scuttle after them and bite when they catch up. A bite does nothing yet, since the player has no health.
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
    const float BiteDistance = 0.85f, BiteSeconds = 0.5f;
    // Seen from above, it bites when its middle is this far from the edge of the player, which puts its fangs
    // just short of them. The players picture starts this far below the spot they stand on.
    const float TopBiteGap = 0.4f, FeetBelow = 0.2f;
    // How far a spider walks in one full cycle of its legs.
    const float StrideDistance = 0.55f;

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
        public Vector2 Heading = Vector2.up;
        // How far through a cycle of its legs it is, from 0 to 1.
        public float Gait;
        // How far through a bite it is in seconds, or less than 0 when it is not biting.
        public float Bite = -1f;
        public bool Chasing;
        public Vector2 WanderDirection;
        public float DecideAt;
        public SpriteRenderer Renderer;
    }

    PlanetOne planet;
    PlayerController player;
    MiningMode mining;
    DayNight dayNight;
    readonly List<Spider> spiders = new List<Spider>();
    Sprite[] topWalk, topBite, sideWalk, sideBite;
    System.Random rand;

    public int Count => spiders.Count;

    public void Setup(PlanetOne planet, PlayerController player, MiningMode mining, DayNight dayNight)
    {
        this.planet = planet;
        this.player = player;
        this.mining = mining;
        this.dayNight = dayNight;

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
        var start = new Vector2Int(planet.width / 2, planet.height / 2 - 1);
        for (int y = 0; y < planet.height; y++)
        {
            for (int x = 0; x < planet.width; x++)
            {
                if (rand.NextDouble() >= Density) continue;
                if (Mathf.Abs(x - start.x) <= ClearOfStart && Mathf.Abs(y - start.y) <= ClearOfStart) continue;
                if (!CanStand(new Vector2Int(x, y), planet.TopAt(x, y))) continue;
                spiders.Add(new Spider { Position = new Vector2(x + 0.5f, y + 0.5f), Heading = RandomDirection() });
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
        Vector2 playerAt = player.GroundPosition;
        float playerFeet = player.FeetHeight;
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
            spider.Chasing = sameHeight && distance < (spider.Chasing ? GiveUpRadius : NoticeRadius);

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
                spider.Bite = spider.Bite < 0f ? 0f : (spider.Bite + Time.deltaTime) % (BiteSeconds * 1.6f);
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
            spider.Position = new Vector2(Mathf.Repeat(at.x, planet.width), Mathf.Repeat(at.y, planet.height));
            Draw(spider, at);
        }
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
            var go = new GameObject("Ice Spider");
            go.transform.SetParent(transform, false);
            spider.Renderer = go.AddComponent<SpriteRenderer>();
            spider.Renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        }
        SpriteRenderer renderer = spider.Renderer;
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
            renderer.transform.SetPositionAndRotation(new Vector3(mining.Along(at), level, 0f), Quaternion.identity);
            renderer.sortingOrder = MiningMode.ObjectSortingOrder;
            renderer.color = dayNight.WorldTint;
            return;
        }

        // From above, the picture is turned to face the way the spider is heading.
        renderer.enabled = true;
        renderer.sprite = biting ? topBite[frame] : topWalk[frame];
        renderer.flipX = false;
        float turn = Mathf.Atan2(spider.Heading.y, spider.Heading.x) * Mathf.Rad2Deg - 90f;
        renderer.transform.SetPositionAndRotation(new Vector3(at.x, at.y + level * PlanetOne.HeightScale, 0f), Quaternion.Euler(0f, 0f, turn));
        renderer.sortingOrder = 0;
        renderer.color = Color.white;
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
