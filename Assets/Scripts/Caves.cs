using UnityEngine;

// The frost caves. Their mouths are dark holes with icicles, high in the cliffs of the mountains round the poles,
// on ledges that cannot be reached without digging or building. Walking into one fades the screen to black and
// then into the cave: stone tunnels and rooms, seen without a roof, zoomed in twice as close, with the mini map
// dark. "FROST CAVE" comes up across the screen and the cave is marked on the maps. Walking back into the hole the
// player came in by takes them back out onto the ledge.
//
// A cave is a world of its own (see PlanetOne), so digging and building in it does not change the land above.
public class Caves : MonoBehaviour
{
    // How long the screen takes to go black, and to clear again.
    const float FadeSeconds = 0.6f;
    // How long the name of the cave stays up, and how long it then takes to fade.
    const float TitleSeconds = 3f, TitleFadeSeconds = 1f;
    // How much of the world the camera shows: half as much in a cave, which zooms in twice as close.
    const float SurfaceZoom = 7f, CaveZoom = 3.5f;
    // The hole is painted with this many pixels a block, this many wide and tall.
    const int HolePixels = 32, HoleWide = 28, HoleTall = 32;
    // The hole is only drawn when the player is within this many squares of it.
    const float ShowWithin = 60f;

    static readonly Color32 HoleDark = new Color32(6, 8, 15, 255), HoleEdge = new Color32(18, 24, 38, 255);
    static readonly Color32 RimStone = new Color32(150, 170, 192, 255), RimFrost = new Color32(226, 238, 248, 255);
    static readonly Color32 Icicle = new Color32(196, 230, 248, 255), IcicleLight = new Color32(240, 250, 255, 255);
    static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    // Where the icicles hang across the top of the hole, from its middle, in pixels, and how long each is.
    static readonly int[] IcicleAt = { -8, -5, -2, 1, 4, 7 };
    static readonly int[] IcicleLength = { 4, 7, 3, 8, 5, 4 };

    PlanetOne planet;
    PlayerController player;
    MiningMode mining;
    Camera cam;
    DayNight dayNight;
    Gatherables gatherables;
    Spiders spiders;
    Pickups pickups;
    WorldMap map;

    Texture2D holePicture;
    readonly System.Collections.Generic.Dictionary<int, Sprite> topDownHoles = new System.Collections.Generic.Dictionary<int, Sprite>();
    SpriteRenderer topDownHole, sideHole;
    // False while the player is standing in the hole they have just come out of, so they do not go straight back in.
    bool armed = true;
    // The fade going into or out of a cave, if one is under way: when it started, which land it goes to, and
    // whether the land has been swapped yet (it is, when the screen is fully black).
    float fadeStartedAt = -1f;
    int fadeTo;
    bool swapped;
    float titleAt = -100f;
    GUIStyle titleStyle;

    public void Setup(PlanetOne planet, PlayerController player, MiningMode mining, Camera cam, DayNight dayNight, Gatherables gatherables, Spiders spiders, Pickups pickups, WorldMap map)
    {
        this.planet = planet;
        this.player = player;
        this.mining = mining;
        this.cam = cam;
        this.dayNight = dayNight;
        this.gatherables = gatherables;
        this.spiders = spiders;
        this.pickups = pickups;
        this.map = map;

        Sprite side = PlanetOne.PaintSprite(HoleWide, HoleTall, new Vector2(0.5f, 0f), HolePixels, PaintHole);
        holePicture = side.texture;
        topDownHole = MakeHole("Cave Hole", planet.TopDownRoot, side);
        topDownHole.spriteSortPoint = SpriteSortPoint.Pivot;
        sideHole = MakeHole("Cave Hole (side)", mining.Root, side);
    }

    static SpriteRenderer MakeHole(string name, Transform parent, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.enabled = false;
        return renderer;
    }

    // The top-down picture of the hole for a ledge at a level. It is sorted at the foot of the cliff the hole is
    // in, just behind the ledge, but drawn as high up the screen as the ledge is.
    Sprite TopDownHole(int level)
    {
        if (topDownHoles.TryGetValue(level, out Sprite sprite)) return sprite;
        float lift = level * PlanetOne.HeightScale - 0.01f;
        sprite = Sprite.Create(holePicture, new Rect(0, 0, HoleWide, HoleTall), new Vector2(0.5f, -lift * HolePixels / HoleTall), HolePixels);
        topDownHoles[level] = sprite;
        return sprite;
    }

    // The hole the player could walk into from here: in a cave, the way out; up top, the nearest cave mouth.
    // Gives its square (the one in front of the hole, the copy nearest the player) and the level it is at.
    bool NearestHole(out Vector2Int square, out int level)
    {
        if (planet.InCave)
        {
            square = PlanetOne.CaveArrival;
            level = 0;
            return true;
        }
        square = default;
        level = 0;
        float best = float.MaxValue;
        Vector2 at = player.GroundPosition;
        for (int i = 0; i < planet.CaveMouths.Length; i++)
        {
            Vector2 near = planet.Nearest(planet.CaveMouths[i] + new Vector2(0.5f, 0.5f), at);
            float away = Vector2.Distance(near, at);
            if (away >= best) continue;
            best = away;
            square = Vector2Int.FloorToInt(near);
            level = planet.CaveMouthLevels[i];
        }
        return best < ShowWithin;
    }

    void Update()
    {
        bool near = NearestHole(out Vector2Int square, out int level);
        ShowHole(near, square, level);

        if (fadeStartedAt >= 0f)
        {
            float t = Time.time - fadeStartedAt;
            if (!swapped && t >= FadeSeconds)
            {
                Go(fadeTo);
                swapped = true;
            }
            if (t >= FadeSeconds * 2f)
            {
                fadeStartedAt = -1f;
                player.Frozen = false;
            }
            return;
        }

        // Walking into the hole: being in its square, at the height of its ledge.
        bool inHole = near && Vector2Int.FloorToInt(player.GroundPosition) == square && Mathf.Abs(player.FeetHeight - level) < 0.6f;
        if (inHole && armed) Begin(planet.InCave ? -1 : MouthIndexAt(square));
        armed = !inHole;
    }

    // Which cave mouth a square is, on whichever copy of the planet it was found.
    int MouthIndexAt(Vector2Int square)
    {
        Vector2Int onPlanet = planet.Wrap(square);
        for (int i = 0; i < planet.CaveMouths.Length; i++)
        {
            if (planet.CaveMouths[i] == onPlanet) return i;
        }
        return -1;
    }

    // Draws the hole: in the top-down view on the cliff behind its square, and from the side in the row view, in the
    // cliff of the row just behind. Looking along a column, the hole faces the other way and cannot be seen.
    void ShowHole(bool near, Vector2Int square, int level)
    {
        topDownHole.enabled = near && !mining.Active;
        if (topDownHole.enabled)
        {
            topDownHole.sprite = TopDownHole(level);
            topDownHole.transform.position = new Vector3(square.x + 0.5f, square.y + 1.01f, 0f);
        }
        sideHole.enabled = near && mining.Active && !mining.Column && mining.Slice == square.y;
        if (sideHole.enabled)
        {
            sideHole.transform.position = new Vector3(square.x + 0.5f, level + MiningMode.BehindLift(1), 0f);
            sideHole.sortingOrder = MiningMode.BehindThingOrder(1);
            sideHole.color = mining.BehindShade(1);
        }
    }

    // Starts the fade into a cave (its number) or back out (-1).
    void Begin(int to)
    {
        if (!planet.InCave && to < 0) return;
        fadeTo = to;
        fadeStartedAt = Time.time;
        swapped = false;
        player.Frozen = true;
        gatherables.CancelWalk();
    }

    // Leaves a cave straight away, with no fade: for when the player falls in one and wakes up at the ship.
    public void LeaveNow()
    {
        if (fadeStartedAt >= 0f)
        {
            fadeStartedAt = -1f;
            player.Frozen = false;
        }
        if (planet.InCave) Go(-1);
    }

    // Swaps the land for a cave's, or back to the surface, and puts the player at the hole they came through.
    void Go(int to)
    {
        int from = planet.CaveIndex;
        if (mining.Active) mining.Toggle();
        planet.UseLand(to);
        bool underground = to >= 0;
        gatherables.SetUnderground(underground);
        spiders.Underground = underground;
        pickups.SetLand(to);
        dayNight.Underground = underground;
        cam.orthographicSize = underground ? CaveZoom : SurfaceZoom;
        if (underground)
        {
            Vector2 mouth = planet.CaveMouths[to] + new Vector2(0.5f, 0.5f);
            map.CaveMouth = mouth;
            map.MarkCave(mouth);
            Vector2Int arrival = PlanetOne.CaveArrival;
            player.Respawn(new Vector2(arrival.x + 0.5f, arrival.y + 0.75f));
            titleAt = Time.time;
        }
        else
        {
            map.CaveMouth = null;
            Vector2Int mouth = planet.CaveMouths[from];
            player.Respawn(new Vector2(mouth.x + 0.5f, mouth.y + 0.75f));
        }
        armed = false;
    }

    void OnGUI()
    {
        // Drawn over everything else on screen.
        GUI.depth = -10;
        var screen = new Rect(0f, 0f, Screen.width, Screen.height);

        float sinceTitle = Time.time - titleAt;
        if (sinceTitle < TitleSeconds + TitleFadeSeconds)
        {
            if (titleStyle == null) titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.fontSize = Mathf.RoundToInt(Screen.height / 7f);
            // Fades in with the cave, holds, then fades away.
            float alpha = Mathf.Clamp01(sinceTitle / FadeSeconds) * (1f - Mathf.Clamp01((sinceTitle - TitleSeconds) / TitleFadeSeconds));
            var line = new Rect(0f, Screen.height * 0.18f, Screen.width, Screen.height * 0.2f);
            float shadow = Mathf.Max(2f, Screen.height / 220f);
            GUI.color = new Color(0.02f, 0.03f, 0.08f, alpha * 0.85f);
            GUI.Label(new Rect(line.x + shadow, line.y + shadow, line.width, line.height), "FROST CAVE", titleStyle);
            GUI.color = new Color(0.86f, 0.95f, 1f, alpha);
            GUI.Label(line, "FROST CAVE", titleStyle);
        }

        if (fadeStartedAt >= 0f)
        {
            float t = Time.time - fadeStartedAt;
            float dark = t < FadeSeconds ? t / FadeSeconds : 1f - Mathf.Clamp01((t - FadeSeconds) / FadeSeconds);
            GUI.color = new Color(0f, 0f, 0f, dark);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }

    // One pixel of the hole, with (0, 0) at the top left: a dark arch, a rim of frosted stone, and icicles hanging
    // down inside from the top of the arch.
    static Color32 PaintHole(int x, int y)
    {
        float across = x + 0.5f - HoleWide / 2f, up = HoleTall - 1 - y + 0.5f;
        const float straight = 17f, inner = 10f, outer = 12.5f;
        bool inside = InArch(across, up, inner, straight);
        if (!InArch(across, up, outer, straight)) return Clear;
        if (!inside) return up > straight + 3f ? RimFrost : RimStone;

        // The icicles hang from the top of the opening.
        float roof = straight + Mathf.Sqrt(Mathf.Max(0f, inner * inner - across * across));
        for (int i = 0; i < IcicleAt.Length; i++)
        {
            float middle = IcicleAt[i] + 0.5f, down = roof - up;
            if (down < 0f || down > IcicleLength[i]) continue;
            float halfWidth = 1.3f * (1f - down / IcicleLength[i]) + 0.2f;
            float off = across - middle;
            if (Mathf.Abs(off) > halfWidth) continue;
            return off < 0f ? IcicleLight : Icicle;
        }
        // Dark inside, a touch lighter just inside the edge.
        bool edge = !InArch(across, up, inner - 1.5f, straight);
        return edge ? HoleEdge : HoleDark;
    }

    // True inside an arch: straight sides this far either side of the middle, up to a height, and a round top.
    static bool InArch(float across, float up, float half, float straight)
    {
        if (Mathf.Abs(across) > half || up < 0f) return false;
        if (up <= straight) return true;
        float over = up - straight;
        return across * across + over * over <= half * half;
    }
}
