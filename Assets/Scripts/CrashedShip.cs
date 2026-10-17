using System.Collections.Generic;
using UnityEngine;

// The wrecked ship, as Taivan drew it (Assets/Resources/Art/CrashedShip): its hull with the workbench nook in its
// doorway and three broken thrusters on top, smoke rising from each. Once it is fixed (at the workbench) a cloud of
// smoke covers it, and when the smoke clears his picture of the fixed ship is there (Art/CrashedShipFixed). It stands over the ship's squares, which nobody walks through, in the top-down view.
public class CrashedShip : MonoBehaviour
{
    // How many of his pixels make one square: the ship is drawn about three and a half squares wide.
    const float PixelsPerSquare = 18f;
    // Where the smoke comes from on his picture, in his pixels from the left and from the top: the top of each thruster.
    static readonly Vector2[] Thrusters = { new Vector2(27f, 52f), new Vector2(42f, 60f), new Vector2(55f, 68f) };
    // A puff of smoke is let go from each thruster this often, rises this fast (squares a second), drifts sideways
    // a little, grows from the first size to the second, and fades away over this many seconds.
    const float PuffEvery = 0.35f, RiseSpeed = 0.7f, Drift = 0.25f, PuffSmall = 0.35f, PuffBig = 1.1f, PuffSeconds = 2.6f;
    static readonly Color SmokeColor = new Color(0.55f, 0.57f, 0.6f, 0.7f);
    const int Order = 61;
    // The cloud when it is fixed: this many puffs, thrown up over this many seconds, each this big and lasting this
    // long. The fixed ship is put in place, hidden in the cloud, this many seconds after it starts.
    const int CloudPuffs = 70;
    const float CloudRising = 1f, CloudPuffBig = 2.2f, CloudPuffSeconds = 3.6f, SwapAfter = 1.3f;
    static readonly Color CloudColor = new Color(0.82f, 0.84f, 0.87f, 0.95f);

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
        if (nook.ShipFixed && fixedUp != null && (cloudSince < 0f || Time.time - cloudSince >= SwapAfter)) ship.sprite = fixedUp;
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
            puff.Picture.transform.position = at + puff.Start + Vector2.up * (puff.Cloud ? RiseSpeed * 0.3f : RiseSpeed) * since + puff.Sideways * since;
            float size = puff.Cloud ? Mathf.Lerp(PuffBig, CloudPuffBig, age) : Mathf.Lerp(PuffSmall, PuffBig, age);
            puff.Picture.transform.localScale = new Vector3(size, size, 1f);
            Color c = puff.Cloud ? CloudColor : SmokeColor;
            // The cloud stays thick for a while, then thins away.
            c.a *= puff.Cloud ? Mathf.Clamp01(age * 8f) * Mathf.Clamp01((1f - age) * 2.2f) : Mathf.Clamp01(age * 6f) * (1f - age);
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
            var start = new Vector2(Random.Range(-0.6f, 0.6f) * bounds.size.x, Random.Range(0.05f, 0.75f) * bounds.size.y);
            puffs.Add(new Puff { Picture = puff, Start = start, Sideways = new Vector2(Random.Range(-Drift, Drift), 0f), Born = Time.time, Cloud = true });
        }
        if (since > CloudRising + CloudPuffSeconds) cloudSince = -1f;
    }

    // For checking: how many puffs of smoke are rising.
    public int Puffs => puffs.Count;
}
