using System.Collections.Generic;
using UnityEngine;

// The wrecked ship, as Taivan drew it (Assets/Resources/Art/CrashedShip): nose down in the snow, cracked, with red
// sparks, and smoke rising from its grey body. (His second drawing, 2026-10-18; Art/CrashedShipBack, the ship seen
// from behind, is kept for later.) Once it is fixed (at the workbench) a cloud of
// smoke covers it, and when the smoke clears the fixed ship stands there on its tail, purple nose up: his picture of the
// fixed ship turned upright (Art/ShipStanding, made from Art/CrashedShipFixed on 2026-10-18, when he asked for it), with
// light flowing along its green lines. Its purple crystal is a little see-through, showing the grey inside, and the
// player can be seen in there when they sit in it (ShipFlight). It stands over the ship's squares, which nobody walks
// through, in the top-down view.
public class CrashedShip : MonoBehaviour
{
    // How many of his pixels make one square: the wreck is drawn about nine squares wide.
    // (18 until 2026-10-18, when Taivan asked for it 3 times bigger, then 2 times: 9.)
    const float PixelsPerSquare = 9f;
    // The smoke is drawn this many times bigger to go with it.
    const float SmokeScale = 2f;
    // Where the smoke comes from on his picture, in his pixels from the left and from the top: three spots along the
    // top of the wreck's grey body. (Until 2026-10-18 it came from the three thrusters on top of his first picture.)
    static readonly Vector2[] Thrusters = { new Vector2(32f, 8f), new Vector2(41f, 4f), new Vector2(50f, 9f) };
    // A puff of smoke is let go from each thruster this often, rises this fast (squares a second), drifts sideways
    // a little, grows from the first size to the second, and fades away over this many seconds.
    const float PuffEvery = 0.35f, RiseSpeed = 0.7f * SmokeScale, Drift = 0.25f * SmokeScale, PuffSmall = 0.35f * SmokeScale, PuffBig = 1.1f * SmokeScale, PuffSeconds = 2.6f;
    static readonly Color SmokeColor = new Color(0.55f, 0.57f, 0.6f, 0.7f);
    const int Order = 61;
    // The fixed ship's green lines flow: patches of light run along them from west to east, this many of his pixels
    // apart, each this many pixels from its middle to where it fades, moving this many pixels a second.
    const int FlowApart = 24;
    const float FlowHalfLength = 5f, FlowSpeed = 14f;
    static readonly Color32 LineGreen = new Color32(0x96, 0xE2, 0xCC, 255);
    // The grey inside the purple crystal, and the player as seen sitting in there: their colour, their size in his
    // pixels (the player is 0.6 by 0.9 squares), standing on the lowest row of the crystal at least this many pixels wide.
    static readonly Color32 InsideGrey = new Color32(0x6E, 0x6E, 0x72, 255);
    static readonly Color32 FigureColor = new Color32(240, 138, 46, 255);
    const int FigureWide = 5, FigureTall = 8, FloorWide = 10;
    // The cloud when it is fixed: this many puffs, thrown up over this many seconds, each this big and lasting this
    // long. The fixed ship is put in place, hidden in the cloud, this many seconds after it starts.
    // (It was thinner until 2026-10-18, when Taivan asked for it to cover the whole ship completely.)
    const int CloudPuffs = 170;
    const float CloudRising = 0.6f, CloudPuffSmall = 1.8f * SmokeScale, CloudPuffBig = 2.8f * SmokeScale, CloudPuffSeconds = 4.2f, SwapAfter = 1.3f;
    static readonly Color CloudColor = new Color(0.82f, 0.84f, 0.87f, 1f);

    class Puff
    {
        public SpriteRenderer Picture;
        public Vector2 Start, Sideways;
        public float Born;
        // A puff of the cloud when the ship is fixed: bigger, paler, and lasting longer.
        public bool Cloud;
    }

    PlanetOne planet;
    CraftingNook nook;
    MiningMode mining;
    SpriteRenderer ship;
    // A soft shadow on the snow under it, so it looks as if it stands there.
    SpriteRenderer shadow;
    Sprite broken, fixedUp, puffSprite;
    // The fixed ship with the light at each step along its green lines.
    Sprite[] flowing;
    // The standing ship's other layers, drawn over its body: the grey inside, the player in there, and the purple
    // crystal in front of them.
    SpriteRenderer inside, figure, glass;
    // How much of the purple crystal shows, 0 to 1 (1 hides what is inside), and whether the player is in there.
    public float GlassShows = 0.75f;
    public bool Seated;
    // The camera the ship is being looked at through, if not the usual one.
    public Camera Viewer;
    Vector2 foot;
    float nextPuffAt;
    readonly List<Puff> puffs = new List<Puff>();
    // Whether it was fixed when last looked at, and when the cloud began (or -1).
    bool wasFixed;
    float cloudSince = -1f;
    int cloudThrown;

    // Whether the picture is there to be drawn: without it the ship is drawn as blocks, as it used to be.
    public static bool HasPicture => Resources.Load<Texture2D>("Art/CrashedShip") != null;

    public void Setup(PlanetOne planet, CraftingNook nook, MiningMode mining, Vector2 foot)
    {
        this.planet = planet;
        this.nook = nook;
        this.mining = mining;
        this.foot = foot;
        broken = Load("Art/CrashedShip");
        fixedUp = Load("Art/CrashedShipFixed");
        Sprite standing = Load("Art/ShipStanding");
        Sprite[] layers = null;
        if (standing != null)
        {
            fixedUp = standing;
            layers = MakeLayers(standing, out flowing);
        }
        else if (fixedUp != null) flowing = MakeFlow(fixedUp.texture.GetPixels32(), fixedUp);
        if (broken == null) return;
        var go = new GameObject("Crashed Ship");
        go.transform.SetParent(planet.TopDownRoot, false);
        ship = go.AddComponent<SpriteRenderer>();
        ship.sprite = broken;
        // Sorted by its foot, so the player walks in front of it or behind it correctly.
        ship.spriteSortPoint = SpriteSortPoint.Pivot;
        // Its layers are drawn together, and sorted against everything else by its foot.
        go.AddComponent<UnityEngine.Rendering.SortingGroup>();
        if (layers != null)
        {
            inside = Layer(go, "Inside", layers[0], 1);
            figure = Layer(go, "Player Inside", layers[1], 2);
            glass = Layer(go, "Purple Crystal", layers[2], 3);
        }
        var shade = new GameObject("Crashed Ship Shadow");
        shade.transform.SetParent(planet.TopDownRoot, false);
        shadow = shade.AddComponent<SpriteRenderer>();
        shadow.sprite = PlanetOne.PaintSprite(32, 16, new Vector2(0.5f, 0.5f), 32f, (x, y) =>
        {
            float dx = (x + 0.5f - 16f) / 16f, dy = (y + 0.5f - 8f) / 8f, r = dx * dx + dy * dy;
            return r > 1f ? new Color32(0, 0, 0, 0) : new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * (1f - r)));
        });
        shadow.sprite.texture.filterMode = FilterMode.Bilinear;
        shadow.color = new Color(0.05f, 0.1f, 0.2f, 0.28f);
        // Sorted a little behind the ship, so the ship is drawn over it.
        shadow.spriteSortPoint = SpriteSortPoint.Pivot;
        // A round puff of smoke in big square pixels, like his pictures: thicker in the middle, in three steps.
        puffSprite = PlanetOne.PaintSprite(8, 8, new Vector2(0.5f, 0.5f), 8f, (x, y) =>
        {
            float dx = (x + 0.5f - 4f) / 4f, dy = (y + 0.5f - 4f) / 4f, r = dx * dx + dy * dy;
            return r > 1f ? new Color32(0, 0, 0, 0) : new Color32(255, 255, 255, (byte)(r < 0.3f ? 255 : r < 0.65f ? 190 : 120));
        });
        puffSprite.texture.filterMode = FilterMode.Point;
    }

    static SpriteRenderer Layer(GameObject ship, string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(ship.transform, false);
        var layer = go.AddComponent<SpriteRenderer>();
        layer.sprite = sprite;
        layer.sortingOrder = order;
        layer.enabled = false;
        return layer;
    }

    // Whether one of his pixels is the purple crystal.
    static bool IsCrystal(Color32 c) => c.a > 0 && c.r - c.g > 30 && c.b - c.g > 40;

    // Splits the standing ship into its layers: its body (with the light flowing along its green lines, given back in
    // flow), then the grey inside where the crystal is, the player standing in there, and the crystal itself.
    static Sprite[] MakeLayers(Sprite ship, out Sprite[] flow)
    {
        Texture2D source = ship.texture;
        int wide = source.width, tall = source.height;
        Color32[] pixels = source.GetPixels32();
        var clear = new Color32(0, 0, 0, 0);
        var body = new Color32[pixels.Length];
        var inside = new Color32[pixels.Length];
        var player = new Color32[pixels.Length];
        var crystal = new Color32[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            bool purple = IsCrystal(pixels[i]);
            body[i] = purple ? clear : pixels[i];
            inside[i] = purple ? InsideGrey : clear;
            crystal[i] = purple ? pixels[i] : clear;
            player[i] = clear;
        }
        // The player stands on the lowest row of the crystal that is wide enough, in the middle of it, and is only seen
        // through the crystal.
        for (int y = 0; y < tall; y++)
        {
            int count = 0, sum = 0;
            for (int x = 0; x < wide; x++) if (IsCrystal(pixels[y * wide + x])) { count++; sum += x; }
            if (count < FloorWide) continue;
            int middle = Mathf.RoundToInt(sum / (float)count);
            for (int fy = y; fy < Mathf.Min(tall, y + FigureTall); fy++)
            {
                for (int fx = middle - FigureWide / 2; fx < middle - FigureWide / 2 + FigureWide; fx++)
                {
                    if (fx >= 0 && fx < wide && IsCrystal(pixels[fy * wide + fx])) player[fy * wide + fx] = FigureColor;
                }
            }
            break;
        }
        flow = MakeFlow(body, ship);
        return new[] { Picture(inside, ship), Picture(player, ship), Picture(crystal, ship) };
    }

    // Pixels the size of one of his pictures, as a sprite drawn and placed the same way.
    static Sprite Picture(Color32[] pixels, Sprite like)
    {
        Texture2D source = like.texture;
        var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, source.width, source.height), new Vector2(like.pivot.x / source.width, like.pivot.y / source.height), PixelsPerSquare);
    }

    // The fixed ship once for each step of the light along its green lines: his picture, with the green brightened
    // toward white where a patch of light is.
    static Sprite[] MakeFlow(Color32[] pixels, Sprite ship)
    {
        Texture2D source = ship.texture;
        var frames = new Sprite[FlowApart];
        for (int step = 0; step < FlowApart; step++)
        {
            var lit = (Color32[])pixels.Clone();
            for (int i = 0; i < lit.Length; i++)
            {
                if (!lit[i].Equals(LineGreen)) continue;
                float from = Mathf.Repeat(i % source.width - step, FlowApart);
                float light = Mathf.Clamp01(1f - Mathf.Min(from, FlowApart - from) / FlowHalfLength);
                lit[i] = Color32.Lerp(LineGreen, new Color32(240, 255, 250, 255), light * 0.85f);
            }
            frames[step] = Picture(lit, ship);
        }
        return frames;
    }

    // His picture, unchanged and drawn sharp, standing on its lowest row. Null if it is missing.
    static Sprite Load(string path)
    {
        var picture = Resources.Load<Texture2D>(path);
        if (picture == null) return null;
        Color32[] pixels = PlanetOne.ReadPixels(picture);
        // His pixels exactly, sharp, as he drew them. (Rounded-off edges and soft, blurred drawing were tried on
        // 2026-10-18 and taken out.) See Update for how each of his pixels is kept a whole number of the screen's pixels.
        int wide = picture.width, tall = picture.height;
        int lowest = 0;
        while (lowest < tall - 1 && System.Array.FindIndex(pixels, lowest * wide, wide, c => c.a > 0) < 0) lowest++;
        var texture = new Texture2D(wide, tall, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply();
        // Standing on the middle of its lowest rows: for the standing ship, the bottom of its tail rather than the middle
        // of its wings.
        float left = wide, right = 0f;
        for (int y = lowest; y < Mathf.Min(tall, lowest + 6); y++)
        {
            for (int x = 0; x < wide; x++)
            {
                if (pixels[y * wide + x].a == 0) continue;
                left = Mathf.Min(left, x);
                right = Mathf.Max(right, x + 1);
            }
        }
        float middle = path == "Art/ShipStanding" ? (left + right) / 2f / wide : 0.5f;
        return Sprite.Create(texture, new Rect(0, 0, wide, tall), new Vector2(middle, lowest / (float)picture.height), PixelsPerSquare);
    }

    void Update()
    {
        if (ship == null) return;
        bool shown = !mining.Active && !planet.InCave;
        // The planet joins up with itself, so the ship is drawn at its copy nearest the player.
        Camera view = Viewer != null ? Viewer : Camera.main;
        Vector2 at = planet.Nearest(foot, view.transform.position);
        // Each of his pixels is drawn as a whole number of the screen's pixels, all the same size, so it looks clean: the
        // ship is made a touch bigger or smaller to fit, whatever the zoom.
        float screenPixels = Screen.height / (2f * view.orthographicSize) / PixelsPerSquare;
        // And lined up with the screen's pixels, wherever the camera is, so its edges stay clean as the view moves.
        float unitPixels = Screen.height / (2f * view.orthographicSize);
        Vector2 eye = view.transform.position;
        Vector2 offset = at - eye;
        ship.transform.position = eye + new Vector2(Mathf.Round(offset.x * unitPixels), Mathf.Round(offset.y * unitPixels)) / unitPixels;
        float fit = Mathf.Max(1f, Mathf.Round(screenPixels)) / screenPixels;
        ship.transform.localScale = new Vector3(fit, fit, 1f);
        ship.enabled = shown;
        shadow.enabled = shown;
        // Under the ship, about as wide as its body, on the snow it rests on. Standing, it is under its tail.
        bool upright = inside != null && ship.sprite != broken;
        shadow.transform.position = upright ? at + new Vector2(0f, 0.25f) : at + new Vector2(-0.4f, 1.3f);
        shadow.transform.localScale = upright ? new Vector3(3.4f, 1.3f, 1f) : new Vector3(ship.bounds.size.x * 0.55f, 2.4f, 1f);
        // Fixed just now: a cloud of smoke covers it, and the fixed ship is there when it clears. (A saved game that was
        // fixed starts with the fixed ship.)
        if (nook.ShipFixed && !wasFixed)
        {
            wasFixed = true;
            if (Time.timeSinceLevelLoad > 2f) { cloudSince = Time.time; cloudThrown = 0; }
        }
        if (nook.ShipFixed && fixedUp != null && (cloudSince < 0f || Time.time - cloudSince >= SwapAfter))
            ship.sprite = flowing[Mathf.FloorToInt(Time.time * FlowSpeed) % flowing.Length];
        if (cloudSince >= 0f && shown) ThrowCloud(at);
        if (inside != null)
        {
            upright = ship.sprite != broken;
            inside.enabled = shown && upright;
            glass.enabled = shown && upright;
            figure.enabled = shown && upright && Seated;
            glass.color = new Color(1f, 1f, 1f, GlassShows);
        }

        // The broken thrusters smoke.
        if (shown && !nook.ShipFixed && Time.time >= nextPuffAt)
        {
            nextPuffAt = Time.time + PuffEvery;
            Texture2D picture = broken.texture;
            foreach (Vector2 thruster in Thrusters)
            {
                var go = new GameObject("Smoke");
                go.transform.SetParent(planet.TopDownRoot, false);
                var puff = go.AddComponent<SpriteRenderer>();
                puff.sprite = puffSprite;
                puff.sortingOrder = Order;
                var start = new Vector2((thruster.x - picture.width / 2f) / PixelsPerSquare, (picture.height - thruster.y) / PixelsPerSquare - broken.pivot.y / PixelsPerSquare);
                puffs.Add(new Puff { Picture = puff, Start = start, Sideways = new Vector2(Random.Range(-Drift, Drift), 0f), Born = Time.time });
            }
        }
        for (int i = puffs.Count - 1; i >= 0; i--)
        {
            Puff puff = puffs[i];
            float age = (Time.time - puff.Born) / (puff.Cloud ? CloudPuffSeconds : PuffSeconds);
            if (age >= 1f || !shown)
            {
                Destroy(puff.Picture.gameObject);
                puffs.RemoveAt(i);
                continue;
            }
            float since = Time.time - puff.Born;
            puff.Picture.transform.position = at + puff.Start + Vector2.up * (puff.Cloud ? RiseSpeed * 0.12f : RiseSpeed) * since + puff.Sideways * since;
            float size = puff.Cloud ? Mathf.Lerp(CloudPuffSmall, CloudPuffBig, age) : Mathf.Lerp(PuffSmall, PuffBig, age);
            puff.Picture.transform.localScale = new Vector3(size, size, 1f);
            Color c = puff.Cloud ? CloudColor : SmokeColor;
            // The cloud stays thick for a while, then thins away.
            c.a *= puff.Cloud ? Mathf.Clamp01(age * 12f) * Mathf.Clamp01((1f - age) * 1.7f) : Mathf.Clamp01(age * 6f) * (1f - age);
            puff.Picture.color = c;
        }
    }

    // Throws up the cloud of smoke over the ship, a few puffs a frame, all over it.
    void ThrowCloud(Vector2 at)
    {
        float since = Time.time - cloudSince;
        int wanted = Mathf.Min(CloudPuffs, Mathf.CeilToInt(CloudPuffs * since / CloudRising));
        // Over the wreck, and over the taller standing ship that will be there when it clears.
        Vector3 size = Vector3.Max(broken.bounds.size, fixedUp != null ? fixedUp.bounds.size : Vector3.zero) * ship.transform.localScale.x;
        for (; cloudThrown < wanted; cloudThrown++)
        {
            var go = new GameObject("Smoke Cloud");
            go.transform.SetParent(planet.TopDownRoot, false);
            var puff = go.AddComponent<SpriteRenderer>();
            puff.sprite = puffSprite;
            puff.sortingOrder = Order;
            // All over the picture, from the ground to the top of the tall fixed ship.
            var start = new Vector2(Random.Range(-0.5f, 0.5f) * size.x, Random.Range(0f, 0.95f) * size.y);
            puffs.Add(new Puff { Picture = puff, Start = start, Sideways = new Vector2(Random.Range(-Drift, Drift), 0f), Born = Time.time, Cloud = true });
        }
        if (since > CloudRising + CloudPuffSeconds) cloudSince = -1f;
    }

    // Whether a point (in the world) is on the ship's picture, not on a see-through part of it.
    public bool Covers(Vector2 point)
    {
        if (ship == null || !ship.enabled) return false;
        Sprite sprite = ship.sprite;
        Vector2 local = (point - (Vector2)ship.transform.position) / ship.transform.localScale.x * sprite.pixelsPerUnit + sprite.pivot;
        int x = Mathf.FloorToInt(local.x), y = Mathf.FloorToInt(local.y);
        if (x < 0 || y < 0 || x >= sprite.texture.width || y >= sprite.texture.height) return false;
        if (sprite.texture.GetPixel(x, y).a > 0f) return true;
        // The crystal is a layer of its own.
        return glass != null && glass.enabled && glass.sprite.texture.GetPixel(x, y).a > 0f;
    }

    // Whether the fixed ship is standing there now (not hidden in its cloud).
    public bool StandingThere => ship != null && nook.ShipFixed && ship.sprite != broken;

    // Where it stands, at its copy nearest a point.
    public Vector2 FootNear(Vector2 point) => planet.Nearest(foot, point);

    // The size of its picture, in squares.
    public Vector2 Size => ship != null ? (Vector2)ship.sprite.bounds.size : Vector2.zero;

    // For checking: how many puffs of smoke are rising.
    public int Puffs => puffs.Count;
}
