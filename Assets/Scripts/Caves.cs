using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
    // A hole is only drawn when the player is within this many squares of it.
    const float ShowWithin = 60f;

    // The darkness inside the opening: nearly black, and a touch lighter along the floor.
    static readonly Color HoleDark = new Color32(4, 6, 12, 255), HoleFloor = new Color32(11, 15, 26, 255);
    // Icicles hang in the opening: Taivan's icicle, upside down. The big ones are this tall, in blocks as drawn (the
    // opening is one and a half tall), the middling ones and the small ones this tall.
    const float BigIcicle = 0.62f, MiddleIcicle = 0.4f, SmallIcicle = 0.22f;

    PlanetOne planet;
    PlayerController player;
    MiningMode mining;
    Camera cam;
    DayNight dayNight;
    Gatherables gatherables;
    Spiders spiders;
    Pickups pickups;
    WorldMap map;

    // What is drawn over the opening of the nearest hole: its darkness and icicles, put in order as one thing just in
    // front of the cliff. It is made again when a different hole is the nearest.
    GameObject mouth;
    PlanetOne.CaveHole mouthOf;
    Vector2Int mouthOffset;
    Sprite icicle;
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
        icicle = Gatherables.IcicleSprite();
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
        bool through = inTunnel && square.y - offset.y >= hole.Inside.y;
        if (through && armed) Begin(planet.InCave ? -1 : System.Array.IndexOf(planet.CaveHoles, hole));
        armed = !through;
    }

    // The opening of a hole seen from above is dark inside, with icicles hanging from its top: a few big ones and
    // some smaller. From the side the dug-out blocks are simply missing, so nothing more is drawn. While the player
    // is in the tunnel the darkness goes, so it does not hide them.
    void ShowDarkness(bool show, PlanetOne.CaveHole hole, Vector2Int offset)
    {
        if (!show)
        {
            if (mouth != null) mouth.SetActive(false);
            return;
        }
        if (mouth == null || hole != mouthOf || offset != mouthOffset) BuildMouth(hole, offset);
        mouth.SetActive(true);
    }

    void BuildMouth(PlanetOne.CaveHole hole, Vector2Int offset)
    {
        if (mouth != null) Destroy(mouth);
        mouthOf = hole;
        mouthOffset = offset;
        // Sorted as one thing at the foot of the cliff, just behind the ledge: over the cliff, under the player.
        mouth = new GameObject("Cave Mouth");
        mouth.transform.SetParent(planet.TopDownRoot, false);
        mouth.transform.position = new Vector3(hole.Front.x + offset.x + 1f, hole.Front.y + offset.y + 1.01f, 0f);
        mouth.AddComponent<SortingGroup>();

        // The top of each column of the opening, for the icicles to hang from.
        var tops = new SortedDictionary<int, int>();
        foreach (Vector3Int block in hole.Opening)
        {
            // The side of one block shows from above as a block wide and half a block tall.
            SpriteRenderer dark = MakePart("Darkness", planet.Square, 0);
            dark.color = block.z == hole.Level ? HoleFloor : HoleDark;
            dark.transform.position = new Vector3(block.x + offset.x + 0.5f, block.y + offset.y + (block.z + 0.5f) * PlanetOne.HeightScale, 0f);
            dark.transform.localScale = new Vector3(1f, PlanetOne.HeightScale, 1f);
            if (!tops.TryGetValue(block.x, out int top) || block.z > top) tops[block.x] = block.z;
        }
        if (icicle == null || tops.Count == 0) return;

        // The same icicles every time for the same hole. The tallest part of the opening gets the big ones.
        var rand = new System.Random(hole.Front.x * 7919 + hole.Front.y * 104729 + hole.Level);
        int highest = int.MinValue;
        foreach (int top in tops.Values) highest = Mathf.Max(highest, top);
        foreach (KeyValuePair<int, int> column in tops)
        {
            float y = hole.Front.y + offset.y + 1f + (column.Value + 1) * PlanetOne.HeightScale;
            bool tall = column.Value == highest;
            // One larger icicle and two smaller ones to a block, spread across it.
            float[] sizes = { tall ? BigIcicle : MiddleIcicle, SmallIcicle, tall ? MiddleIcicle * 0.8f : SmallIcicle * 0.8f };
            float[] across = { 0.3f + (float)rand.NextDouble() * 0.4f, 0.08f + (float)rand.NextDouble() * 0.14f, 0.78f + (float)rand.NextDouble() * 0.14f };
            for (int i = 0; i < sizes.Length; i++)
            {
                float size = sizes[i] * (0.85f + (float)rand.NextDouble() * 0.3f) / Gatherables.IcicleShare;
                SpriteRenderer hanging = MakePart("Icicle", icicle, 1);
                hanging.flipY = true;
                hanging.transform.position = new Vector3(column.Key + offset.x + across[i], y, 0f);
                hanging.transform.localScale = new Vector3(size, size, 1f);
            }
        }
    }

    SpriteRenderer MakePart(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(mouth.transform, true);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
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

    // Goes into a cave straight away, with no fade: for a saved game that was saved in one.
    public void EnterNow(int cave)
    {
        if (cave >= 0 && cave < planet.CaveHoles.Length && !planet.InCave) Go(cave);
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
        // A cave is seen twice as close as the surface, whatever the zoom is set to.
        cam.orthographicSize = underground ? GameSettings.Zoom / 2f : GameSettings.Zoom;
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
}
