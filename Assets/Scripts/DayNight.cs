using UnityEngine;

// The day and night cycle. The planets orbit a binary system: a dim red sun and a black hole.
// In the side-scroller view the sun crosses the sky by day and the black hole by night, with stars.
// That is in the column view (G), which looks west. In the row view (R), which looks north, the sun and black
// hole are out of sight and only the stars show.
// At night the world also gets darker, in both views.
public class DayNight : MonoBehaviour
{
    // Seconds for one full day and night: 6 minutes, half of it day and half night.
    public float cycleSeconds = 360f;
    // Where we are in the cycle: 0 is sunrise, 0.5 is sunset, and 1 is the next sunrise.
    [Range(0f, 1f)] public float time = 0.1f;

    const int Pixels = 16;
    // The black hole is painted at the same fine scale as the snow picture: this many pixels to a block.
    const int FinePixels = 128;
    const int BlackHoleWidth = 8 * FinePixels, BlackHoleHeight = 9 * FinePixels / 2;
    // The black hole's disc of hot matter is painted level. It is turned as the black hole crosses the sky.
    const float DiscTilt = 0f;
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
        blackHole = MakeSkySprite("Black Hole", PlanetOne.PaintSprite(BlackHoleWidth, BlackHoleHeight, new Vector2(0.5f, 0.5f), FinePixels, true, PaintBlackHole), -20);

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

        // The sun and the black hole cross the sky from east to west. They are in view looking along a column (west),
        // and out of sight looking along a row (north). The day carries on all the same.
        sun.enabled = day && mining.Column;
        blackHole.enabled = !day && mining.Column;
        SpriteRenderer up = day ? sun : blackHole;
        up.transform.localPosition = position;
        // The black hole keeps its disc lined up with its path across the sky, as a real one would: tipped up as it
        // rises, level at midnight when it is highest, and tipped down as it sets.
        float turn = Mathf.Atan2(Mathf.Cos(angle) * cam.orthographicSize * 0.72f, Mathf.Sin(angle) * halfWidth * 0.85f) * Mathf.Rad2Deg;
        blackHole.transform.localRotation = Quaternion.Euler(0f, 0f, turn);
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

    // A black disc wrapped in light, after the picture Taivan gave: a long, tipped streak of hot matter crossing in
    // front of it, the far side of that disc bent up and over the top as a thick bright arc, a thinner ring beneath,
    // and a dim glow round it all. Distances here are in blocks from the middle of the hole, which is 1 block in radius.
    static Color32 PaintBlackHole(int x, int y)
    {
        float dx = (x + 0.5f - BlackHoleWidth / 2f) / FinePixels, dy = (BlackHoleHeight / 2f - (y + 0.5f)) / FinePixels;
        // Along the tipped disc, and up from it.
        float tilt = DiscTilt * Mathf.Deg2Rad;
        float along = dx * Mathf.Cos(tilt) + dy * Mathf.Sin(tilt), up = -dx * Mathf.Sin(tilt) + dy * Mathf.Cos(tilt);
        float r = Mathf.Sqrt(dx * dx + dy * dy);

        // The streak: brightest and thickest by the hole, tapering to a point far out on either side.
        float reach = Mathf.Clamp01(1f - Mathf.Abs(along) / 3.8f);
        float streak = Mathf.Pow(reach, 1.3f) * 1.25f * Bump((up + 0.14f) / (0.05f + 0.15f * reach));
        // Light bent round the hole. The arc over the top is thick and brightest toward the right, the ring
        // underneath is thin and faint, and a fine bright line hugs the edge of the hole all the way round.
        float over = up > -0.14f ? Bump((r - 1.2f) / 0.14f) * Mathf.Lerp(0.75f, 1.15f, Mathf.InverseLerp(-1.2f, 1.2f, along)) : 0f;
        float under = up <= -0.14f ? Bump((r - 1.27f) / 0.045f) * 0.5f : 0f;
        float edge = Bump((r - 1.04f) / 0.035f) * (up > -0.14f ? 0.9f : 0.45f);
        float glow = r > 1f ? Bump((r - 1f) / 1.1f) * 0.16f : 0f;
        // A wide, dim haze along the disc.
        float haze = reach * 0.22f * Bump((up + 0.14f) / (0.2f + 0.45f * reach));

        if (r < 1f)
        {
            // Inside the hole only the streak passing in front shows; the rest is black.
            Color lit = Heat(streak);
            float cover = Mathf.Clamp01(streak * 1.6f);
            return new Color(lit.r * cover, lit.g * cover, lit.b * cover, 1f);
        }
        float light = streak + over + under + edge + glow + haze;
        Color color = Heat(light);
        color.a = Mathf.Clamp01(light * 1.5f);
        return color;
    }

    // 1 at the middle, falling smoothly away to nothing either side.
    static float Bump(float distance)
    {
        return Mathf.Exp(-distance * distance);
    }

    // The colour of light of a given strength: deep red when faint, through orange and yellow to near white.
    static Color Heat(float light)
    {
        var faint = new Color(0.55f, 0.16f, 0.03f);
        var orange = new Color(1f, 0.52f, 0.14f);
        var yellow = new Color(1f, 0.84f, 0.48f);
        var white = new Color(1f, 0.98f, 0.92f);
        if (light < 0.35f) return Color.Lerp(faint, orange, light / 0.35f);
        if (light < 0.75f) return Color.Lerp(orange, yellow, (light - 0.35f) / 0.4f);
        return Color.Lerp(yellow, white, Mathf.Clamp01((light - 0.75f) / 0.45f));
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
