using System.Collections.Generic;
using UnityEngine;

// Purple crystals in the frost caves (Taivan's pictures, Assets/Resources/Art/CaveCrystals: three of them side by side,
// each one square wide and two tall). About forty grow on the floor of each cave, scattered through its tunnels and
// rooms, and nothing walks through them. Each gives off light: the caves are dark, and round every crystal a soft
// purple light reaches about four squares. They are mined with the ice pickaxe, eight hits each, and drop nothing yet.
// A crystal mined stays gone, in saved games too. They are seen and mined in the top-down view.
public class CaveCrystals : MonoBehaviour
{
    // About this many crystals grow in each cave, at least this many squares apart, and none this close to where the
    // player comes in.
    const int PerCave = 40, Apart = 4, ClearOfArrival = 5;
    // How many hits of the ice pickaxe break one, how close the player must be, and how often holding the button hits.
    const int Hits = 8;
    const float Reach = 3.5f, HeldHitSeconds = 0.4f;
    // How dark the caves are, as how much of the view the dark covers (0 none, 1 black), how far a crystal's light
    // reaches in squares, and the colour of its light.
    const float Darkness = 0.72f, LightReach = 4f;
    static readonly Color Dark = new Color(0.02f, 0.02f, 0.06f), Glow = new Color(0.62f, 0.38f, 0.92f);
    // How much of the light's colour is washed over the ground near a crystal.
    const float GlowWash = 0.16f;
    // The dark is worked out on a grid this many points to a square, smoothed between them.
    const int DarkDetail = 4;

    PlanetOne planet;
    PlayerController player;
    MiningMode mining;
    Inventory inventory;
    CraftingNook nook;
    Hud hud;
    Camera cam;

    Sprite[] pictures;
    Sprite[] crackSprites;
    // The crystals of the cave the player is in: where each stands, and its picture.
    readonly Dictionary<Vector2Int, SpriteRenderer> shown = new Dictionary<Vector2Int, SpriteRenderer>();
    int shownCave = -1;
    // The crystals mined, in every cave: the cave's number, then the square.
    readonly HashSet<(int cave, Vector2Int cell)> mined = new HashSet<(int, Vector2Int)>();

    // The crystal being worked on and how many times it has been hit.
    Vector2Int damaged;
    int hits;
    float nextHeldHit;
    SpriteRenderer cracks;

    // The dark over the cave, with holes of light round the crystals.
    SpriteRenderer dark;
    Texture2D darkTexture;
    Color32[] darkPixels;

    public void Setup(PlanetOne planet, PlayerController player, MiningMode mining, Inventory inventory, CraftingNook nook, Hud hud, Camera cam)
    {
        this.planet = planet;
        this.player = player;
        this.mining = mining;
        this.inventory = inventory;
        this.nook = nook;
        this.hud = hud;
        this.cam = cam;
        pictures = CutSheet("Art/CaveCrystals", true);
        crackSprites = CutSheet("Art/BlockCracks", false);

        var crackObject = new GameObject("Crystal Cracks");
        crackObject.transform.SetParent(planet.TopDownRoot, false);
        cracks = crackObject.AddComponent<SpriteRenderer>();
        cracks.sortingOrder = 99;
        cracks.enabled = false;

        // The dark rides along with the camera, over everything in the cave.
        var darkObject = new GameObject("Cave Darkness");
        darkObject.transform.SetParent(cam.transform, false);
        darkObject.transform.localPosition = new Vector3(0f, 0f, 10f);
        dark = darkObject.AddComponent<SpriteRenderer>();
        dark.sortingOrder = 100;
        dark.enabled = false;

        // Nothing walks through a crystal.
        System.Func<int, int, bool> before = planet.Obstacle;
        planet.Obstacle = (x, y) => (before != null && before(x, y)) || (planet.InCave && shown.ContainsKey(new Vector2Int(x, y)));
    }

    // Cuts a picture of square frames, or of frames twice as tall as they are wide, into its frames, unchanged and
    // drawn sharp. Tall ones stand on their lowest row; square ones are centred. Null if the picture is missing.
    static Sprite[] CutSheet(string path, bool tall)
    {
        var sheet = Resources.Load<Texture2D>(path);
        if (sheet == null) return null;
        Color32[] pixels = PlanetOne.ReadPixels(sheet);
        int high = sheet.height, wide = tall ? high / 2 : high, count = sheet.width / wide;
        var frames = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            var cut = new Color32[wide * high];
            int lowest = high;
            for (int y = 0; y < high; y++)
            {
                for (int x = 0; x < wide; x++)
                {
                    Color32 c = pixels[y * sheet.width + i * wide + x];
                    cut[y * wide + x] = c;
                    if (c.a > 0) lowest = Mathf.Min(lowest, y);
                }
            }
            var texture = new Texture2D(wide, high, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(cut);
            texture.Apply();
            var pivot = tall ? new Vector2(0.5f, Mathf.Min(lowest, high - 1) / (float)high) : new Vector2(0.5f, 0.5f);
            frames[i] = Sprite.Create(texture, new Rect(0, 0, wide, high), pivot, wide);
        }
        return frames;
    }

    void Update()
    {
        if (pictures == null) return;
        int cave = planet.InCave ? planet.CaveIndex : -1;
        if (cave != shownCave) ShowCave(cave);
        bool topDown = planet.InCave && !mining.Active;
        foreach (SpriteRenderer crystal in shown.Values) crystal.enabled = topDown;
        if (!topDown) cracks.enabled = false;
        ShowDark(topDown);
        if (!topDown) return;

        // A click on a crystal with the pickaxe is one hit. Holding the button keeps hitting, at a steady pace.
        bool pressed = GameInput.LeftClickPressed();
        bool held = !pressed && GameInput.LeftClickHeld() && Time.time >= nextHeldHit;
        if ((!pressed && !held) || inventory.Equipped != nook.Pickaxe || GameInput.Blocked) return;
        if (!CrystalUnder(cam.ScreenToWorldPoint(GameInput.PointerPosition()), out Vector2Int cell)) return;
        nextHeldHit = Time.time + HeldHitSeconds;
        if (Vector2.Distance(player.GroundPosition, new Vector2(cell.x + 0.5f, cell.y + 0.5f)) > Reach)
        {
            if (pressed) hud.Say("Get closer to mine it", 1.5f);
            return;
        }
        Hit(cell);
    }

    // Puts up the crystals of the cave the player has gone into, or takes them all away when they come out.
    void ShowCave(int cave)
    {
        foreach (SpriteRenderer crystal in shown.Values) Destroy(crystal.gameObject);
        shown.Clear();
        hits = 0;
        cracks.enabled = false;
        shownCave = cave;
        if (cave < 0) return;
        foreach (Vector2Int cell in GrowSpots(cave))
        {
            if (mined.Contains((cave, cell))) continue;
            var go = new GameObject("Cave Crystal");
            go.transform.SetParent(planet.TopDownRoot, false);
            go.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.3f, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = pictures[(int)((uint)(cell.x * 73856093 ^ cell.y * 19349663 ^ cave * 83492791) % (uint)pictures.Length)];
            // Sorted by its foot, so the player walks in front of or behind it correctly.
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            shown[cell] = renderer;
        }
    }

    // Where the crystals grow in a cave: always the same squares for the same cave of the same world. They stand on
    // open floor, apart from one another, out of the way of where the player comes in.
    List<Vector2Int> GrowSpots(int cave)
    {
        var floor = new List<Vector2Int>();
        int size = PlanetOne.CaveSize;
        Vector2Int arrival = PlanetOne.CaveArrival;
        for (int y = 1; y < size - 1; y++)
        {
            for (int x = 1; x < size - 1; x++)
            {
                if (planet.TopAt(x, y) != 0 || planet.TopAt(x, y + 1) != 0) continue;
                if (Mathf.Abs(x - arrival.x) < ClearOfArrival && Mathf.Abs(y - arrival.y) < ClearOfArrival) continue;
                floor.Add(new Vector2Int(x, y));
            }
        }
        var rand = new System.Random(planet.seed * 41 + cave * 1009 + 17);
        var spots = new List<Vector2Int>();
        for (int tries = 0; tries < PerCave * 30 && spots.Count < PerCave && floor.Count > 0; tries++)
        {
            Vector2Int spot = floor[rand.Next(floor.Count)];
            bool crowded = false;
            foreach (Vector2Int other in spots)
            {
                if (Mathf.Abs(other.x - spot.x) < Apart && Mathf.Abs(other.y - spot.y) < Apart) { crowded = true; break; }
            }
            if (!crowded) spots.Add(spot);
        }
        return spots;
    }

    // The crystal whose picture is under a point, if there is one.
    bool CrystalUnder(Vector2 point, out Vector2Int cell)
    {
        foreach (KeyValuePair<Vector2Int, SpriteRenderer> crystal in shown)
        {
            Bounds bounds = crystal.Value.bounds;
            if (point.x < bounds.min.x || point.x > bounds.max.x || point.y < bounds.min.y || point.y > bounds.max.y) continue;
            cell = crystal.Key;
            return true;
        }
        cell = default;
        return false;
    }

    // For the pickaxe: true if a click there is on a crystal, so it is not taken as a click on the ground.
    public bool TakesClick(Vector2 point)
    {
        return planet.InCave && !mining.Active && CrystalUnder(point, out _);
    }

    // One hit of the pickaxe. Cracks spread across it, and the last hit breaks it.
    public void Hit(Vector2Int cell)
    {
        if (!shown.TryGetValue(cell, out SpriteRenderer crystal)) return;
        if (hits == 0 || cell != damaged) hits = 0;
        damaged = cell;
        hits++;
        if (hits < Hits)
        {
            if (crackSprites != null)
            {
                int last = crackSprites.Length - 1;
                cracks.sprite = crackSprites[Mathf.Clamp(Mathf.RoundToInt((hits - 1) * last / (float)(Hits - 2)), 0, last)];
                cracks.transform.position = crystal.bounds.center;
                cracks.enabled = true;
            }
            return;
        }
        hits = 0;
        cracks.enabled = false;
        mined.Add((planet.CaveIndex, cell));
        Destroy(crystal.gameObject);
        shown.Remove(cell);
    }

    // The dark over the cave, with the crystals' light in it. Worked out afresh each frame for what is on screen.
    void ShowDark(bool show)
    {
        dark.enabled = show;
        if (!show) return;
        float halfTall = cam.orthographicSize + 1f, halfWide = halfTall * cam.aspect + 1f;
        int wide = Mathf.CeilToInt(halfWide * 2f * DarkDetail), tall = Mathf.CeilToInt(halfTall * 2f * DarkDetail);
        if (darkTexture == null || darkTexture.width != wide || darkTexture.height != tall)
        {
            darkTexture = new Texture2D(wide, tall, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            darkPixels = new Color32[wide * tall];
            dark.sprite = Sprite.Create(darkTexture, new Rect(0, 0, wide, tall), new Vector2(0.5f, 0.5f), DarkDetail);
        }
        // The lights near enough to reach the screen: each crystal's middle.
        Vector2 middle = cam.transform.position;
        var lights = new List<Vector2>();
        foreach (SpriteRenderer crystal in shown.Values)
        {
            Vector2 at = crystal.bounds.center;
            if (Mathf.Abs(at.x - middle.x) < halfWide + LightReach && Mathf.Abs(at.y - middle.y) < halfTall + LightReach) lights.Add(at);
        }
        for (int py = 0; py < tall; py++)
        {
            for (int px = 0; px < wide; px++)
            {
                var point = new Vector2(middle.x - halfWide + (px + 0.5f) / DarkDetail, middle.y - halfTall + (py + 0.5f) / DarkDetail);
                float light = 0f;
                foreach (Vector2 at in lights)
                {
                    float away = Vector2.Distance(point, at) / LightReach;
                    if (away < 1f) light = Mathf.Max(light, 1f - Mathf.SmoothStep(0f, 1f, away));
                }
                Color c = Color.Lerp(Dark, Glow, light);
                c.a = Mathf.Lerp(Darkness, GlowWash, light) * (1f - light * 0.3f);
                darkPixels[py * wide + px] = c;
            }
        }
        darkTexture.SetPixels32(darkPixels);
        darkTexture.Apply();
    }

    // For saved games: which crystals have been mined.
    public void Write(System.IO.BinaryWriter w)
    {
        w.Write(mined.Count);
        foreach ((int cave, Vector2Int cell) in mined)
        {
            w.Write(cave);
            w.Write(cell.x);
            w.Write(cell.y);
        }
    }

    public void Read(System.IO.BinaryReader r)
    {
        mined.Clear();
        int count = r.ReadInt32();
        for (int i = 0; i < count; i++) mined.Add((r.ReadInt32(), new Vector2Int(r.ReadInt32(), r.ReadInt32())));
        shownCave = -2;
    }

    // For checking: how many crystals are in the cave the player is in, and where.
    public int Count => shown.Count;
    public IEnumerable<Vector2Int> Cells => shown.Keys;
}
