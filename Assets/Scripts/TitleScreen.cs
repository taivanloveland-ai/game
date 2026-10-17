using UnityEngine;

// The title screen, and the loading screen after it. Both show Planet 1 from space, with the red sun and the black
// hole it circles, against the stars. The title screen has three buttons: Survival, Creative and Exit. Picking a
// mode starts the world being made, and the same scene stays up as the loading screen, with a line saying what is
// being made and the black hole slowly turning, until the game begins.
public class TitleScreen : MonoBehaviour
{
    // The planet is painted this many pixels across and drawn this many blocks across.
    const int PlanetPixels = 256;
    const float PlanetSize = 10.4f;
    // Where things are on screen, in blocks from the middle (the screen is 14 blocks tall).
    static readonly Vector3 PlanetAt = new Vector3(-5.6f, -0.6f, 0f), SunAt = new Vector3(9.2f, 4.6f, 0f), BlackHoleAt = new Vector3(2.4f, 3.9f, 0f);
    // The scene is drawn over everything else, in case anything else is on screen.
    const int Order = 500;
    // How fast the black hole turns on the loading screen, in degrees a second.
    const float LoadingTurn = 8f;
    // The planet turns once in this many seconds: slowly, but enough to see.
    const float SpinTurnsPerSecond = 1f / 100f;
    // How far the sun and the black hole drift from their places as they circle, in blocks, and how fast.
    const float SunDrift = 0.45f, BlackHoleDrift = 0.6f, DriftSpeed = 0.22f;
    // The planet's lands are worked out once as a flat map this many pixels round and tall, and wrapped onto the
    // ball afresh each frame as it turns. The lands' noise repeats after this many blobs, so the map joins up.
    const int MapWide = 768, MapTall = 256, BlobsRound = 35;

    // The planet from space. Latitudes, in degrees: the North Pole's ice, the ring of mountains below it, the ocean
    // round the South Pole and its snowy cap. (The bands are drawn wider than they really are, so they can be seen.)
    const float NorthIceFrom = 76f, MountainsFrom = 56f, OceanFrom = -57f, SouthSnowFrom = -71f;
    static readonly Color Snow = new Color32(226, 234, 242, 255), Ice = new Color32(146, 202, 232, 255), Rock = new Color32(93, 107, 126, 255);
    static readonly Color Water = new Color32(38, 78, 122, 255), Peak = new Color32(245, 249, 252, 255);
    static readonly Color Night = new Color32(5, 8, 16, 255), Air = new Color32(150, 204, 240, 255);
    // The algae, on the equator: where its middle is, as a share of the way round (facing us as the title comes up),
    // and how far it reaches, in degrees
    // (drawn a little bigger than it really is, so it can be seen).
    const float AlgaeRound = 0.97f, AlgaeReach = 22f;
    // The way to the sun, for the planet's day and night sides.
    static readonly Vector3 ToSun = new Vector3(0.72f, 0.42f, 0.55f).normalized;

    PlanetOne planet;
    Camera cam;
    Color skyBefore;
    GameObject scene;
    Transform blackHole, sun;
    float blackHoleTurn = -14f;
    bool loading;
    float loadingSince;
    TitleMusic music;
    // Which mode's save slots are showing, if the player has pressed Survival or Creative: 0 for neither, 1 for
    // survival, 2 for creative. What is in each of those slots, and which one Delete has been pressed on once.
    int slotsFor;
    SaveGame.Summary[] slots;
    int deleting = -1;
    // The slot a new game is being named for, or -1, and the name typed so far. A new save has to be given a name
    // before it can be started.
    int naming = -1;
    string typed = "";
    GUIStyle fieldStyle, dateStyle;
    GUIStyle slotStyle, smallButtonStyle;
    GUIStyle buttonStyle, loadingStyle, stageStyle;
    Texture2D buttonBack, buttonOver;

    public void Setup(PlanetOne planet, Camera cam)
    {
        this.planet = planet;
        scene = new GameObject("Title Scene");
        scene.transform.SetParent(cam.transform, false);
        scene.transform.localPosition = new Vector3(0f, 0f, 10f);
        this.cam = cam;
        skyBefore = cam.backgroundColor;
        cam.backgroundColor = Color.black;
        music = TitleMusic.Play();

        Make("Stars", DayNight.StarsSprite(), Vector3.zero, Order);
        sun = Make("Sun", DayNight.SunSprite(), SunAt, Order + 1);
        blackHole = Make("Black Hole", DayNight.BlackHoleSprite(), BlackHoleAt, Order + 1);
        blackHole.localRotation = Quaternion.Euler(0f, 0f, -14f);
        MapPlanet();
        planetPicture = new Texture2D(PlanetPixels, PlanetPixels, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        planetPixels = new Color32[PlanetPixels * PlanetPixels];
        PaintPlanet();
        Sprite planetSprite = Sprite.Create(planetPicture, new Rect(0, 0, PlanetPixels, PlanetPixels), new Vector2(0.5f, 0.5f), PlanetPixels / PlanetSize);
        Transform world = Make("Planet 1", planetSprite, PlanetAt, Order + 2);
        // Tipped a little, as planets are.
        world.localRotation = Quaternion.Euler(0f, 0f, -16f);
    }

    Transform Make(string name, Sprite sprite, Vector3 at, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(scene.transform, false);
        go.transform.localPosition = at;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return go.transform;
    }

    // Starts a game: survival, or creative, in a save slot (counting from 0, or -1 for none). Given a saved game, it
    // carries that game on. The buttons call this.
    public void Choose(bool creative, int slot = -1, SaveGame.Loaded saved = null, string name = "")
    {
        if (loading) return;
        loading = true;
        loadingSince = Time.unscaledTime;
        planet.StartGame(creative, slot, saved, name);
    }

    void Update()
    {
        // The planet turns slowly. The sun and the black hole circle each other, a little way, on opposite sides.
        float time = Time.unscaledTime;
        spin += SpinTurnsPerSecond * Time.unscaledDeltaTime;
        PaintPlanet();
        var swing = new Vector3(Mathf.Cos(time * DriftSpeed), Mathf.Sin(time * DriftSpeed) * 0.6f, 0f);
        sun.localPosition = SunAt + swing * SunDrift;
        blackHole.localPosition = BlackHoleAt - swing * BlackHoleDrift;
        // The black hole's disc rocks gently on the title screen, and turns steadily on the loading screen.
        if (loading) blackHoleTurn -= LoadingTurn * Time.unscaledDeltaTime;
        blackHole.localRotation = Quaternion.Euler(0f, 0f, blackHoleTurn + (loading ? 0f : Mathf.Sin(time * 0.35f) * 5f));
        if (!loading) return;
        // The game has begun: the title scene goes.
        if (planet.Ready)
        {
            cam.backgroundColor = skyBefore;
            // The title theme fades away as the planet's music comes in.
            if (music != null) music.End();
            Destroy(scene);
            Destroy(this);
        }
    }

    void OnGUI()
    {
        GUI.depth = -20;
        float h = Screen.height, w = Screen.width;
        if (buttonStyle == null)
        {
            buttonBack = Fill(new Color(0.05f, 0.08f, 0.16f, 0.82f));
            buttonOver = Fill(new Color(0.16f, 0.3f, 0.46f, 0.92f));
            buttonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            buttonStyle.normal.background = buttonBack;
            buttonStyle.hover.background = buttonOver;
            buttonStyle.active.background = buttonOver;
            buttonStyle.normal.textColor = new Color(0.88f, 0.95f, 1f);
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;
            loadingStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            loadingStyle.normal.textColor = new Color(0.88f, 0.95f, 1f);
            stageStyle = new GUIStyle(loadingStyle) { fontStyle = FontStyle.Normal };
            stageStyle.normal.textColor = new Color(0.66f, 0.8f, 0.92f);
        }
        buttonStyle.fontSize = Mathf.RoundToInt(h / 22f);
        loadingStyle.fontSize = Mathf.RoundToInt(h / 13f);
        stageStyle.fontSize = Mathf.RoundToInt(h / 30f);

        // The buttons, and the loading words that take their place, are on the right, clear of the planet.
        float left = w * 0.6f, wide = w * 0.3f;
        if (loading)
        {
            int dots = (int)((Time.unscaledTime - loadingSince) * 2f) % 4;
            GameText.Label(new Rect(left - wide * 0.25f, h * 0.44f, wide * 1.5f, h * 0.14f), "Loading" + new string('.', dots) + new string(' ', 3 - dots), loadingStyle);
            GameText.Label(new Rect(left - wide * 0.25f, h * 0.58f, wide * 1.5f, h * 0.06f), planet.LoadingStage, stageStyle);
            return;
        }
        if (slotsFor != 0)
        {
            DrawSlots(w, h);
            return;
        }
        float tall = h * 0.1f, gap = h * 0.035f, top = h * 0.42f;
        if (GameText.Button(new Rect(left, top, wide, tall), "Survival", buttonStyle)) ShowSlots(false);
        if (GameText.Button(new Rect(left, top + tall + gap, wide, tall), "Creative", buttonStyle)) ShowSlots(true);
        if (GameText.Button(new Rect(left, top + (tall + gap) * 2f, wide, tall), "Exit", buttonStyle)) Exit();
    }

    // Opens the five save slots of a mode.
    void ShowSlots(bool creative)
    {
        slotsFor = creative ? 2 : 1;
        deleting = -1;
        naming = -1;
        slots = new SaveGame.Summary[SaveGame.Slots];
        for (int i = 0; i < slots.Length; i++) slots[i] = SaveGame.Peek(creative, i);
    }

    // The save slots of the mode that was picked: each is empty, with a button to start a new game in it, or holds a
    // saved game, with buttons to carry it on or delete it (Delete has to be pressed twice).
    void DrawSlots(float w, float h)
    {
        bool creative = slotsFor == 2;
        if (slotStyle == null)
        {
            slotStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
            slotStyle.normal.textColor = new Color(0.88f, 0.95f, 1f);
            smallButtonStyle = new GUIStyle(buttonStyle);
            dateStyle = new GUIStyle(slotStyle);
            dateStyle.normal.textColor = new Color(0.62f, 0.76f, 0.9f);
            // The box a save's name is typed in: bright writing on a dark box with a pale edge, so what is typed shows.
            fieldStyle = new GUIStyle(GUI.skin.textField) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            Texture2D fieldBack = Framed(new Color(0.1f, 0.16f, 0.27f, 1f), new Color(0.62f, 0.8f, 0.95f, 1f));
            fieldStyle.normal.background = fieldStyle.focused.background = fieldStyle.hover.background = fieldStyle.active.background = fieldBack;
            fieldStyle.normal.textColor = fieldStyle.focused.textColor = fieldStyle.hover.textColor = fieldStyle.active.textColor = Color.white;
            fieldStyle.border = new RectOffset(2, 2, 2, 2);
            fieldStyle.padding = new RectOffset(10, 10, 2, 2);
        }
        slotStyle.fontSize = Mathf.RoundToInt(h / 36f);
        dateStyle.fontSize = Mathf.RoundToInt(h / 50f);
        smallButtonStyle.fontSize = Mathf.RoundToInt(h / 44f);
        fieldStyle.fontSize = Mathf.RoundToInt(h / 46f);
        loadingStyle.fontSize = Mathf.RoundToInt(h / 18f);

        float left = w * 0.43f, wide = w * 0.54f, row = h * 0.095f, gap = h * 0.018f, top = h * 0.3f;
        GameText.Label(new Rect(left, top - row * 1.5f, wide, row), creative ? "Creative" : "Survival", loadingStyle);
        for (int i = 0; i < slots.Length; i++)
        {
            float y = top + i * (row + gap);
            GUI.DrawTexture(new Rect(left, y, wide, row), buttonBack);
            SaveGame.Summary saved = slots[i];
            // Naming a new game: a box to type in, with Start (once something has been typed) and Cancel.
            if (naming == i)
            {
                GameText.Label(new Rect(left + wide * 0.02f, y, wide * 0.13f, row), "Name:", slotStyle);
                GUI.SetNextControlName("SaveName");
                typed = GUI.TextField(new Rect(left + wide * 0.15f, y + row * 0.18f, wide * 0.4f, row * 0.64f), typed, SaveGame.LongestName, fieldStyle);
                GUI.FocusControl("SaveName");
                bool named = typed.Trim().Length > 0;
                bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
                float nameButton = wide * 0.21f, nameInset = row * 0.14f;
                GUI.enabled = named;
                bool start = GameText.Button(new Rect(left + wide - nameButton * 2f - nameInset * 2f, y + nameInset, nameButton, row - nameInset * 2f), "Start", smallButtonStyle);
                GUI.enabled = true;
                if (named && (start || enter)) Choose(creative, i, null, typed.Trim());
                else if (GameText.Button(new Rect(left + wide - nameButton - nameInset, y + nameInset, nameButton, row - nameInset * 2f), "Cancel", smallButtonStyle)) naming = -1;
                continue;
            }
            // The slot's number and name, with when it was saved on a smaller line underneath.
            string title = (i + 1) + ".  " + (saved == null ? "Empty" : saved.Name != "" ? saved.Name : "Saved game");
            float wordsWide = wide * 0.5f;
            if (saved == null) GameText.Label(new Rect(left + wide * 0.03f, y, wordsWide, row), title, slotStyle);
            else
            {
                GameText.Label(new Rect(left + wide * 0.03f, y + row * 0.06f, wordsWide, row * 0.52f), title, slotStyle);
                GameText.Label(new Rect(left + wide * 0.03f, y + row * 0.56f, wordsWide, row * 0.38f), "Saved " + saved.Saved.ToString("d MMM, h:mm tt"), dateStyle);
            }
            float buttonWide = wide * 0.21f, inset = row * 0.14f;
            var first = new Rect(left + wide - buttonWide * 2f - inset * 2f, y + inset, buttonWide, row - inset * 2f);
            var second = new Rect(left + wide - buttonWide - inset, y + inset, buttonWide, row - inset * 2f);
            if (saved == null)
            {
                if (GameText.Button(second, "New game", smallButtonStyle))
                {
                    naming = i;
                    typed = "";
                    deleting = -1;
                }
                continue;
            }
            if (GameText.Button(first, "Continue", smallButtonStyle))
            {
                SaveGame.Loaded game = SaveGame.Load(creative, i);
                if (game != null) Choose(creative, i, game);
            }
            if (GameText.Button(second, deleting == i ? "Sure?" : "Delete", smallButtonStyle))
            {
                if (deleting == i)
                {
                    SaveGame.Delete(creative, i);
                    slots[i] = null;
                    deleting = -1;
                }
                else deleting = i;
            }
        }
        float backY = top + slots.Length * (row + gap) + gap;
        if (GameText.Button(new Rect(left + wide * 0.7f, backY, wide * 0.3f, row), "Back", smallButtonStyle)) slotsFor = 0;
    }

    // A small picture of a box in one colour with an edge in another, for stretching over a box of any size.
    static Texture2D Framed(Color inside, Color edge)
    {
        const int size = 8;
        var texture = new Texture2D(size, size) { filterMode = FilterMode.Point };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++) texture.SetPixel(x, y, x < 2 || y < 2 || x >= size - 2 || y >= size - 2 ? edge : inside);
        }
        texture.Apply();
        return texture;
    }

    static Texture2D Fill(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    // Leaves the game. In the Unity editor that means stopping play.
    static void Exit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // The planet's lands laid flat, and the same in the dark of its night side.
    Color32[] landMap, nightMap;
    // For each pixel of the ball: where it is in the picture, which row of the map it shows and how far round from
    // the middle of the side facing us (in map pixels), how much sunlight reaches it and how much of the rim of air
    // shows there (both out of 256).
    int[] ballPixel, ballRow, ballSun, ballAir;
    float[] ballRound;
    Texture2D planetPicture;
    Color32[] planetPixels;
    // How far the planet has turned, in whole turns.
    float spin;

    // Works out the planet's lands as a flat map, and for every pixel of the ball what part of the map it shows.
    // North is up.
    void MapPlanet()
    {
        landMap = new Color32[MapWide * MapTall];
        nightMap = new Color32[MapWide * MapTall];
        for (int row = 0; row < MapTall; row++)
        {
            float latitude = (row + 0.5f) / MapTall * 180f - 90f;
            float up = latitude * Mathf.Deg2Rad * BlobsRound / (Mathf.PI * 2f);
            for (int column = 0; column < MapWide; column++)
            {
                float around = column * (float)BlobsRound / MapWide;
                Color land;
                if (latitude > NorthIceFrom) land = Ice;
                else if (latitude > MountainsFrom) land = Noise(around * 2.4f, up * 2.4f, 31, 84) > 0.6f ? Peak : Noise(around * 1.6f, up * 1.6f, 47, 56) > 0.36f ? Rock : Snow;
                else if (latitude < SouthSnowFrom) land = Noise(around * 2f, up * 2f, 59, 70) > 0.72f ? Ice : Snow;
                else if (latitude < OceanFrom + (Noise(around, 0f, 67, BlobsRound) - 0.5f) * 7f) land = Water;
                else
                {
                    // The lowlands: snow, lakes of ice and grey rock, as on the ground.
                    float e = Noise(around, up, 11, BlobsRound) * 0.65f + Noise(around * 2.6f, up * 2.6f, 23, 91) * 0.35f;
                    land = e > 0.6f ? Rock : e < 0.42f ? Ice : Snow;
                    // The algae, round its middle on the equator, with a wavy edge, in the colours it has on the map.
                    float across = Mathf.Abs(column / (float)MapWide - AlgaeRound);
                    across = Mathf.Min(across, 1f - across) * 360f;
                    float edge = Mathf.Sqrt(across * across + latitude * latitude) / (AlgaeReach * (0.75f + 0.5f * Noise(around * 3f, up * 3f, 101, 105)));
                    if (edge < 1f && e <= 0.6f) land = PlanetOne.AlgaeMapColor(Mathf.InverseLerp(0.25f, 0.75f, Noise(around * 4f, up * 4f, 113, 140)), row % 3 == 0, edge);
                }
                landMap[row * MapWide + column] = land;
                nightMap[row * MapWide + column] = Color.Lerp(Night, land, 0.1f);
            }
        }

        var pixel = new System.Collections.Generic.List<int>();
        var rows = new System.Collections.Generic.List<int>();
        var round = new System.Collections.Generic.List<float>();
        var sunlit = new System.Collections.Generic.List<int>();
        var air = new System.Collections.Generic.List<int>();
        float half = PlanetPixels / 2f;
        for (int y = 0; y < PlanetPixels; y++)
        {
            for (int x = 0; x < PlanetPixels; x++)
            {
                // The picture's rows count up from the bottom.
                float nx = (x + 0.5f - half) / half, ny = (y + 0.5f - half) / half;
                float fromMiddle = nx * nx + ny * ny;
                if (fromMiddle > 1f) continue;
                float nz = Mathf.Sqrt(1f - fromMiddle);
                pixel.Add(y * PlanetPixels + x);
                rows.Add(Mathf.Clamp((int)((Mathf.Asin(ny) * Mathf.Rad2Deg + 90f) / 180f * MapTall), 0, MapTall - 1));
                round.Add(Mathf.Atan2(nx, nz) / (Mathf.PI * 2f) * MapWide);
                // Day on the side facing the sun, night on the other, with a soft line between. A thin rim of air at the edge.
                float sunlight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.3f, nx * ToSun.x + ny * ToSun.y + nz * ToSun.z));
                sunlit.Add(Mathf.RoundToInt(sunlight * 256f));
                air.Add(fromMiddle > 0.93f ? Mathf.RoundToInt(0.4f * sunlight * Mathf.InverseLerp(0.93f, 1f, fromMiddle) * 256f) : 0);
            }
        }
        ballPixel = pixel.ToArray();
        ballRow = rows.ToArray();
        ballRound = round.ToArray();
        ballSun = sunlit.ToArray();
        ballAir = air.ToArray();
    }

    // Paints the ball as it is now, turned as far as it has spun. The lands move from west to east; the sunlight
    // stays where it is.
    void PaintPlanet()
    {
        Color32 airColour = Air;
        float turned = Mathf.Repeat(spin, 1f) * MapWide;
        for (int i = 0; i < ballPixel.Length; i++)
        {
            int column = (int)(ballRound[i] - turned + MapWide * 2) % MapWide;
            int at = ballRow[i] * MapWide + column;
            Color32 day = landMap[at], dark = nightMap[at];
            int light = ballSun[i], rim = ballAir[i];
            int r = dark.r + ((day.r - dark.r) * light >> 8), g = dark.g + ((day.g - dark.g) * light >> 8), b = dark.b + ((day.b - dark.b) * light >> 8);
            if (rim > 0)
            {
                r += (airColour.r - r) * rim >> 8;
                g += (airColour.g - g) * rim >> 8;
                b += (airColour.b - b) * rim >> 8;
            }
            planetPixels[ballPixel[i]] = new Color32((byte)r, (byte)g, (byte)b, 255);
        }
        planetPicture.SetPixels32(planetPixels);
        planetPicture.Apply();
    }

    // Smooth blobs of noise, from 0 to 1, which repeat after a number of blobs across so the map joins up.
    static float Noise(float x, float y, int seed, int period)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
        return Mathf.Lerp(Mathf.Lerp(Hash(x0 % period, y0, seed), Hash((x0 + 1) % period, y0, seed), sx), Mathf.Lerp(Hash(x0 % period, y0 + 1, seed), Hash((x0 + 1) % period, y0 + 1, seed), sx), sy);
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
}
