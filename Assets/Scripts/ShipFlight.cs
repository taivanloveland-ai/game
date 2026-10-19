using UnityEngine;

// Sitting in the fixed ship (Taivan's request of 2026-10-18). Right-click the standing ship to get in, right-click
// again to get out. Inside, the usual view is from behind the ship (his picture of its back, Art/CrashedShipBack),
// R switches to the view from inside, where the purple crystal is very see-through and the player can be seen in
// there, and F shows or hides the ship's controls. Left click launches into space.
public class ShipFlight : MonoBehaviour
{
    // The player gets in from this close to the ship's foot.
    const float GetInWithin = 4.5f;
    // How much of the purple crystal shows: from outside, and in the view from inside.
    const float GlassFromOutside = 0.75f, GlassFromInside = 0.25f;
    // The view from behind is drawn far from the planet, where nothing else is.
    static readonly Vector2 ViewPlace = new Vector2(-40000f, -40000f);
    // The view from behind: his picture of the ship's back fills about this much of the screen's height, and stands
    // this far down the screen (0 the bottom, 1 the top), on snow up to the horizon this far down.
    const float BackFills = 0.42f, BackStandsAt = 0.18f, HorizonAt = 0.42f;
    // The view from inside: the standing ship fills about this much of the screen's height.
    const float InsideFills = 0.8f;
    static readonly Color32 SnowColor = new Color32(0xE4, 0xEE, 0xF4, 255), SnowShade = new Color32(0xC6, 0xD6, 0xE4, 255), SnowSparkle = new Color32(0xFF, 0xFF, 0xFF, 255);
    static readonly Color DaySkyTop = new Color32(0x4E, 0x86, 0xC8, 255), DaySkyLow = new Color32(0xB8, 0xD6, 0xEE, 255);
    static readonly Color NightSkyTop = new Color32(0x08, 0x0C, 0x24, 255), NightSkyLow = new Color32(0x24, 0x30, 0x5C, 255);
    const int SkyBands = 8;

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

    // The camera for the views from behind and from inside.
    Camera view;
    Transform backRoot;
    SpriteRenderer back, ground, horizon, backShadow;
    SpriteRenderer[] sky;
    // Specks on the snow, and where each is: x from -1 to 1 across the screen, y from the horizon (0) to the bottom (1).
    SpriteRenderer[] specks;
    Vector2[] speckSpots;
    GUIStyle titleStyle, lineStyle, hintStyle;

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
        MakeBackView();
    }

    // The view from behind the ship: sky, snow to the horizon, and the ship's back standing on it. Everything is in his
    // pixels (one unit each), drawn a whole number of the screen's pixels big.
    void MakeBackView()
    {
        backRoot = new GameObject("Ship Back View").transform;
        backRoot.position = ViewPlace;
        Sprite white = PlanetOne.PaintSprite(1, 1, new Vector2(0.5f, 0.5f), 1f, (x, y) => new Color32(255, 255, 255, 255));
        sky = new SpriteRenderer[SkyBands];
        for (int i = 0; i < SkyBands; i++) sky[i] = Plain("Sky", white, -10);
        ground = Plain("Snow", white, -8);
        ground.color = SnowColor;
        horizon = Plain("Horizon", white, -7);
        horizon.color = SnowShade;
        // Specks of shade and sparkle on the snow, nearer ones further apart, so it looks like ground going away.
        var random = new System.Random(7);
        specks = new SpriteRenderer[260];
        speckSpots = new Vector2[specks.Length];
        for (int i = 0; i < specks.Length; i++)
        {
            specks[i] = Plain("Speck", white, -6);
            // More of them far away, near the horizon.
            speckSpots[i] = new Vector2((float)random.NextDouble() * 2f - 1f, (float)System.Math.Pow(random.NextDouble(), 1.6));
            specks[i].color = random.Next(3) == 0 ? SnowSparkle : SnowShade;
        }
        backShadow = Plain("Ship Shadow", PlanetOne.PaintSprite(40, 8, new Vector2(0.5f, 0.5f), 1f, (x, y) =>
        {
            float dx = (x + 0.5f - 20f) / 20f, dy = (y + 0.5f - 4f) / 4f;
            return dx * dx + dy * dy > 1f ? new Color32(0, 0, 0, 0) : new Color32(0x9C, 0xB0, 0xC6, 255);
        }), -5);
        var picture = Resources.Load<Texture2D>("Art/CrashedShipBack");
        if (picture == null) return;
        Color32[] pixels = PlanetOne.ReadPixels(picture);
        int lowest = 0;
        while (lowest < picture.height - 1 && System.Array.FindIndex(pixels, lowest * picture.width, picture.width, c => c.a > 0) < 0) lowest++;
        var texture = new Texture2D(picture.width, picture.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply();
        back = Plain("Ship Back", Sprite.Create(texture, new Rect(0, 0, picture.width, picture.height), new Vector2(0.5f, lowest / (float)picture.height), 1f), 0);
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
            // Right-click the standing ship (not its workbench) to get in.
            if (!ship.StandingThere || mining.Active || planet.InCave || !GameInput.RightClickPressed()) return;
            Vector2 point = cam.ScreenToWorldPoint(GameInput.PointerPosition());
            if (!ship.Covers(point) || nook.IsOnNook(point)) return;
            if (Vector2.Distance(player.GroundPosition, ship.FootNear(player.GroundPosition)) > GetInWithin || player.FeetHeight > 0.6f)
            {
                hud.Say("Walk up to the ship to get in.", 2f);
                return;
            }
            GetIn();
            return;
        }
        if (GameInput.ShipLeavePressed())
        {
            GetOut();
            return;
        }
        if (GameInput.ShipControlsPressed()) ControlsShown = !ControlsShown;
        if (GameInput.ShipInsidePressed()) LookingFromInside = !LookingFromInside;
        if (GameInput.ShipLaunchPressed()) hud.Say("Launching into space is being built next!", 2f);
    }

    public void GetIn()
    {
        Aboard = true;
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
    }

    void LateUpdate()
    {
        if (!Aboard) return;
        if (LookingFromInside) LookFromInside();
        else LookFromBehind();
    }

    // The standing ship close up, the crystal very see-through, the player in there.
    void LookFromInside()
    {
        ship.GlassShows = GlassFromInside;
        ship.Viewer = view;
        Vector2 size = ship.Size;
        // A whole number of the screen's pixels for each of his.
        int scale = Mathf.Max(1, Mathf.FloorToInt(Screen.height * InsideFills / (size.y * 9f)));
        view.orthographicSize = Screen.height / (2f * scale * 9f);
        view.backgroundColor = cam.backgroundColor;
        Vector2 foot = ship.FootNear(cam.transform.position);
        view.transform.position = new Vector3(foot.x, foot.y + size.y / 2f, -10f);
    }

    // His picture of the ship's back, standing on the snow, under the sky.
    void LookFromBehind()
    {
        ship.GlassShows = GlassFromOutside;
        ship.Viewer = null;
        if (back == null) return;
        Vector2 picture = back.sprite.rect.size;
        int scale = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(Screen.height * BackFills / picture.y, Screen.width * 0.6f / picture.x)));
        float halfTall = Screen.height / (2f * scale), halfWide = halfTall * view.aspect;
        view.orthographicSize = halfTall;
        view.transform.position = new Vector3(ViewPlace.x, ViewPlace.y, -10f);
        float night = dayNight.Night;
        view.backgroundColor = Color.Lerp(DaySkyTop, NightSkyTop, night);
        Color tint = dayNight.WorldTint;
        // Lines up a height on screen (0 the bottom, 1 the top) with his pixels.
        float Down(float share) => Mathf.Round(-halfTall + share * 2f * halfTall);

        float horizonY = Down(HorizonAt);
        for (int i = 0; i < SkyBands; i++)
        {
            // Bands of sky from the horizon up, paler near it.
            float bottom = Mathf.Round(horizonY + (halfTall - horizonY) * i / SkyBands), top = Mathf.Round(horizonY + (halfTall - horizonY) * (i + 1) / SkyBands);
            Place(sky[i], new Vector2(0f, (bottom + top) / 2f), new Vector2(halfWide * 2f + 2f, top - bottom));
            float up = i / (SkyBands - 1f);
            sky[i].color = Color.Lerp(Color.Lerp(DaySkyLow, NightSkyLow, night), Color.Lerp(DaySkyTop, NightSkyTop, night), up);
        }
        Place(ground, new Vector2(0f, (horizonY - halfTall) / 2f), new Vector2(halfWide * 2f + 2f, horizonY + halfTall));
        ground.color = SnowColor * tint;
        Place(horizon, new Vector2(0f, horizonY - 1f), new Vector2(halfWide * 2f + 2f, 2f));
        horizon.color = SnowShade * tint;
        for (int i = 0; i < specks.Length; i++)
        {
            Vector2 spot = speckSpots[i];
            float y = Mathf.Round(horizonY - 2f - spot.y * (horizonY + halfTall - 2f));
            specks[i].transform.position = (Vector3)(ViewPlace + new Vector2(Mathf.Round(spot.x * halfWide), y));
            // Nearer ones bigger.
            specks[i].transform.localScale = new Vector3(spot.y > 0.5f ? 2f : 1f, 1f, 1f);
        }
        float standY = Down(BackStandsAt);
        back.transform.position = (Vector3)(ViewPlace + new Vector2(0f, standY));
        back.color = tint;
        backShadow.transform.position = (Vector3)(ViewPlace + new Vector2(0f, standY + 1f));
        backShadow.transform.localScale = new Vector3(picture.x * 0.9f / 40f, 1f, 1f);
        backShadow.color = tint;
    }

    void Place(SpriteRenderer picture, Vector2 middle, Vector2 size)
    {
        picture.transform.position = (Vector3)(ViewPlace + middle);
        picture.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    void OnGUI()
    {
        if (!Aboard) return;
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            lineStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        }
        if (!ControlsShown)
        {
            GameText.Label(new Rect(0f, Screen.height - 40f, Screen.width, 30f), "F: the ship's controls", hintStyle);
            return;
        }
        string[,] lines =
        {
            { "Left click", "Launch into space" },
            { "W A S D", "Steer, in space" },
            { "R", LookingFromInside ? "The view from behind the ship" : "The view from inside the ship" },
            { "F", "Hide these controls" },
            { "Right click", "Get out of the ship" },
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
}
