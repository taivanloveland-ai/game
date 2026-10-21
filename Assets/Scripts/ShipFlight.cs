using UnityEngine;

// Sitting in the fixed ship and flying it (Taivan's request of 2026-10-18). Standing next to the fixed ship, press E
// to get in (right-clicking the ship opens the workbench, since 2026-10-19); E or a right click gets out again
// (and landing, the player gets out by themselves). Inside, the usual view is from behind the ship (his picture of
// its back, Art/ShipBack, the second picture of his ship sheet of 2026-10-19), R switches to the view from the
// player's eyes, looking straight out, and F shows or hides the ship's controls.
// Left click launches: the ship rises, the ground falls away and the sky turns to space. In space the view is from
// behind the ship flying forward, with the stars far behind it all, the sun and the black hole, and the ship pushing itself
// along with ice shards blown out of its three round thrusters by the magical orbs. W A S D steer it to the next
// planet, which grows as it comes nearer; reaching it, the ship lands there.
public class ShipFlight : MonoBehaviour
{
    // The player gets in from this close to the ship's foot.
    const float GetInWithin = 4.5f;
    // How much of the purple crystal shows, seen from outside.
    const float GlassFromOutside = 0.75f;
    // The views are drawn far from the planet, where nothing else is.
    static readonly Vector2 ViewPlace = new Vector2(-40000f, -40000f);
    // The view from behind: his picture of the ship's back fills about this much of the screen's height, and stands
    // this far up the screen (0 the bottom, 1 the top), on ground up to the horizon this far up. In space it flies
    // a little higher.
    const float BackFills = 0.3f, BackWide = 0.5f, BackStandsAt = 0.18f, HorizonAt = 0.42f, BackFliesAt = 0.26f;
    static readonly Color DaySkyTop = new Color32(0x4E, 0x86, 0xC8, 255), DaySkyLow = new Color32(0xB8, 0xD6, 0xEE, 255);
    static readonly Color NightSkyTop = new Color32(0x08, 0x0C, 0x24, 255), NightSkyLow = new Color32(0x24, 0x30, 0x5C, 255);
    const int SkyBands = 8;

    // Taking off lasts this many seconds.
    const float RiseSeconds = 4f;
    // In space: how fast the ship flies (in the trip's own distances a second), how fast W A S D turn it (degrees a
    // second), how far the next planet is, how big the planets are, and how close to its ground the ship lands.
    const float FlySpeed = 7f, TurnSpeed = 50f, TripLength = 230f, PlanetRadius = 14f, LandsWithin = 3f;
    // The next planet starts this far off to the side and up, so it has to be steered to.
    const float StartsAside = 22f, StartsAbove = 7f;
    // How many stars are scattered all round the sky (about a sixth of them on the screen at once).
    const int StarCount = 1800;
    float[] starBright;
    // Arriving: the screen fills with the planet's colour over this many seconds.
    const float ArriveSeconds = 1.6f;
    // His round thrusters on the back of the ship, in his pixels from its foot: where the ice shards blow from. Found
    // in his picture: each patch of the pale blue glass.
    Vector2[] Thrusters = new Vector2[0];
    static readonly Color32[] ThrusterGlass =
    {
        new Color32(0xCE, 0xEB, 0xF8, 255), new Color32(0x92, 0xCA, 0xE8, 255), new Color32(0x70, 0xAE, 0xD4, 255), new Color32(0xE4, 0xF4, 0xFB, 255),
        new Color32(0xC4, 0xE2, 0xF2, 255), new Color32(0x68, 0xB5, 0xDF, 255), new Color32(0xD5, 0xEA, 0xF6, 255),
    };
    // The ice pillar's ten shards (his pictures), blown out as the exhaust. Set before Setup.
    public Sprite[] ExhaustShards;
    // Steering, the ship leans: this far over at most (degrees) as it turns, and this many of his pixels up or down
    // as it climbs or dives, easing into it this fast (a share a second).
    const float LeanMost = 12f, PitchMost = 5f, LeanEase = 5f;
    float lean, pitch;
    // The size of the drawing in his picture of the ship's back (without the clear space round it), and how many of
    // the screen's pixels each of his is drawn.
    Vector2 backSize = Vector2.one;
    int viewScale = 1;
    // Ice shards blown out: this many a second, each lasting this long and flying this fast (his pixels a second).
    const float ShardsEachSecond = 30f, ShardSeconds = 0.7f, ShardSpeedLow = 45f, ShardSpeedHigh = 90f;
    static readonly Color32[] ShardColors =
    {
        new Color32(0xC4, 0xE2, 0xF2, 255), new Color32(0xD5, 0xEA, 0xF6, 255), new Color32(0x92, 0xCA, 0xE8, 255),
        new Color32(0xFF, 0xFF, 0xFF, 255), new Color32(0xD7, 0xB4, 0xEE, 255),
    };

    enum Stage { Landed, Rising, InSpace, Arriving }

    PlanetOne planet;
    PlayerController player;
    MiningMode mining;
    CraftingNook nook;
    CrashedShip ship;
    Hud hud;
    Camera cam;
    DayNight dayNight;

    // Whether the player sits in the ship, is looking from inside it, and has the controls showing.
    public bool Aboard { get; private set; }
    public bool LookingFromInside { get; private set; }
    public bool ControlsShown { get; private set; }
    Stage stage;
    float stageSince;
    public bool InSpace => Aboard && stage != Stage.Landed;

    // Called on reaching the next planet. Without it the ship comes back down where it took off.
    public System.Action Arrived;
    // The planet being flown to, and the one left behind, as they are named on screen and coloured. (Planet 1 is called
    // the frost planet on screen since 2026-10-20.)
    public string NextPlanetName = "the rocky planet", HomeName = "the frost planet";
    public System.Func<int, int, int, Color32> PaintNext = PaintRocky, PaintHome = PaintIcy;
    public Color NextColor = new Color32(197, 157, 109, 255), HomeColor = new Color32(0xE4, 0xEE, 0xF4, 255);
    // The planet just left can be flown back to (Taivan's request of 2026-10-20): it starts this far behind the ship,
    // and reaching it the ship lands back where it took off. Whether that is where the ship is landing now.
    const float HomeBehind = 40f;
    bool goingHome;
    // The ground the ship stands on in the view from behind: its colour, its shade, and its sparkle.
    public Color32 GroundColor = new Color32(0xE4, 0xEE, 0xF4, 255), GroundShade = new Color32(0xC6, 0xD6, 0xE4, 255), GroundSpeck = new Color32(0xFF, 0xFF, 0xFF, 255);

    // The camera for the views from behind and from inside.
    Camera view;
    Transform backRoot;
    SpriteRenderer back, ground, horizon, backShadow;
    SpriteRenderer[] sky;
    // Specks on the ground, and where each is: x from -1 to 1 across the screen, y from the horizon (0) to the bottom (1).
    SpriteRenderer[] specks;
    Vector2[] speckSpots;
    GUIStyle titleStyle, lineStyle, hintStyle, markerStyle;

    // Space: where the ship is and which way it faces, the planets, the stars, the sun and the black hole.
    Vector3 shipAt;
    Quaternion facing = Quaternion.identity;
    Vector3 nextAt, homeAt;
    SpriteRenderer nextPlanet, homePlanet, sun, blackHole;
    static readonly Vector3 SunWay = Quaternion.Euler(-12f, -42f, 0f) * Vector3.forward;
    static readonly Vector3 BlackHoleWay = Quaternion.Euler(-24f, 34f, 0f) * Vector3.forward;
    const int PlanetPixels = 128;
    // The planets in space are round and turn (Taivan's request of 2026-10-19): their lands, the same as on the title
    // screen, wrapped onto a ball that spins once in this many seconds, lit from the sun, with a night side and a rim
    // of air.
    const float PlanetSpinSeconds = 50f;
    Ball nextBall, homeBall;

    class Ball
    {
        public Sprite Sprite;
        readonly Texture2D texture;
        readonly Color32[] land, night, pixels;
        readonly Color32 air;
        readonly int[] spots, rows;
        readonly float[] round;
        readonly Vector3[] normals;
        float spin;

        public Ball(bool rocky)
        {
            land = TitleScreen.LandMap(rocky);
            night = new Color32[land.Length];
            // Its night side is dim, but not so dark it cannot be seen against space.
            for (int i = 0; i < land.Length; i++) night[i] = Color.Lerp(TitleScreen.Night, land[i], 0.3f);
            air = rocky ? TitleScreen.Dust : TitleScreen.Air;
            texture = new Texture2D(PlanetPixels, PlanetPixels, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            pixels = new Color32[PlanetPixels * PlanetPixels];
            var spotList = new System.Collections.Generic.List<int>();
            var rowList = new System.Collections.Generic.List<int>();
            var roundList = new System.Collections.Generic.List<float>();
            var normalList = new System.Collections.Generic.List<Vector3>();
            float half = PlanetPixels / 2f;
            for (int y = 0; y < PlanetPixels; y++)
            {
                for (int x = 0; x < PlanetPixels; x++)
                {
                    float nx = (x + 0.5f - half) / half, ny = (y + 0.5f - half) / half, fromMiddle = nx * nx + ny * ny;
                    if (fromMiddle > 1f) continue;
                    float nz = Mathf.Sqrt(1f - fromMiddle);
                    spotList.Add(y * PlanetPixels + x);
                    rowList.Add(Mathf.Clamp((int)((Mathf.Asin(ny) * Mathf.Rad2Deg + 90f) / 180f * TitleScreen.MapTall), 0, TitleScreen.MapTall - 1));
                    roundList.Add(Mathf.Atan2(nx, nz) / (Mathf.PI * 2f) * TitleScreen.MapWide);
                    normalList.Add(new Vector3(nx, ny, nz));
                }
            }
            spots = spotList.ToArray();
            rows = rowList.ToArray();
            round = roundList.ToArray();
            normals = normalList.ToArray();
            Sprite = Sprite.Create(texture, new Rect(0, 0, PlanetPixels, PlanetPixels), new Vector2(0.5f, 0.5f), PlanetPixels);
        }

        // Turns it on a little and paints it, lit from the way to the sun (as seen by the viewer: x right, y up, z toward
        // the viewer).
        public void Paint(Vector3 toSun, float seconds)
        {
            spin += seconds / PlanetSpinSeconds;
            float turned = Mathf.Repeat(spin, 1f) * TitleScreen.MapWide;
            for (int i = 0; i < spots.Length; i++)
            {
                int column = (int)(round[i] - turned + TitleScreen.MapWide * 2) % TitleScreen.MapWide;
                int at = rows[i] * TitleScreen.MapWide + column;
                Vector3 n = normals[i];
                float sunlight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.3f, Vector3.Dot(n, toSun)));
                Color32 c = Color32.Lerp(night[at], land[at], sunlight);
                float edge = 1f - n.z * n.z;
                if (edge > 0.93f) c = Color32.Lerp(c, air, 0.4f * sunlight * Mathf.InverseLerp(0.93f, 1f, edge));
                pixels[spots[i]] = c;
            }
            texture.SetPixels32(pixels);
            texture.Apply();
        }
    }
    SpriteRenderer[] stars;
    Vector3[] starAt;
    // The ice shards, and the glow of the orbs in the thrusters.
    class Shard { public SpriteRenderer Picture; public Vector2 From, Way; public float Born; }
    readonly System.Collections.Generic.List<Shard> shards = new System.Collections.Generic.List<Shard>();
    Sprite shardSprite;
    float shardsOwed;
    SpriteRenderer[] glows;
    readonly System.Random random = new System.Random(11);

    public void Setup(PlanetOne planet, PlayerController player, MiningMode mining, CraftingNook nook, CrashedShip ship, Hud hud, Camera cam, DayNight dayNight)
    {
        this.planet = planet;
        this.player = player;
        this.mining = mining;
        this.nook = nook;
        this.ship = ship;
        this.hud = hud;
        this.cam = cam;
        this.dayNight = dayNight;

        var eye = new GameObject("Ship View Camera");
        view = eye.AddComponent<Camera>();
        view.orthographic = true;
        view.clearFlags = CameraClearFlags.SolidColor;
        view.depth = cam.depth + 1f;
        view.cullingMask = cam.cullingMask;
        // Sorted the same way as the usual view, things further down the screen in front.
        view.transparencySortMode = cam.transparencySortMode;
        view.transparencySortAxis = cam.transparencySortAxis;
        view.enabled = false;
        backRoot = new GameObject("Ship Views").transform;
        backRoot.position = ViewPlace;
        MakeBackView();
        MakeSpace();
    }

    // The view from behind the ship: sky, ground to the horizon, and the ship's back standing on it. Everything is in his
    // pixels (one unit each), drawn a whole number of the screen's pixels big.
    void MakeBackView()
    {
        Sprite white = PlanetOne.PaintSprite(1, 1, new Vector2(0.5f, 0.5f), 1f, (x, y) => new Color32(255, 255, 255, 255));
        sky = new SpriteRenderer[SkyBands];
        for (int i = 0; i < SkyBands; i++) sky[i] = Plain("Sky", white, -10);
        ground = Plain("Ground", white, -8);
        horizon = Plain("Horizon", white, -7);
        specks = new SpriteRenderer[260];
        speckSpots = new Vector2[specks.Length];
        var spread = new System.Random(7);
        for (int i = 0; i < specks.Length; i++)
        {
            specks[i] = Plain("Speck", white, -6);
            // More of them far away, near the horizon.
            speckSpots[i] = new Vector2((float)spread.NextDouble() * 2f - 1f, (float)System.Math.Pow(spread.NextDouble(), 1.6));
            specks[i].sortingOrder = spread.Next(3) == 0 ? -5 : -6;
        }
        backShadow = Plain("Ship Shadow", PlanetOne.PaintSprite(40, 8, new Vector2(0.5f, 0.5f), 1f, (x, y) =>
        {
            float dx = (x + 0.5f - 20f) / 20f, dy = (y + 0.5f - 4f) / 4f;
            return dx * dx + dy * dy > 1f ? new Color32(0, 0, 0, 0) : new Color32(255, 255, 255, 255);
        }), -4);
        var picture = Resources.Load<Texture2D>("Art/ShipBack");
        if (picture == null) picture = Resources.Load<Texture2D>("Art/CrashedShipBack");
        if (picture == null) return;
        Color32[] pixels = PlanetOne.ReadPixels(picture);
        int lowest = 0;
        while (lowest < picture.height - 1 && System.Array.FindIndex(pixels, lowest * picture.width, picture.width, c => c.a > 0) < 0) lowest++;
        var texture = new Texture2D(picture.width, picture.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply();
        back = Plain("Ship Back", Sprite.Create(texture, new Rect(0, 0, picture.width, picture.height), new Vector2(0.5f, lowest / (float)picture.height), 1f), 10);
        Thrusters = FindThrusters(pixels, picture.width, picture.height, lowest);
        int left = picture.width, right = 0, highest = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a == 0) continue;
            left = Mathf.Min(left, i % picture.width);
            right = Mathf.Max(right, i % picture.width + 1);
            highest = Mathf.Max(highest, i / picture.width + 1);
        }
        backSize = new Vector2(Mathf.Max(1, right - left), Mathf.Max(1, highest - lowest));
    }

    // The middle of each patch of thruster glass in his picture, left to right, in his pixels from its foot.
    static Vector2[] FindThrusters(Color32[] pixels, int wide, int tall, int lowest)
    {
        bool Glass(int i) => System.Array.Exists(ThrusterGlass, g => g.r == pixels[i].r && g.g == pixels[i].g && g.b == pixels[i].b) && pixels[i].a > 0;
        var seen = new bool[pixels.Length];
        var found = new System.Collections.Generic.List<Vector2>();
        var queue = new System.Collections.Generic.Queue<int>();
        for (int start = 0; start < pixels.Length; start++)
        {
            if (seen[start] || !Glass(start)) continue;
            seen[start] = true;
            queue.Enqueue(start);
            float sumX = 0f, sumY = 0f;
            int count = 0;
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int x = i % wide, y = i / wide;
                sumX += x + 0.5f;
                sumY += y + 0.5f;
                count++;
                foreach (int next in new[] { x > 0 ? i - 1 : -1, x < wide - 1 ? i + 1 : -1, y > 0 ? i - wide : -1, y < tall - 1 ? i + wide : -1 })
                {
                    if (next < 0 || seen[next] || !Glass(next)) continue;
                    seen[next] = true;
                    queue.Enqueue(next);
                }
            }
            // Only real thrusters, not a stray pixel or two.
            if (count >= 12) found.Add(new Vector2(sumX / count - wide / 2f, sumY / count - lowest));
        }
        found.Sort((a, b) => a.x.CompareTo(b.x));
        return found.ToArray();
    }

    // Space: the stars, the sun, the black hole, both planets, the ice shards and the orbs' glow.
    void MakeSpace()
    {
        Sprite white = PlanetOne.PaintSprite(1, 1, new Vector2(0.5f, 0.5f), 1f, (x, y) => new Color32(255, 255, 255, 255));
        stars = new SpriteRenderer[StarCount];
        starAt = new Vector3[StarCount];
        starBright = new float[StarCount];
        for (int i = 0; i < StarCount; i++)
        {
            stars[i] = Plain("Star", white, -9);
            // Which way it is, anywhere round the sky, and how bright.
            starAt[i] = new Vector3(Spread(1f), Spread(1f), Spread(1f));
            while (starAt[i].sqrMagnitude > 1f || starAt[i].sqrMagnitude < 0.01f) starAt[i] = new Vector3(Spread(1f), Spread(1f), Spread(1f));
            starAt[i].Normalize();
            starBright[i] = 0.35f + 0.65f * (float)random.NextDouble();
        }
        sun = Plain("Sun", DayNight.SunSprite(), -9);
        blackHole = Plain("Black Hole", DayNight.BlackHoleSprite(), -9);
        blackHole.transform.localRotation = Quaternion.Euler(0f, 0f, -14f);
        // Round and turning: the next planet is the rocky one unless it is Planet 1 (flying home from the rocky one).
        bool toRocky = PaintNext == PaintRocky;
        nextBall = new Ball(toRocky);
        homeBall = new Ball(!toRocky);
        nextPlanet = Plain("Next Planet", nextBall.Sprite, -8);
        homePlanet = Plain("Planet Left Behind", homeBall.Sprite, -8);
        // A small ice shard: a diamond of pixels.
        shardSprite = PlanetOne.PaintSprite(3, 3, new Vector2(0.5f, 0.5f), 1f, (x, y) => x == 1 || y == 1 ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0));
        // The glow of a magical orb, round, in the middle of a thruster.
        Sprite glow = PlanetOne.PaintSprite(9, 9, new Vector2(0.5f, 0.5f), 1f, (x, y) =>
        {
            float dx = x - 4f, dy = y - 4f, r = dx * dx + dy * dy;
            return r > 17f ? new Color32(0, 0, 0, 0) : new Color32(255, 255, 255, (byte)(r < 5f ? 255 : 150));
        });
        // On the ship's back, so they lean with it.
        glows = new SpriteRenderer[Thrusters.Length];
        for (int i = 0; i < glows.Length; i++)
        {
            glows[i] = Plain("Orb Glow", glow, 11);
            if (back == null) continue;
            glows[i].transform.SetParent(back.transform, false);
            glows[i].transform.localPosition = Thrusters[i];
        }
        HideSpace();
    }

    float Spread(float size) => ((float)random.NextDouble() * 2f - 1f) * size;

    // A planet as a round ball of pixels, painted by paint(x, y, size), lit from the left.
    Sprite PlanetSprite(System.Func<int, int, int, Color32> paint)
    {
        return PlanetOne.PaintSprite(PlanetPixels, PlanetPixels, new Vector2(0.5f, 0.5f), PlanetPixels, (x, y) =>
        {
            float dx = (x + 0.5f) / PlanetPixels * 2f - 1f, dy = (y + 0.5f) / PlanetPixels * 2f - 1f, r = dx * dx + dy * dy;
            if (r > 1f) return new Color32(0, 0, 0, 0);
            Color32 c = paint(x, y, PlanetPixels);
            // The far side from the sun is in shadow, in two sharp steps.
            float light = -dx * 0.8f - dy * 0.3f + 0.4f;
            float shade = light > 0f ? 1f : light > -0.45f ? 0.7f : 0.45f;
            return new Color32((byte)(c.r * shade), (byte)(c.g * shade), (byte)(c.b * shade), 255);
        });
    }

    // The rocky planet: desert rock, with darker hollows and paler sand.
    public static Color32 PaintRocky(int x, int y, int size)
    {
        float n = Blobs(x, y, 9f, 3) * 0.65f + Blobs(x, y, 4f, 5) * 0.35f;
        // Its main colour is the tan Taivan picked, 197, 157, 109.
        if (n < 0.36f) return new Color32(0x7E, 0x5E, 0x3E, 255);
        if (n < 0.5f) return new Color32(0xA0, 0x7C, 0x54, 255);
        if (n < 0.7f) return new Color32(197, 157, 109, 255);
        return new Color32(0xD0, 0xAC, 0x7E, 255);
    }

    // Planet 1: snow and ice, blue ocean, and patches of teal algae.
    public static Color32 PaintIcy(int x, int y, int size)
    {
        float n = Blobs(x, y, 11f, 7) * 0.7f + Blobs(x, y, 5f, 2) * 0.3f;
        float fromMiddle = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
        if (fromMiddle > 0.8f) return new Color32(0xF2, 0xF8, 0xFC, 255);
        if (n < 0.38f) return new Color32(0x2A, 0x5E, 0x9E, 255);
        if (n > 0.68f) return new Color32(0x2E, 0xA0, 0x8E, 255);
        return n < 0.5f ? new Color32(0xC6, 0xD6, 0xE4, 255) : new Color32(0xE4, 0xEE, 0xF4, 255);
    }

    // Smooth blobs from 0 to 1, about blob pixels across.
    static float Blobs(int x, int y, float blob, int seed)
    {
        float fx = x / blob, fy = y / blob;
        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float sx = fx - x0, sy = fy - y0;
        sx = sx * sx * (3f - 2f * sx);
        sy = sy * sy * (3f - 2f * sy);
        return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0, seed), Hash(x0 + 1, y0, seed), sx), Mathf.Lerp(Hash(x0, y0 + 1, seed), Hash(x0 + 1, y0 + 1, seed), sx), sy);
    }

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393) ^ (uint)(y * 668265263) ^ (uint)(seed * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h >> 8) / 16777216f;
        }
    }

    SpriteRenderer Plain(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(backRoot, false);
        var picture = go.AddComponent<SpriteRenderer>();
        picture.sprite = sprite;
        picture.sortingOrder = order;
        return picture;
    }

    void Update()
    {
        if (!Aboard)
        {
            // E, standing next to the fixed ship, gets in.
            if (NextToShip && GameInput.UsePressed()) GetIn();
            return;
        }
        if (GameInput.ShipControlsPressed()) ControlsShown = !ControlsShown;
        if (GameInput.ShipInsidePressed()) LookingFromInside = !LookingFromInside;
        if (stage == Stage.Landed)
        {
            if (GameInput.ShipLeavePressed() || GameInput.UsePressed())
            {
                GetOut();
                return;
            }
            if (GameInput.ShipLaunchPressed()) Launch();
            return;
        }
        float since = Time.time - stageSince;
        if (stage == Stage.Rising && since >= RiseSeconds) SetStage(Stage.InSpace);
        if (stage == Stage.InSpace || stage == Stage.Arriving)
        {
            Vector2 steer = stage == Stage.InSpace ? (TestSteer ?? GameInput.ShipSteer()) : Vector2.zero;
            facing *= Quaternion.Euler(-steer.y * TurnSpeed * Time.deltaTime, steer.x * TurnSpeed * Time.deltaTime, 0f);
            shipAt += facing * Vector3.forward * FlySpeed * Time.deltaTime;
            float landsAt = PlanetRadius + LandsWithin + FlySpeed * ArriveSeconds;
            if (stage == Stage.InSpace && Vector3.Distance(shipAt, nextAt) < landsAt)
            {
                goingHome = false;
                SetStage(Stage.Arriving);
            }
            else if (stage == Stage.InSpace && Vector3.Distance(shipAt, homeAt) < landsAt)
            {
                goingHome = true;
                SetStage(Stage.Arriving);
            }
        }
        if (stage == Stage.Arriving && since >= ArriveSeconds)
        {
            // Back to the planet just left: the ship comes down where it took off, nothing else changed.
            if (goingHome)
            {
                goingHome = false;
                hud.Say("You landed back on " + HomeName + "!", 3f);
                SetStage(Stage.Landed);
                // And the player gets out by themselves (Taivan, 2026-10-20).
                GetOut();
            }
            else if (Arrived != null) Arrived();
            else
            {
                hud.Say("Landing on " + NextPlanetName + " is being built next!", 3f);
                SetStage(Stage.Landed);
                GetOut();
            }
        }
    }

    // Whether the player stands next to the fixed ship, out in the open, close enough to get in.
    bool NextToShip => ship.StandingThere && !mining.Active && !planet.InCave && !nook.Open
        && Vector2.Distance(player.GroundPosition, ship.FootNear(player.GroundPosition)) <= GetInWithin && player.FeetHeight < 0.6f;

    void SetStage(Stage next)
    {
        stage = next;
        stageSince = Time.time;
    }

    // Leaves the ground: the trip begins with the next planet ahead, off to one side, and the planet left behind
    // just behind.
    public void Launch()
    {
        if (!Aboard || stage != Stage.Landed) return;
        SetStage(Stage.Rising);
        shipAt = Vector3.zero;
        facing = Quaternion.identity;
        nextAt = Quaternion.Euler(-StartsAbove, StartsAside, 0f) * Vector3.forward * TripLength;
        homeAt = Vector3.back * HomeBehind;
        goingHome = false;
    }

    public void GetIn()
    {
        Aboard = true;
        SetStage(Stage.Landed);
        LookingFromInside = false;
        ControlsShown = false;
        GameInput.InShip = true;
        player.Frozen = true;
        player.Hidden = true;
        nook.Close();
        ship.Seated = true;
        view.enabled = true;
    }

    public void GetOut()
    {
        Aboard = false;
        GameInput.InShip = false;
        player.Frozen = false;
        player.Hidden = false;
        ship.Seated = false;
        ship.GlassShows = GlassFromOutside;
        ship.Viewer = null;
        view.enabled = false;
        HideSpace();
    }

    void LateUpdate()
    {
        if (!Aboard) return;
        LookFromBehind();
    }

    // His picture of the ship's back: standing on the ground under the sky, rising off it, or flying through space.
    void LookFromBehind()
    {
        backRoot.gameObject.SetActive(true);
        ship.GlassShows = GlassFromOutside;
        ship.Viewer = null;
        if (back == null) return;
        Vector2 picture = backSize;
        int scale = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(Screen.height * BackFills / picture.y, Screen.width * BackWide / picture.x)));
        viewScale = scale;
        float halfTall = Screen.height / (2f * scale), halfWide = halfTall * view.aspect;
        view.orthographicSize = halfTall;
        view.transform.position = new Vector3(ViewPlace.x, ViewPlace.y, -10f);
        // Lines up a height on screen (0 the bottom, 1 the top) with his pixels.
        float Up(float share) => Mathf.Round(-halfTall + share * 2f * halfTall);

        float since = Time.time - stageSince;
        // How far through taking off: 0 on the ground, 1 in space.
        float risen = stage == Stage.Landed ? 0f : stage == Stage.Rising ? Mathf.Clamp01(since / RiseSeconds) : 1f;
        Color tint = risen > 0f ? Color.white : dayNight.WorldTint;
        Color skyTop = Color.Lerp(Color.Lerp(DaySkyTop, NightSkyTop, dayNight.Night), Color.black, Mathf.Clamp01(risen * 1.5f));
        Color skyLow = Color.Lerp(Color.Lerp(DaySkyLow, NightSkyLow, dayNight.Night), Color.black, Mathf.Clamp01(risen * 1.2f));
        view.backgroundColor = skyTop;

        // The ground falls away below as the ship rises.
        float drop = Mathf.Round(risen * risen * halfTall * 2.4f);
        float horizonY = Up(HorizonAt) - drop;
        bool groundShows = horizonY > -halfTall;
        for (int i = 0; i < SkyBands; i++)
        {
            // Bands of sky from the horizon up, paler near it.
            float bottom = Mathf.Round(horizonY + (halfTall - horizonY) * i / SkyBands), top = Mathf.Round(horizonY + (halfTall - horizonY) * (i + 1) / SkyBands);
            Place(sky[i], new Vector2(0f, (bottom + top) / 2f), new Vector2(halfWide * 2f + 2f, top - bottom));
            sky[i].color = Color.Lerp(skyLow, skyTop, i / (SkyBands - 1f));
            sky[i].enabled = risen < 1f;
        }
        ground.enabled = horizon.enabled = groundShows;
        Place(ground, new Vector2(0f, (horizonY - halfTall) / 2f), new Vector2(halfWide * 2f + 2f, horizonY + halfTall));
        ground.color = (Color)GroundColor * tint;
        Place(horizon, new Vector2(0f, horizonY - 1f), new Vector2(halfWide * 2f + 2f, 2f));
        horizon.color = (Color)GroundShade * tint;
        for (int i = 0; i < specks.Length; i++)
        {
            Vector2 spot = speckSpots[i];
            float y = Mathf.Round(horizonY - 2f - spot.y * (halfTall * 2f - 2f));
            specks[i].enabled = groundShows && y > -halfTall;
            specks[i].transform.position = (Vector3)(ViewPlace + new Vector2(Mathf.Round(spot.x * halfWide), y));
            // Nearer ones bigger.
            specks[i].transform.localScale = new Vector3(spot.y > 0.5f ? 2f : 1f, 1f, 1f);
            specks[i].color = (Color)(specks[i].sortingOrder == -5 ? GroundSpeck : GroundShade) * tint;
        }

        // The ship stands on the ground, rises a little up the screen as it takes off, and rocks gently in space. From
        // inside, in space, it is seen from the side instead, close up, the crystal very see-through.
        float standY = Mathf.Round(Mathf.Lerp(Up(BackStandsAt), Up(BackFliesAt), Mathf.SmoothStep(0f, 1f, risen)));
        Vector2 steer = stage == Stage.InSpace ? (TestSteer ?? GameInput.ShipSteer()) : Vector2.zero;
        // Steering, it leans into the turn, and rises or dips a little as it climbs or dives.
        float ease = Mathf.Clamp01(LeanEase * Time.deltaTime);
        lean = Mathf.Lerp(lean, -steer.x * LeanMost, ease);
        pitch = Mathf.Lerp(pitch, steer.y * PitchMost, ease);
        Vector2 shipSpot = new Vector2(Mathf.Round(lean / LeanMost * 4f), standY + Mathf.Round(pitch) + (risen >= 1f ? Mathf.Round(Mathf.Sin(Time.time * 2f) * 1.2f) : 0f));
        // From the player's eyes (R, since 2026-10-20) the ship itself is not seen: just space, or the sky and the ground,
        // clear. (It had a purple tint and the crystal's facet edges round it, taken away the same day.)
        bool fromSide = LookingFromInside;
        back.enabled = !fromSide;
        back.transform.position = (Vector3)(ViewPlace + shipSpot);
        back.transform.localRotation = Quaternion.Euler(0f, 0f, lean);
        // Climbing it shows a little more of its top, diving a little less.
        back.transform.localScale = new Vector3(1f, 1f + pitch / PitchMost * 0.06f, 1f);
        back.color = tint;
        backShadow.enabled = groundShows && !fromSide;
        backShadow.transform.position = (Vector3)(ViewPlace + new Vector2(0f, Up(BackStandsAt) + 1f - drop));
        backShadow.transform.localScale = new Vector3(picture.x * 0.9f / 40f * (1f - risen * 0.6f), 1f, 1f);
        backShadow.color = (Color)GroundShade * 0.85f * tint;

        bool thrusting = stage != Stage.Landed;
        for (int i = 0; i < glows.Length; i++)
        {
            glows[i].enabled = !fromSide;
            // The orbs glow softly on the ground, and burn bright while they blow out ice.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (thrusting ? 14f : 3f) + i * 2f);
            glows[i].color = thrusting ? Color.Lerp(new Color32(0xD7, 0xB4, 0xEE, 255), Color.white, 0.4f + pulse * 0.5f) : new Color(0.85f, 0.72f, 0.95f, 0.25f + pulse * 0.25f);
        }
        BlowShards(thrusting && !fromSide);
        ShowSpace(risen, halfTall, halfWide, scale);
        // A little shake while taking off.
        if (stage == Stage.Rising)
        {
            Vector2 shake = Random.insideUnitCircle * (1f - since / RiseSeconds) * 2f;
            view.transform.position += new Vector3(Mathf.Round(shake.x), Mathf.Round(shake.y), 0f);
        }
    }

    // Ice shards blown out of the three thrusters toward the viewer: spreading out and down, growing and fading.
    void BlowShards(bool blowing)
    {
        if (blowing)
        {
            shardsOwed += ShardsEachSecond * Time.deltaTime;
            for (; shardsOwed >= 1f; shardsOwed -= 1f)
            {
                if (Thrusters.Length == 0) break;
                Vector2 port = Thrusters[random.Next(Thrusters.Length)];
                Shard shard = null;
                foreach (Shard old in shards) if (!old.Picture.enabled) { shard = old; break; }
                if (shard == null)
                {
                    shard = new Shard { Picture = Plain("Ice Shard", shardSprite, 12) };
                    shards.Add(shard);
                }
                shard.Picture.enabled = true;
                // The ice pillar's shards, as he drew them; without them, small diamonds of ice.
                bool his = ExhaustShards != null && ExhaustShards.Length > 0;
                shard.Picture.sprite = his ? ExhaustShards[random.Next(ExhaustShards.Length)] : shardSprite;
                shard.Picture.color = his ? Color.white : (Color)ShardColors[random.Next(ShardColors.Length)];
                // Blown from the thruster as the ship leans now.
                Vector2 from = back.transform.TransformPoint(port + new Vector2(Spread(3f), Spread(3f)));
                shard.From = from - ViewPlace;
                // Out from the middle of the ship and a little down, as if blown back past the viewer.
                Vector2 way = new Vector2(port.x * 0.04f + Spread(1f), Spread(0.6f) - 0.7f).normalized;
                shard.Way = way * Mathf.Lerp(ShardSpeedLow, ShardSpeedHigh, (float)random.NextDouble());
                shard.Born = Time.time;
            }
        }
        foreach (Shard shard in shards)
        {
            if (!shard.Picture.enabled) continue;
            float age = (Time.time - shard.Born) / ShardSeconds;
            if (age >= 1f || !blowing)
            {
                shard.Picture.enabled = false;
                continue;
            }
            Vector2 at = shard.From + shard.Way * (Time.time - shard.Born);
            shard.Picture.transform.position = (Vector3)(ViewPlace + new Vector2(Mathf.Round(at.x), Mathf.Round(at.y)));
            // Bigger as they come nearer, in steps. Each pixel of the ice pillar's shards is a whole number of the screen's
            // pixels, about a third of one of the ship's, then two thirds.
            Sprite drawn = shard.Picture.sprite;
            float steps = 1f + Mathf.Floor(age * 2f);
            float size = drawn == shardSprite ? steps : drawn.pixelsPerUnit * Mathf.Max(1f, Mathf.Round(viewScale * 0.34f * steps)) / viewScale;
            shard.Picture.transform.localScale = new Vector3(size, size, 1f);
            Color c = shard.Picture.color;
            c.a = age < 0.6f ? 1f : 1f - (age - 0.6f) / 0.4f;
            shard.Picture.color = c;
        }
    }

    void HideSpace()
    {
        foreach (SpriteRenderer star in stars) star.enabled = false;
        sun.enabled = blackHole.enabled = nextPlanet.enabled = homePlanet.enabled = false;
        foreach (Shard shard in shards) shard.Picture.enabled = false;
    }

    // The stars, the sun, the black hole and the planets, as seen from just behind the ship looking ahead.
    void ShowSpace(float risen, float halfTall, float halfWide, int scale)
    {
        if (risen <= 0f)
        {
            foreach (SpriteRenderer star in stars) star.enabled = false;
            sun.enabled = blackHole.enabled = nextPlanet.enabled = homePlanet.enabled = false;
            return;
        }
        // How far from the middle of the screen something straight ahead and one step to the side is drawn.
        float focal = halfTall;
        float shows = Mathf.Clamp01(risen * 2f - 0.6f);
        // The stars are so far away that flying never brings them nearer: like the sun and the black hole, they only move
        // across the screen as the ship turns. (Until 2026-10-20 they rushed past like specks of dust, which Taivan did
        // not like; then for a moment they stood still on the screen.)
        Quaternion toShip = Quaternion.Inverse(facing);
        for (int i = 0; i < stars.Length; i++)
        {
            Vector3 way = toShip * starAt[i];
            Vector2 spot = way.z > 0.05f ? new Vector2(way.x, way.y) / way.z * focal : Vector2.zero;
            stars[i].enabled = shows > 0f && way.z > 0.05f && Mathf.Abs(spot.x) < halfWide + 1f && Mathf.Abs(spot.y) < halfTall + 1f;
            if (!stars[i].enabled) continue;
            stars[i].transform.position = (Vector3)(ViewPlace + new Vector2(Mathf.Round(spot.x), Mathf.Round(spot.y)));
            stars[i].transform.localScale = Vector3.one;
            stars[i].color = new Color(0.88f, 0.91f, 1f, starBright[i] * shows);
        }
        // The sun and the black hole are so far away they only move as the ship turns. Each of their own pixels is
        // drawn a whole number of the screen's pixels: two for the sun, one for the black hole's finer picture.
        Distant(sun, SunWay, focal, 2f * 16f / scale, shows);
        Distant(blackHole, BlackHoleWay, focal, 24f / scale, shows);
        Near(nextPlanet, nextAt, focal, shows);
        Near(homePlanet, homeAt, focal, shows);
        // The planets turn, and are lit from the sun as the ship sees it.
        Vector3 sunSeen = Quaternion.Inverse(facing) * SunWay;
        var toSun = new Vector3(sunSeen.x, sunSeen.y, -sunSeen.z);
        if (nextPlanet.enabled) nextBall.Paint(toSun, Time.deltaTime);
        if (homePlanet.enabled) homeBall.Paint(toSun, Time.deltaTime);
    }

    void Distant(SpriteRenderer picture, Vector3 way, float focal, float size, float shows)
    {
        Vector3 local = Quaternion.Inverse(facing) * way;
        picture.enabled = shows > 0f && local.z > 0.05f;
        if (!picture.enabled) return;
        Vector2 spot = new Vector2(local.x, local.y) / local.z * focal;
        picture.transform.position = (Vector3)(ViewPlace + new Vector2(Mathf.Round(spot.x), Mathf.Round(spot.y)));
        picture.transform.localScale = new Vector3(size, size, 1f);
        picture.color = new Color(1f, 1f, 1f, shows);
    }

    // A planet: drawn bigger the nearer it is. (Ones behind the viewer are not drawn.)
    void Near(SpriteRenderer picture, Vector3 at, float focal, float shows)
    {
        Vector3 local = Quaternion.Inverse(facing) * (at - shipAt);
        picture.enabled = shows > 0f && local.z > PlanetRadius * 0.3f;
        if (!picture.enabled) return;
        Vector2 spot = new Vector2(local.x, local.y) / local.z * focal;
        picture.transform.position = (Vector3)(ViewPlace + new Vector2(Mathf.Round(spot.x), Mathf.Round(spot.y)));
        float across = Mathf.Max(3f, Mathf.Round(PlanetRadius * 2f / local.z * focal));
        picture.transform.localScale = new Vector3(across, across, 1f);
        picture.color = new Color(1f, 1f, 1f, shows);
        // Nearer planets are drawn in front of further ones and of the sun.
        picture.sortingOrder = -8 + (local.z < 60f ? 1 : 0);
    }

    // Where a planet is on screen, in the screen's pixels from its top left, and whether it is on screen.
    bool OnScreen(Vector3 at, out Vector2 spot)
    {
        Vector3 local = Quaternion.Inverse(facing) * (at - shipAt);
        float scaleNow = Screen.height / (2f * view.orthographicSize);
        Vector2 flat = local.z > 0.01f ? new Vector2(local.x, local.y) / local.z * view.orthographicSize : new Vector2(local.x, local.y).normalized * 1e5f;
        spot = new Vector2(Screen.width / 2f + flat.x * scaleNow, Screen.height / 2f - flat.y * scaleNow);
        return local.z > 0.01f && spot.x > 0f && spot.x < Screen.width && spot.y > 0f && spot.y < Screen.height;
    }

    // While a planet is off the screen, its name at the edge points the way to it (lower down by drop, so two names
    // pointing the same way do not cover each other).
    void PointTo(Vector3 at, string name, float drop)
    {
        if (OnScreen(at, out Vector2 spot)) return;
        Vector2 middle = new Vector2(Screen.width / 2f, Screen.height / 2f), way = spot - middle;
        float reach = Mathf.Min((Screen.width / 2f - 90f) / Mathf.Max(0.001f, Mathf.Abs(way.x)), (Screen.height / 2f - 70f) / Mathf.Max(0.001f, Mathf.Abs(way.y)));
        Vector2 edge = middle + way * reach;
        string words = Mathf.Abs(way.x) > Mathf.Abs(way.y) ? (way.x > 0f ? name + " >" : "< " + name) : (way.y > 0f ? "v " + name + " v" : "^ " + name + " ^");
        GameText.Label(new Rect(edge.x - 110f, edge.y - 14f + drop, 220f, 28f), words, markerStyle);
    }

    void Place(SpriteRenderer picture, Vector2 middle, Vector2 size)
    {
        picture.transform.position = (Vector3)(ViewPlace + middle);
        picture.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    void OnGUI()
    {
        if (!Aboard && !NextToShip) return;
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            lineStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            markerStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }
        // Next to the ship: how to get in.
        if (!Aboard)
        {
            GameText.Label(new Rect(0f, Screen.height - 130f, Screen.width, 30f), "E: get in the ship", hintStyle);
            return;
        }
        // In space: how far the next planet is, and which way, when it is off the screen.
        if (stage == Stage.InSpace)
        {
            GameText.Label(new Rect(0f, 12f, Screen.width, 26f), "Flying to " + NextPlanetName + ": " + Mathf.CeilToInt(TripLeft / FlySpeed) + " seconds away", hintStyle);
            // The planet just left can be flown back to.
            float homeLeft = Vector3.Distance(shipAt, homeAt) - PlanetRadius;
            GameText.Label(new Rect(0f, 36f, Screen.width, 24f), "Back to " + HomeName + ": " + Mathf.CeilToInt(homeLeft / FlySpeed) + " seconds away", hintStyle);
            PointTo(nextAt, NextPlanetName, 0f);
            PointTo(homeAt, HomeName, 30f);
        }
        // Arriving: the screen fills with the planet's colour.
        if (stage == Stage.Arriving)
        {
            Color before = GUI.color;
            Color fill = goingHome ? HomeColor : NextColor;
            GUI.color = new Color(fill.r, fill.g, fill.b, Mathf.Clamp01((Time.time - stageSince) / ArriveSeconds));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = before;
        }
        if (!ControlsShown)
        {
            GameText.Label(new Rect(0f, Screen.height - 40f, Screen.width, 30f), "F: the ship's controls", hintStyle);
            return;
        }
        string[,] lines =
        {
            { "Left click", stage == Stage.Landed ? "Launch into space" : "(flying)" },
            { "W A S D", "Steer, in space" },
            { "R", LookingFromInside ? "The view from behind the ship" : "The view from your eyes" },
            { "F", "Hide these controls" },
            { "E", stage == Stage.Landed ? "Get out of the ship" : "Get out, once landed" },
        };
        float width = 420f, height = 56f + lines.GetLength(0) * 30f + 12f;
        var panel = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        GUI.Box(panel, GUIContent.none);
        GUI.Box(panel, GUIContent.none);
        GameText.Label(new Rect(panel.x, panel.y + 10f, width, 28f), "The ship's controls", titleStyle);
        for (int i = 0; i < lines.GetLength(0); i++)
        {
            float y = panel.y + 52f + i * 30f;
            GameText.Label(new Rect(panel.x + 24f, y, 130f, 28f), lines[i, 0], lineStyle);
            GameText.Label(new Rect(panel.x + 160f, y, width - 180f, 28f), lines[i, 1], lineStyle);
        }
    }

    // For checking: how far there is still to go, turning straight toward the next planet, and the part of the trip.
    public float TripLeft => Vector3.Distance(shipAt, nextAt) - PlanetRadius;
    public void FaceNext() => facing = Quaternion.LookRotation(nextAt - shipAt);
    public void FaceHome() => facing = Quaternion.LookRotation(homeAt - shipAt);
    public string StageName => stage.ToString();
    // For checking: steers as if these keys were held.
    public Vector2? TestSteer;
}
