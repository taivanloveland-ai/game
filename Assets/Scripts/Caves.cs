using System.Collections.Generic;
using UnityEngine;

// The frost caves. Their mouths are natural holes dug out of real blocks in the cliffs of the mountains round the North
// Pole, dark inside with icicles hanging at the top, on ledges that cannot be reached without digging or building.
// Walking into one fades the screen to black and then into the cave: stone tunnels and rooms, seen without a roof,
// zoomed in twice as close, with the mini map dark. "FROST CAVE" comes up across the screen and the cave is marked
// on the maps. The way out is a hole like it in the cave wall where the player arrives, back onto the ledge.
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
    // The darkness in each dug-out block of the opening is painted with this many pixels a block: a block wide and
    // half a block tall, which is how the side of one block shows from above.
    const int DarkPixels = 32;
    // A hole is only drawn when the player is within this many squares of it.
    const float ShowWithin = 60f;

    static readonly Color32 HoleDark = new Color32(6, 8, 15, 255), HoleDeep = new Color32(12, 16, 27, 255);
    static readonly Color32 Icicle = new Color32(196, 230, 248, 255), IcicleLight = new Color32(240, 250, 255, 255);
    // Where icicles hang along the top of an opening, in pixels from the left of a block, and how long each is.
    static readonly int[] IcicleAt = { 3, 9, 16, 23, 28 };
    static readonly int[] IcicleLength = { 5, 8, 4, 7, 4 };

    PlanetOne planet;
    PlayerController player;
    MiningMode mining;
    Camera cam;
    DayNight dayNight;
    Gatherables gatherables;
    Spiders spiders;
    Pickups pickups;
    WorldMap map;

    // The dark pictures for the opening, by the level of the block and whether icicles hang from its top.
    readonly Dictionary<(int, bool), Sprite> darkSprites = new Dictionary<(int, bool), Sprite>();
    Texture2D darkPicture, iciclePicture;
    readonly List<SpriteRenderer> darkness = new List<SpriteRenderer>();
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
        darkPicture = PlanetOne.PaintSprite(DarkPixels, DarkPixels / 2, new Vector2(0.5f, 0f), DarkPixels, (x, y) => PaintDark(x, y, false)).texture;
        iciclePicture = PlanetOne.PaintSprite(DarkPixels, DarkPixels / 2, new Vector2(0.5f, 0f), DarkPixels, (x, y) => PaintDark(x, y, true)).texture;
    }

    // The darkness for one dug-out block of an opening seen from above, at a level. It is sorted just in front of
    // the square it is dug out of, but drawn as high up the screen as the block is.
    Sprite DarkSprite(int level, bool icicles)
    {
        if (darkSprites.TryGetValue((level, icicles), out Sprite sprite)) return sprite;
        float lift = level * PlanetOne.HeightScale - 0.01f;
        sprite = Sprite.Create(icicles ? iciclePicture : darkPicture, new Rect(0, 0, DarkPixels, DarkPixels / 2), new Vector2(0.5f, -lift * 2f), DarkPixels);
        darkSprites[(level, icicles)] = sprite;
        return sprite;
    }

    // The hole the player could walk into from here: in a cave, the way out; up top, the nearest cave mouth, with how
    // far the copy of it nearest the player is from where it is on the planet.
    bool NearestHole(out PlanetOne.CaveHole hole, out Vector2Int offset)
    {
        offset = Vector2Int.zero;
        hole = planet.CaveExit;
        if (hole != null) return true;
        float best = float.MaxValue;
        Vector2 at = player.GroundPosition;
        foreach (PlanetOne.CaveHole mouth in planet.CaveHoles)
        {
            Vector2 near = planet.Nearest(mouth.Front + new Vector2(0.5f, 0.5f), at);
            float away = Vector2.Distance(near, at);
            if (away >= best) continue;
            best = away;
            hole = mouth;
            offset = Vector2Int.FloorToInt(near) - mouth.Front;
        }
        return hole != null && best < ShowWithin;
    }

    void Update()
    {
        bool near = NearestHole(out PlanetOne.CaveHole hole, out Vector2Int offset);
        Vector2Int square = Vector2Int.FloorToInt(player.GroundPosition);
        bool inTunnel = near && Mathf.Abs(player.FeetHeight - hole.Level) < 0.6f && hole.Squares.Contains(square - offset);
        ShowDarkness(near && !mining.Active && !inTunnel, hole, offset);

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

        // Walking far enough into the tunnel, at the level of its floor, goes through.
        bool through = near && square - offset == hole.Inside && Mathf.Abs(player.FeetHeight - hole.Level) < 0.6f;
        if (through && armed) Begin(planet.InCave ? -1 : System.Array.IndexOf(planet.CaveHoles, hole));
        armed = !through;
    }

    // The opening of a hole seen from above is dark inside, with icicles hanging from its top. From the side the
    // dug-out blocks are simply missing, so nothing more is drawn. While the player is in the tunnel the darkness
    // goes, so it does not hide them.
    void ShowDarkness(bool show, PlanetOne.CaveHole hole, Vector2Int offset)
    {
        int used = 0;
        if (show)
        {
            foreach (Vector3Int block in hole.Opening)
            {
                if (used == darkness.Count)
                {
                    var go = new GameObject("Cave Hole Darkness");
                    go.transform.SetParent(planet.TopDownRoot, false);
                    var renderer = go.AddComponent<SpriteRenderer>();
                    renderer.spriteSortPoint = SpriteSortPoint.Pivot;
                    darkness.Add(renderer);
                }
                SpriteRenderer dark = darkness[used++];
                // Icicles hang from the top of each column of the opening.
                bool top = !hole.Opening.Contains(block + Vector3Int.forward);
                dark.sprite = DarkSprite(block.z, top);
                dark.transform.position = new Vector3(block.x + offset.x + 0.5f, block.y + offset.y + 0.01f, 0f);
                dark.enabled = true;
            }
        }
        for (int i = used; i < darkness.Count; i++) darkness[i].enabled = false;
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

    // One pixel of the darkness in a dug-out block, with (0, 0) at the top left: a little lighter toward the bottom,
    // where the floor of the tunnel catches some light. With icicles, they hang from the top edge.
    static Color32 PaintDark(int x, int y, bool icicles)
    {
        if (icicles)
        {
            for (int i = 0; i < IcicleAt.Length; i++)
            {
                int off = x - IcicleAt[i];
                // Two pixels wide, then one at the tip.
                if (off < 0 || off > 1 || y >= IcicleLength[i]) continue;
                if (off == 1 && y >= IcicleLength[i] - 2) continue;
                return off == 0 ? IcicleLight : Icicle;
            }
        }
        return y >= DarkPixels / 2 - 3 ? HoleDeep : HoleDark;
    }
}
