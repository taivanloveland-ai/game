using System.Collections.Generic;
using UnityEngine;

// The ice boss: Taivan's ice pillar, drawn and animated by him in two sprite sheets. Assets/Resources/Art/IceBossRising
// is the pillar rising out of the ground, frame by frame; Assets/Resources/Art/IceBossBall is the ball on its top
// growing from nothing and cracking, frame by frame, and then the ten shards it breaks into.
//
// A little way into either pole's ice cap, while snow is falling, the ice cracks open a little way to one side of the
// player and the pillar rises out of it, the view shaking as it comes up, seven times as tall as the player. It never
// comes up when it is not snowing. Then the ball grows on its top and cracks, and breaks into its ten shards, which
// spread out into a circle round where the ball was and shoot one at a time at where the player is, two a second.
// Each that hits takes 4 to 8 health. A shadow on the ground right under each shard shows where it is. Once they are
// all gone the ball grows again. Nothing is thrown from the pillar itself.
//
// It is beaten with 100 hits of the ice axe, from close by. It shatters into 20 ice shards and leaves a magical orb
// (Taivan's picture, Assets/Resources/Art/MagicalOrb). Once beaten, it does not come back to that ice cap while the
// game is being played. It only acts in the top-down view, and not in the caves.
public class IceBoss : MonoBehaviour
{
    // It is this many times as tall as the player, the ball on its top included.
    // (It was 4 times as tall until 2026-10-14, when Taivan asked for it much larger.)
    const float TimesTaller = 7f;
    // It rises this many squares to one side of the player, level with them, so all of it is on the screen.
    const float RisesBeside = 6.5f;
    // Before it rises, the ice cracks open where it will come up, spreading out over this many seconds; and the
    // cracks fade away over this many seconds once it has risen. The view shakes this hard while it all happens.
    const float CrackSeconds = 1.4f, CracksFadeSeconds = 3f, ShakeHard = 0.06f;
    // The cracks are drawn in this many steps as they spread.
    const int CrackSteps = 10;
    // Its rising animation is played at this many frames a second.
    const float RiseFramesPerSecond = 24f;
    // The ball sheet: each frame is this many pixels square; the last this many frames are the shards, and the ones
    // before are the ball growing and cracking, played over this many seconds.
    const int BallFrame = 32, ShardCount = 10;
    const float BallSeconds = 5f;
    // When the ball breaks its shards spread out into a circle this wide (from its middle to each shard) round
    // where the ball was, over this many seconds. The whole circle hangs there this long, and then they shoot at the
    // player one after another, going round the circle, this many seconds apart: two a second.
    const float ShardsSpread = 1.6f, SpreadSeconds = 0.3f, CircleHolds = 0.6f, ShootEvery = 0.5f;
    // How much a hit takes, how fast the shards fly (squares a second), and how far past the player they carry on
    // before they melt away.
    // (Each shard took 5 until 2026-10-16.)
    const int LeastDamage = 4, MostDamage = 8;
    const float PieceSpeed = 7f, FliesPast = 10f;
    // A shard hits the player if it comes this close to where they stand, while they are near the ground. It flies
    // down to this height above the ground, about the middle of the player.
    const float HitsWithin = 0.45f, PieceEndsAt = 0.45f;
    // How many hits of the ice axe it takes, how close the player must be, and how often they can swing.
    // (50 until 2026-10-16, and 30 before that.)
    const int Hits = 100;
    const float AxeReach = 4.5f, SwingSeconds = 0.45f;
    // If the player goes further than this away, it sinks back into the ice until they come again.
    const float LeavesBeyond = 70f;
    // It rises whenever the player is this many squares in from the edge of an ice cap while snow is falling on them
    // (from a snow cloud or on a snow day) at least this thickly, and never when it is not snowing.
    // (Until 2026-10-16 it rose 3 times in 4, snow or not.)
    const int RisesInside = 15;
    const float RisesInSnow = 0.05f;
    const int Order = 60;
    // When it is beaten it drops this many ice shards, scattered within this many squares of its foot, and the orb.
    const int ShardsDropped = 20;
    const float ShardsScatter = 1.6f;

    class Piece
    {
        public Transform At;
        public SpriteRenderer Picture, Shadow;
        // Where it is over the ground, which way it flies, how high it is drawn above the ground, and how fast it
        // comes down; how far it has flown and has to fly; and, while it still hangs by the pillar, where it hangs,
        // its turn to shoot, and whether it has been shot yet.
        public Vector2 Ground, Heading, Hangs, ShotFrom;
        public float Lift, Drop, Flown, Reach;
        public int Turn;
        public bool Shot;
    }

    PlanetOne planet;
    PlayerController player;
    MiningMode mining;
    Health health;
    Inventory inventory;
    CraftingNook nook;
    Hud hud;
    Camera cam;
    Weather weather;
    Pickups pickups;
    Gatherables gatherables;

    // The magical orb it leaves when beaten.
    public Item OrbItem { get; private set; }

    // His frames: the pillar rising (the last one is the pillar all the way up), the ball, and the shards.
    Sprite[] risingFrames, ballFrames, shardSprites;
    // Where the middle of the ball is, from the foot of the pillar.
    Vector2 ballOffset;
    SpriteRenderer ballPicture;
    // When the ball started growing this time, and when it last broke.
    float ballStarted, brokeAt;
    Sprite dot, oval;
    float pixelsPerUnit;
    Sprite[] crackSprites;
    SpriteRenderer cracks;
    float nextChunkAt;

    GameObject body;
    SpriteRenderer picture;
    Transform barBack, barFill;
    readonly List<Piece> flying = new List<Piece>();
    readonly List<(Transform at, Vector2 velocity, float born)> bits = new List<(Transform, Vector2, float)>();
    // Where it stands, as positions in the game go; whether it is up, since when, and its health.
    Vector2 foot;
    bool up;
    float roseAt, nextSwingAt, hurtUntil;
    int hitsLeft;
    bool upNorth;
    bool beatenNorth, beatenSouth;

    public void Setup(PlanetOne planet, PlayerController player, MiningMode mining, Health health, Inventory inventory, CraftingNook nook, Hud hud, Camera cam, Sprite square, Weather weather, Pickups pickups, Gatherables gatherables)
    {
        this.weather = weather;
        this.pickups = pickups;
        this.gatherables = gatherables;
        OrbItem = MakeOrb();
        this.planet = planet;
        this.player = player;
        this.mining = mining;
        this.health = health;
        this.inventory = inventory;
        this.nook = nook;
        this.hud = hud;
        this.cam = cam;
        dot = square;
        crackSprites = MakeCracks();
        // A soft oval for the shadows.
        oval = PlanetOne.PaintSprite(32, 16, new Vector2(0.5f, 0.5f), 32f, (x, y) =>
        {
            float dx = (x + 0.5f - 16f) / 16f, dy = (y + 0.5f - 8f) / 8f;
            return dx * dx + dy * dy <= 1f ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
        });
        if (!CutSheets()) return;

        // It stands in the way: nobody walks through it.
        System.Func<int, int, bool> before = planet.Obstacle;
        planet.Obstacle = (x, y) => (before != null && before(x, y)) || Blocks(x, y);
    }

    // Cuts his two sheets into their frames, without changing any of their pixels.
    bool CutSheets()
    {
        var rising = Resources.Load<Texture2D>("Art/IceBossRising");
        var balls = Resources.Load<Texture2D>("Art/IceBossBall");
        if (rising == null || balls == null) return false;
        Color32[] risingPixels = PlanetOne.ReadPixels(rising), ballPixels = PlanetOne.ReadPixels(balls);
        int frameWide = rising.height / 2, frameTall = rising.height;
        if (rising.width % frameWide != 0) Debug.LogWarning("Ice boss: its rising sheet is " + rising.width + " wide, not a whole number of frames");
        int risingCount = rising.width / frameWide;
        int ballCount = balls.width / BallFrame - ShardCount;

        // The pillar all the way up is the last frame. The ball sits on its flat top, the highest row (under its
        // little point) that is more than a quarter of the frame across.
        int last = risingCount - 1, flatTop = 0;
        for (int y = 0; y < frameTall; y++)
        {
            int across = 0;
            for (int x = 0; x < frameWide; x++)
            {
                if (risingPixels[y * rising.width + last * frameWide + x].a > 0) across++;
            }
            if (across > frameWide / 4) flatTop = y;
        }
        // It is drawn TimesTaller times as tall as the player, from its foot to the top of the ball.
        pixelsPerUnit = (flatTop + 1 + BallFrame) / (PlayerController.Height * TimesTaller);
        ballOffset = new Vector2(0f, flatTop + 1 + BallFrame / 2f - 1f) / pixelsPerUnit;

        risingFrames = new Sprite[risingCount];
        for (int i = 0; i < risingCount; i++) risingFrames[i] = Cut(risingPixels, rising.width, new RectInt(i * frameWide, 0, frameWide, frameTall), new Vector2(0.5f, 0f));
        ballFrames = new Sprite[ballCount];
        for (int i = 0; i < ballCount; i++) ballFrames[i] = Cut(ballPixels, balls.width, new RectInt(i * BallFrame, 0, BallFrame, BallFrame), new Vector2(0.5f, 0.5f));
        shardSprites = new Sprite[ShardCount];
        for (int i = 0; i < ShardCount; i++) shardSprites[i] = Cut(ballPixels, balls.width, new RectInt((ballCount + i) * BallFrame, 0, BallFrame, BallFrame), new Vector2(0.5f, 0.5f));
        Debug.Log("Ice boss: " + risingCount + " rising frames of " + frameWide + " x " + frameTall + ", " + ballCount + " ball frames and " + ShardCount + " shards");
        return true;
    }

    // The magical orb, from Taivan's picture: one block across, drawn sharp, sitting on the ground at its lowest row.
    static Item MakeOrb()
    {
        var picture = Resources.Load<Texture2D>("Art/MagicalOrb");
        if (picture == null) return null;
        Color32[] pixels = PlanetOne.ReadPixels(picture);
        var texture = new Texture2D(picture.width, picture.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply();
        int lowest = 0;
        while (lowest < picture.height - 1 && System.Array.FindIndex(pixels, lowest * picture.width, picture.width, c => c.a > 0) < 0) lowest++;
        var sprite = Sprite.Create(texture, new Rect(0, 0, picture.width, picture.height), new Vector2(0.5f, lowest / (float)picture.height), picture.height);
        return new Item("Magical orb", sprite);
    }

    // One frame cut out of a sheet, as its own sharp picture.
    Sprite Cut(Color32[] pixels, int sheetWide, RectInt box, Vector2 pivot)
    {
        var cut = new Color32[box.width * box.height];
        for (int y = 0; y < box.height; y++)
        {
            for (int x = 0; x < box.width; x++) cut[y * box.width + x] = pixels[(box.y + y) * sheetWide + box.x + x];
        }
        var texture = new Texture2D(box.width, box.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(cut);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, box.width, box.height), pivot, pixelsPerUnit);
    }

    // True for the squares its base stands on, so they cannot be walked into.
    bool Blocks(int x, int y)
    {
        if (!up) return false;
        Vector2Int at = planet.Wrap(Vector2Int.FloorToInt(foot));
        return y == at.y && Mathf.Abs(x - at.x) <= 2;
    }

    void Update()
    {
        if (risingFrames == null) return;
        bool topDown = !mining.Active && !planet.InCave;
        if (!up)
        {
            if (topDown) TryToRise();
            return;
        }

        // Too far away, or gone into a cave: it sinks back into the ice.
        Vector2 at = player.GroundPosition;
        Vector2 near = planet.Nearest(foot, at);
        if (planet.InCave || Vector2.Distance(near, at) > LeavesBeyond)
        {
            Sink();
            return;
        }
        foot = near;
        body.SetActive(topDown);
        Place();
        MoveBits();
        if (!topDown)
        {
            ClearPieces();
            return;
        }

        if (Risen >= 1f)
        {
            if (flying.Count == 0 && Time.time >= ballStarted + BallSeconds) Break();
            MovePieces();
            TakeSwings();
        }
    }

    // Coming far enough into an ice cap that has not beaten it yet, while it is snowing, brings it up out of the ice
    // beside the player.
    void TryToRise()
    {
        Vector2 at = player.GroundPosition;
        if (planet.IntoIceCap(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y), out bool north) < RisesInside) return;
        if (weather.Snowing < RisesInSnow || (north ? beatenNorth : beatenSouth)) return;
        upNorth = north;
        // Beside the player, east or west, level with them, so all of its height is on the screen.
        foot = at + new Vector2(Random.value < 0.5f ? -RisesBeside : RisesBeside, -0.5f);
        up = true;
        roseAt = Time.time;
        ballStarted = Time.time + CrackSeconds + RiseSeconds;
        hitsLeft = Hits;
        Build();
        hud.Say("Something rises out of the ice!", 3f);
    }

    void Build()
    {
        body = new GameObject("Ice Boss");
        body.transform.SetParent(planet.TopDownRoot, false);
        picture = body.AddComponent<SpriteRenderer>();
        // All its parts are put in order as one thing, at its foot.
        body.AddComponent<UnityEngine.Rendering.SortingGroup>();
        var ballObject = new GameObject("Ball");
        ballObject.transform.SetParent(body.transform, false);
        ballObject.transform.localPosition = ballOffset;
        ballPicture = ballObject.AddComponent<SpriteRenderer>();
        ballPicture.sortingOrder = 1;
        // Its health, as a bar over its head.
        barBack = MakeBar("Health Bar", new Color(0.08f, 0.09f, 0.12f, 0.85f), Order + 2);
        barFill = MakeBar("Health", new Color(0.84f, 0.2f, 0.22f, 1f), Order + 3);
        // The cracks in the ice where it comes up, drawn on the ice round its foot.
        var crackObject = new GameObject("Cracks");
        crackObject.transform.SetParent(body.transform, false);
        crackObject.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        // The cracks spread as far as it is big.
        crackObject.transform.localScale = new Vector3(TimesTaller / 4f, TimesTaller / 4f, 1f);
        cracks = crackObject.AddComponent<SpriteRenderer>();
        cracks.sortingOrder = -2;
        // A shadow round its foot, drawn on the ice just behind it.
        var shadow = new GameObject("Shadow");
        shadow.transform.SetParent(body.transform, false);
        shadow.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        shadow.transform.localScale = new Vector3(Pillar.bounds.size.x * 0.95f, 0.7f, 1f);
        var shade = shadow.AddComponent<SpriteRenderer>();
        shade.sprite = oval;
        shade.color = new Color(0.05f, 0.1f, 0.2f, 0.3f);
        shade.sortingOrder = -1;
    }

    // The pillar all the way up.
    Sprite Pillar => risingFrames[risingFrames.Length - 1];
    // How long its rising animation takes.
    float RiseSeconds => risingFrames.Length / RiseFramesPerSecond;

    Transform MakeBar(string name, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(body.transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = dot;
        renderer.color = color;
        renderer.sortingOrder = order;
        return go.transform;
    }

    // Puts it where it stands, rising out of the ice to begin with, with its health bar over it.
    void Place()
    {
        float risen = Risen;
        // The health bar is over the top of the circle the shards hang in.
        float height = ballOffset.y + ShardsSpread + 0.7f;
        body.transform.position = new Vector3(foot.x, foot.y + planet.TopAt(Mathf.FloorToInt(foot.x), Mathf.FloorToInt(foot.y)) * PlanetOne.HeightScale, 0f);
        ShowRising(risen);
        picture.color = Time.time < hurtUntil ? new Color(1f, 0.55f, 0.55f) : Color.white;
        ballPicture.color = picture.color;
        float barWide = 3.6f, filled = barWide * hitsLeft / Hits;
        barBack.localPosition = new Vector3(0f, height + 0.35f, 0f);
        barBack.localScale = new Vector3(barWide + 0.08f, 0.2f, 1f);
        barFill.localPosition = new Vector3(-(barWide - filled) / 2f, height + 0.35f, 0f);
        barFill.localScale = new Vector3(filled, 0.12f, 1f);
        barBack.gameObject.SetActive(risen >= 1f);
        barFill.gameObject.SetActive(risen >= 1f);
    }

    // How far it has risen out of the ice: 0 while the ice is still cracking and when it starts to come up, 1 once it
    // is all the way out.
    float Risen => Mathf.Clamp01((Time.time - roseAt - CrackSeconds) / RiseSeconds);

    // The ice cracks open, then his rising animation plays, with chunks of ice thrown up round it and the view
    // shaking. Then the cracks slowly fade.
    void ShowRising(float risen)
    {
        float since = Time.time - roseAt;
        float spread = Mathf.Clamp01(since / CrackSeconds);
        cracks.sprite = crackSprites[Mathf.Min(CrackSteps - 1, Mathf.FloorToInt(spread * CrackSteps))];
        float fade = 1f - Mathf.Clamp01((since - CrackSeconds - RiseSeconds) / CracksFadeSeconds);
        cracks.enabled = fade > 0f;
        cracks.color = new Color(1f, 1f, 1f, fade);

        bool rising = risen < 1f;
        CameraFollow.Shake = rising && body.activeSelf ? ShakeHard * (since < CrackSeconds ? 0.5f : 1f) : 0f;
        if (!rising)
        {
            picture.sprite = Pillar;
            ShowBall();
            return;
        }
        ballPicture.enabled = false;
        picture.sprite = since < CrackSeconds ? null : risingFrames[Mathf.Min(risingFrames.Length - 1, Mathf.FloorToInt(risen * risingFrames.Length))];
        if (risen > 0f && Time.time >= nextChunkAt)
        {
            nextChunkAt = Time.time + 0.06f;
            ThrowChunk();
        }
    }

    // His ball frames, growing and cracking, on the top of the pillar. Once it has broken it is not there until it
    // starts to grow again.
    void ShowBall()
    {
        float since = Time.time - ballStarted;
        bool there = flying.Count == 0 && since >= 0f;
        ballPicture.enabled = there;
        if (!there) return;
        ballPicture.sprite = ballFrames[Mathf.Min(ballFrames.Length - 1, Mathf.FloorToInt(since / BallSeconds * ballFrames.Length))];
    }

    // A chunk of ice thrown up from round its foot as it breaks out.
    void ThrowChunk()
    {
        SpriteRenderer chunk = MakeFlyingThing("Ice Chunk", dot, Order + 1);
        float size = Random.Range(0.1f, 0.22f);
        chunk.transform.localScale = new Vector3(size, size, 1f);
        chunk.color = Random.value < 0.5f ? new Color32(206, 235, 248, 255) : new Color32(146, 202, 232, 255);
        float side = Random.Range(-1f, 1f);
        chunk.transform.position = body.transform.position + new Vector3(side * Pillar.bounds.size.x * 0.5f, 0.1f, 0f);
        bits.Add((chunk.transform, new Vector2(side * 2.2f + Random.Range(-0.6f, 0.6f), Random.Range(2.5f, 4.5f)), Time.time));
    }

    // The cracks, drawn in steps as they spread: jagged lines running out from the middle across the ice, dark
    // blue with a pale edge. Drawn flatter than they are wide, since the ice is seen at a slant.
    Sprite[] MakeCracks()
    {
        const int wide = 112, tall = 56, perUnit = 24;
        var rand = new System.Random(7);
        // Each crack is a line of points from the middle outward, with a few branching off part way.
        var lines = new List<List<Vector2>>();
        for (int i = 0; i < 9; i++)
        {
            float angle = i * Mathf.PI * 2f / 9f + (float)rand.NextDouble() * 0.5f;
            var line = new List<Vector2> { Vector2.zero };
            Vector2 at = Vector2.zero;
            int length = 6 + rand.Next(5);
            for (int step = 0; step < length; step++)
            {
                angle += ((float)rand.NextDouble() - 0.5f) * 0.9f;
                at += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.11f;
                line.Add(at);
                if (step == length / 2 && rand.NextDouble() < 0.6)
                {
                    var branch = new List<Vector2> { at };
                    Vector2 b = at;
                    float turn = angle + (rand.NextDouble() < 0.5 ? 0.9f : -0.9f);
                    for (int k = 0; k < 3; k++)
                    {
                        b += new Vector2(Mathf.Cos(turn), Mathf.Sin(turn)) * 0.09f;
                        branch.Add(b);
                    }
                    lines.Add(branch);
                }
            }
            lines.Add(line);
        }
        var sprites = new Sprite[CrackSteps];
        for (int stage = 0; stage < CrackSteps; stage++)
        {
            // How far out from the middle the cracks have reached, as a share of the whole.
            float reach = (stage + 1f) / CrackSteps;
            sprites[stage] = PlanetOne.PaintSprite(wide, tall, new Vector2(0.5f, 0.5f), perUnit, (x, y) =>
            {
                // From the middle, in shares of the half-width, with the height stretched back out.
                var p = new Vector2((x + 0.5f - wide / 2f) / (wide / 2f), (tall / 2f - y - 0.5f) / (wide / 2f) * 2f);
                float nearest = float.MaxValue;
                foreach (List<Vector2> line in lines)
                {
                    for (int k = 1; k < line.Count; k++)
                    {
                        if (line[k].magnitude > reach) break;
                        nearest = Mathf.Min(nearest, DistanceToSegment(p, line[k - 1], line[k]));
                    }
                }
                float pixel = 1f / (wide / 2f);
                if (nearest < pixel * 1.1f) return new Color32(52, 104, 150, 255);
                if (nearest < pixel * 2.1f) return new Color32(226, 244, 252, 255);
                return new Color32(0, 0, 0, 0);
            });
        }
        return sprites;
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 along = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, along) / Mathf.Max(0.0001f, along.sqrMagnitude));
        return Vector2.Distance(p, a + along * t);
    }

    // The ball breaks into its shards, which spread out round where it was and hang there, each with its shadow on
    // the ground under it, until their turn to shoot.
    void Break()
    {
        ballPicture.enabled = false;
        brokeAt = Time.time;
        for (int i = 0; i < shardSprites.Length; i++)
        {
            var piece = new Piece();
            SpriteRenderer shape = MakeFlyingThing("Ice Shard", shardSprites[i], Order);
            piece.At = shape.transform;
            piece.Picture = shape;
            // Its shadow, a dark oval on the ground right under it.
            piece.Shadow = MakeFlyingThing("Ice Shard Shadow", oval, 0);
            piece.Shadow.color = new Color(0.05f, 0.1f, 0.2f, 0.35f);
            float shadowWide = Mathf.Max(0.3f, shardSprites[i].bounds.size.x * 0.8f);
            piece.Shadow.transform.localScale = new Vector3(shadowWide, shadowWide * 0.7f, 1f);
            float angle = i * Mathf.PI * 2f / shardSprites.Length;
            piece.Hangs = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ShardsSpread;
            piece.Turn = i;
            flying.Add(piece);
        }
        MovePieces();
    }

    SpriteRenderer MakeFlyingThing(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(planet.TopDownRoot, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    void MovePieces()
    {
        Vector2 at = player.GroundPosition;
        for (int i = flying.Count - 1; i >= 0; i--)
        {
            Piece piece = flying[i];
            Vector2 was = piece.Ground;
            if (!piece.Shot)
            {
                // Still hanging by the pillar: spreading out from where the ball was, until its turn comes, one every
                // half second. Then it shoots at where the player is right then.
                float spread = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - brokeAt) / SpreadSeconds));
                piece.Ground = foot + new Vector2(piece.Hangs.x * spread, -0.05f);
                piece.Lift = ballOffset.y + piece.Hangs.y * spread;
                if (Time.time >= brokeAt + CircleHolds + piece.Turn * ShootEvery)
                {
                    piece.Shot = true;
                    piece.ShotFrom = piece.Ground;
                    Vector2 toward = at - piece.Ground;
                    float distance = Mathf.Max(0.5f, toward.magnitude);
                    piece.Heading = toward / distance;
                    piece.Drop = (piece.Lift - PieceEndsAt) / (distance / PieceSpeed);
                    piece.Reach = distance + FliesPast;
                }
            }
            else
            {
                float step = PieceSpeed * Time.deltaTime;
                piece.Flown += step;
                // Worked out afresh from where it was shot, not added to bit by bit, so it cannot drift off its line.
                piece.Ground = piece.ShotFrom + piece.Heading * piece.Flown;
                piece.Lift = Mathf.Max(PieceEndsAt, piece.Lift - piece.Drop * Time.deltaTime);
            }
            // Drawn as high above its shadow as it is above the ground.
            piece.At.position = new Vector3(piece.Ground.x, piece.Ground.y + piece.Lift, 0f);
            piece.Shadow.transform.position = new Vector3(piece.Ground.x, piece.Ground.y, 0f);
            if (!piece.Shot) continue;

            // Anywhere along the way it came this frame, so a slow frame cannot carry it past the player unhurt.
            bool hits = DistanceToSegment(at, was, piece.Ground) < HitsWithin && player.FeetHeight < PieceEndsAt + 0.6f;
            if (hits) health.Hurt(Random.Range(LeastDamage, MostDamage + 1));
            if (hits || piece.Flown > piece.Reach)
            {
                Destroy(piece.At.gameObject);
                Destroy(piece.Shadow.gameObject);
                flying.RemoveAt(i);
                // The last one is gone: the ball starts to grow again.
                if (flying.Count == 0) ballStarted = Time.time;
            }
        }
    }

    // The chunks of ice thrown up as it rises fly out, fall and fade.
    void MoveBits()
    {
        for (int i = bits.Count - 1; i >= 0; i--)
        {
            (Transform at, Vector2 velocity, float born) = bits[i];
            float age = Time.time - born;
            if (age > 0.7f)
            {
                Destroy(at.gameObject);
                bits.RemoveAt(i);
                continue;
            }
            velocity.y -= 9f * Time.deltaTime;
            bits[i] = (at, velocity, born);
            at.position += (Vector3)(velocity * Time.deltaTime);
            at.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 1f - age / 0.7f);
        }
    }

    void ClearPieces()
    {
        bool any = flying.Count > 0;
        foreach (Piece piece in flying)
        {
            Destroy(piece.At.gameObject);
            Destroy(piece.Shadow.gameObject);
        }
        flying.Clear();
        if (any) ballStarted = Time.time;
    }

    // The ice axe: a click on it, from close enough, is one hit. Holding the button keeps swinging.
    void TakeSwings()
    {
        if (Time.time < nextSwingAt || inventory.Equipped != nook.Axe || health.Down) return;
        if (!GameInput.LeftClickPressed() && !GameInput.LeftClickHeld()) return;
        Vector2 point = cam.ScreenToWorldPoint(GameInput.PointerPosition());
        if (!picture.bounds.Contains(new Vector3(point.x, point.y, picture.bounds.center.z))) return;
        nextSwingAt = Time.time + SwingSeconds;
        if (Vector2.Distance(player.GroundPosition, foot) > AxeReach)
        {
            hud.Say("Get closer to hit it", 1.5f);
            return;
        }
        Hit();
    }

    // One hit with the ice axe. The last one shatters it.
    public void Hit()
    {
        if (!up) return;
        hitsLeft--;
        hurtUntil = Time.time + 0.12f;
        if (hitsLeft > 0) return;
        if (upNorth) beatenNorth = true;
        else beatenSouth = true;
        hud.Say("It shatters!", 3f);
        // Its ice shards scatter round its foot, and the orb is left where it stood.
        for (int i = 0; i < ShardsDropped && gatherables.ShardItem != null; i++) pickups.Drop(gatherables.ShardItem, foot + Random.insideUnitCircle * ShardsScatter);
        if (OrbItem != null) pickups.Drop(OrbItem, foot);
        Sink();
    }

    // Takes it away: beaten, or left behind.
    void Sink()
    {
        CameraFollow.Shake = 0f;
        ClearPieces();
        up = false;
        if (body != null) Destroy(body);
    }

    // For checking: whether it is up, where it stands, how many hits it has left, and how many shards are out.
    public bool Up => up;
    public Vector2 Foot => foot;
    public int HitsLeft => hitsLeft;
    public int Flying => flying.Count;
    public int PieceCount => shardSprites != null ? shardSprites.Length : 0;
}
