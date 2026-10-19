using System.Collections.Generic;
using UnityEngine;

// The wrecked ship, as Taivan drew it (Assets/Resources/Art/CrashedShip): nose down in the snow, cracked, with red
// sparks, and smoke rising from its grey body. (His second drawing, 2026-10-18; Art/CrashedShipBack, the ship seen
// from behind, is kept for later.) Once it is fixed (at the workbench) a cloud of
// smoke covers it, and when the smoke clears his picture of the fixed ship is there (Art/CrashedShipFixed), with light
// flowing along its green lines. It stands over the ship's squares, which nobody walks through, in the top-down view.
public class CrashedShip : MonoBehaviour
{
    // How many of his pixels make one square: the wreck is drawn about nine squares wide.
    // (18 until 2026-10-18, when Taivan asked for it 3 times bigger.)
    const float PixelsPerSquare = 6f;
    // The smoke is drawn this many times bigger to go with it.
    const float SmokeScale = 3f;
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
    Sprite broken, fixedUp, puffSprite;
    // The fixed ship with the light at each step along its green lines.
    Sprite[] flowing;
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
        if (fixedUp != null) flowing = MakeFlow(fixedUp);
        if (broken == null) return;
        var go = new GameObject("Crashed Ship");
        go.transform.SetParent(planet.TopDownRoot, false);
        ship = go.AddComponent<SpriteRenderer>();
        ship.sprite = broken;
        // Sorted by its foot, so the player walks in front of it or behind it correctly.
        ship.spriteSortPoint = SpriteSortPoint.Pivot;
        // A round puff of smoke in big square pixels, like his pictures: thicker in the middle, in three steps.
        puffSprite = PlanetOne.PaintSprite(8, 8, new Vector2(0.5f, 0.5f), 8f, (x, y) =>
        {
            float dx = (x + 0.5f - 4f) / 4f, dy = (y + 0.5f - 4f) / 4f, r = dx * dx + dy * dy;
            return r > 1f ? new Color32(0, 0, 0, 0) : new Color32(255, 255, 255, (byte)(r < 0.3f ? 255 : r < 0.65f ? 190 : 120));
        });
        puffSprite.texture.filterMode = FilterMode.Point;
    }

    // His picture, unchanged and drawn sharp, standing on its lowest row. Null if it is missing.
    // The fixed ship once for each step of the light along its green lines: his picture, with the green brightened
    // toward white where a patch of light is.
    static Sprite[] MakeFlow(Sprite ship)
    {
        Texture2D source = ship.texture;
        Color32[] pixels = source.GetPixels32();
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
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(lit);
            texture.Apply();
            frames[step] = Sprite.Create(texture, new Rect(0, 0, source.width, source.height), new Vector2(ship.pivot.x / source.width, ship.pivot.y / source.height), PixelsPerSquare);
        }
        return frames;
    }

    static Sprite Load(string path)
    {
        var picture = Resources.Load<Texture2D>(path);
        if (picture == null) return null;
        Color32[] pixels = PlanetOne.ReadPixels(picture);
        var texture = new Texture2D(picture.width, picture.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply();
        int lowest = 0;
        while (lowest < picture.height - 1 && System.Array.FindIndex(pixels, lowest * picture.width, picture.width, c => c.a > 0) < 0) lowest++;
        return Sprite.Create(texture, new Rect(0, 0, picture.width, picture.height), new Vector2(0.5f, lowest / (float)picture.height), PixelsPerSquare);
    }

    void Update()
    {
        if (ship == null) return;
        bool shown = !mining.Active && !planet.InCave;
        // The planet joins up with itself, so the ship is drawn at its copy nearest the player.
        Vector2 at = planet.Nearest(foot, Camera.main.transform.position);
        ship.transform.position = at;
        ship.enabled = shown;
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
        Bounds bounds = ship.bounds;
        for (; cloudThrown < wanted; cloudThrown++)
        {
            var go = new GameObject("Smoke Cloud");
            go.transform.SetParent(planet.TopDownRoot, false);
            var puff = go.AddComponent<SpriteRenderer>();
            puff.sprite = puffSprite;
            puff.sortingOrder = Order;
            // All over the picture, from the ground to the top of the tall fixed ship.
            var start = new Vector2(Random.Range(-0.5f, 0.5f) * bounds.size.x, Random.Range(0f, 0.95f) * bounds.size.y);
            puffs.Add(new Puff { Picture = puff, Start = start, Sideways = new Vector2(Random.Range(-Drift, Drift), 0f), Born = Time.time, Cloud = true });
        }
        if (since > CloudRising + CloudPuffSeconds) cloudSince = -1f;
    }

    // For checking: how many puffs of smoke are rising.
    public int Puffs => puffs.Count;
}
