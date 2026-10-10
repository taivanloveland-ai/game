using UnityEngine;

// The day and night cycle. The planets orbit a binary system: a dim red sun and a black hole.
// In the side-scroller view the sun crosses the sky by day and the black hole by night, with stars.
// At night the world also gets darker, in both views.
public class DayNight : MonoBehaviour
{
    // Seconds for one full day and night: 6 minutes, half of it day and half night.
    public float cycleSeconds = 360f;
    // Where we are in the cycle: 0 is sunrise, 0.5 is sunset, and 1 is the next sunrise.
    [Range(0f, 1f)] public float time = 0.1f;

    const int Pixels = 16;
    // How much of the cycle each sunrise and sunset takes to fade, on either side of the moment itself.
    const float Twilight = 0.05f;
    // The world's darkness changes in this many steps so it is not recoloured every single frame.
    const int TintSteps = 32;

    static readonly Color DaySky = new Color32(66, 52, 78, 255);
    static readonly Color NightSky = new Color32(6, 8, 16, 255);
    static readonly Color NightTint = new Color(0.42f, 0.48f, 0.68f);
    static readonly Color NightOverlay = new Color(0.03f, 0.05f, 0.16f, 0.5f);

    // 0 in full daylight, 1 in the middle of the night.
    public float Night { get; private set; }
    public Color SkyColor => Color.Lerp(DaySky, NightSky, Night);
    // What to multiply the world's colours by right now.
    public Color WorldTint => Color.Lerp(Color.white, NightTint, Mathf.Round(Night * TintSteps) / TintSteps);

    Camera cam;
    MiningMode mining;
    GameObject sky;
    SpriteRenderer sun, blackHole, stars, overlay;

    public void Setup(Camera cam, MiningMode mining, Sprite square)
    {
        this.cam = cam;
        this.mining = mining;

        // The sky rides along with the camera, behind everything else.
        sky = new GameObject("Sky");
        sky.transform.SetParent(cam.transform, false);
        sky.transform.localPosition = new Vector3(0f, 0f, 10f);
        stars = MakeSkySprite("Stars", PlanetOne.PaintSprite(448, 256, new Vector2(0.5f, 0.5f), PaintStars), -30);
        sun = MakeSkySprite("Sun", PlanetOne.PaintSprite(64, 64, new Vector2(0.5f, 0.5f), PaintSun), -20);
        blackHole = MakeSkySprite("Black Hole", PlanetOne.PaintSprite(112, 64, new Vector2(0.5f, 0.5f), PaintBlackHole), -20);

        // Top-down has no sky, so night there is a dark blue wash over the whole screen.
        var overlayObject = new GameObject("Night Overlay");
        overlayObject.transform.SetParent(cam.transform, false);
        overlayObject.transform.localPosition = new Vector3(0f, 0f, 10f);
        overlayObject.transform.localScale = new Vector3(80f, 50f, 1f);
        overlay = overlayObject.AddComponent<SpriteRenderer>();
        overlay.sprite = square;
        overlay.sortingOrder = 100;

        Refresh();
    }

    SpriteRenderer MakeSkySprite(string name, Sprite sprite, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(sky.transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    void Update()
    {
        time = Mathf.Repeat(time + Time.deltaTime / cycleSeconds, 1f);
        Refresh();
    }

    void Refresh()
    {
        // Midnight is at 0.75. Night fades in around sunset and out around sunrise.
        float fromMidnight = Mathf.Abs(Mathf.Repeat(time - 0.75f + 0.5f, 1f) - 0.5f);
        Night = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f - Twilight, 0.25f + Twilight, fromMidnight));

        bool sideView = mining != null && mining.Active;
        sky.SetActive(sideView);
        overlay.enabled = !sideView && Night > 0.01f;
        overlay.color = new Color(NightOverlay.r, NightOverlay.g, NightOverlay.b, NightOverlay.a * Night);
        if (!sideView) return;

        // Whichever of the two is up rises on the left, arcs over, and sets on the right.
        bool day = time < 0.5f;
        float angle = (day ? time : time - 0.5f) * 2f * Mathf.PI;
        float halfWidth = cam.orthographicSize * cam.aspect;
        var position = new Vector3(-Mathf.Cos(angle) * halfWidth * 0.85f, 0.3f + Mathf.Sin(angle) * cam.orthographicSize * 0.72f, 0f);
        // Fade in and out near the horizon instead of popping.
        float fade = Mathf.Clamp01(Mathf.Sin(angle) * 5f);

        sun.enabled = day;
        blackHole.enabled = !day;
        SpriteRenderer up = day ? sun : blackHole;
        up.transform.localPosition = position;
        up.color = new Color(1f, 1f, 1f, fade);
        stars.color = new Color(1f, 1f, 1f, Night);
    }

    // The paint functions get a pixel position in the picture and return its colour. 16 pixels is one block.

    // A dull red disc with a faint glow around it.
    static Color32 PaintSun(int x, int y)
    {
        float r = Radius(x, y, 32f, 32f);
        if (r < 0.55f) return new Color32(214, 78, 52, 255);
        if (r < 0.85f) return new Color32(186, 52, 40, 255);
        float glow = 1f - Mathf.InverseLerp(0.85f, 2f, r);
        return new Color32(186, 52, 40, (byte)(glow * glow * 110f));
    }

    // A black disc with a thin bright ring, and a flat glowing band of hot matter crossing in front of it.
    static Color32 PaintBlackHole(int x, int y)
    {
        float dx = (x + 0.5f - 56f) / Pixels, dy = (y + 0.5f - 32f) / Pixels;
        float r = Mathf.Sqrt(dx * dx + dy * dy);

        float band = Squared(dx / 3.2f) + Squared(dy / 0.2f);
        if (band < 1f)
        {
            float heat = (1f - Mathf.Abs(dx) / 3.2f) * (1f - band);
            return Color32.Lerp(new Color32(255, 140, 50, 255), new Color32(255, 240, 200, 255), heat);
        }
        if (r < 0.95f) return new Color32(0, 0, 0, 255);
        if (r < 1.08f) return new Color32(255, 190, 110, 255);
        if (r < 1.7f)
        {
            float glow = 1f - Mathf.InverseLerp(1.08f, 1.7f, r);
            return new Color32(255, 140, 50, (byte)(glow * glow * 110f));
        }
        float halo = Squared(dx / 3.4f) + Squared(dy / 0.45f);
        return new Color32(255, 140, 50, (byte)(halo < 1f ? (1f - halo) * 90f : 0f));
    }

    // Mostly empty, with the odd star of varying brightness.
    static Color32 PaintStars(int x, int y)
    {
        int hash = (x * 73856093) ^ (y * 19349663);
        hash = (hash ^ (hash >> 13)) * 0x5bd1e995;
        hash ^= hash >> 15;
        if ((hash & 1023) > 3) return new Color32(0, 0, 0, 0);
        return new Color32(225, 232, 255, (byte)(110 + ((hash >> 10) & 127)));
    }

    static float Radius(int x, int y, float centerX, float centerY)
    {
        float dx = (x + 0.5f - centerX) / Pixels, dy = (y + 0.5f - centerY) / Pixels;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    static float Squared(float value)
    {
        return value * value;
    }
}
