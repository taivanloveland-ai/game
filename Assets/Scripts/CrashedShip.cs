using System.Collections.Generic;
using UnityEngine;

// The wrecked ship, as Taivan drew it (Assets/Resources/Art/CrashedShip): its hull with the workbench nook in its
// doorway and three broken thrusters on top, smoke rising from each. Once the thrusters are fixed (at the workbench,
// with three magical orbs) the smoke stops, and his picture of the fixed ship is shown instead, once he has drawn it
// (Art/CrashedShipFixed). It stands over the ship's squares, which nobody walks through, in the top-down view.
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

    class Puff
    {
        public SpriteRenderer Picture;
        public Vector2 Start, Sideways;
        public float Born;
    }

    PlanetOne planet;
    CraftingNook nook;
    MiningMode mining;
    SpriteRenderer ship;
    Sprite broken, fixedUp, puffSprite;
    Vector2 foot;
    float nextPuffAt;
    readonly List<Puff> puffs = new List<Puff>();

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
        if (nook.ThrustersFixed && fixedUp != null) ship.sprite = fixedUp;

        // The broken thrusters smoke.
        if (shown && !nook.ThrustersFixed && Time.time >= nextPuffAt)
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
            float age = (Time.time - puff.Born) / PuffSeconds;
            if (age >= 1f || !shown)
            {
                Destroy(puff.Picture.gameObject);
                puffs.RemoveAt(i);
                continue;
            }
            float since = Time.time - puff.Born;
            puff.Picture.transform.position = at + puff.Start + Vector2.up * RiseSpeed * since + puff.Sideways * since;
            float size = Mathf.Lerp(PuffSmall, PuffBig, age);
            puff.Picture.transform.localScale = new Vector3(size, size, 1f);
            Color c = SmokeColor;
            c.a *= Mathf.Clamp01(age * 6f) * (1f - age);
            puff.Picture.color = c;
        }
    }

    // For checking: how many puffs of smoke are rising.
    public int Puffs => puffs.Count;
}
