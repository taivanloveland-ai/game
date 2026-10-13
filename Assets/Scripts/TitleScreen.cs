using UnityEngine;

// The title screen, and the loading screen after it. Both show Planet 1 from space, with the red sun and the black
// hole it circles, against the stars. The title screen has three buttons: Survival, Creative and Exit. Picking a
// mode starts the world being made, and the same scene stays up as the loading screen, with a line saying what is
// being made and the black hole slowly turning, until the game begins.
public class TitleScreen : MonoBehaviour
{
    // The planet is painted this many pixels across and drawn this many blocks across.
    const int PlanetPixels = 256;
    const float PlanetSize = 10.4f;
    // Where things are on screen, in blocks from the middle (the screen is 14 blocks tall).
    static readonly Vector3 PlanetAt = new Vector3(-5.6f, -0.6f, 0f), SunAt = new Vector3(9.2f, 4.6f, 0f), BlackHoleAt = new Vector3(2.4f, 3.9f, 0f);
    // The scene is drawn over everything else, in case anything else is on screen.
    const int Order = 500;
    // How fast the black hole turns on the loading screen, in degrees a second.
    const float LoadingTurn = 8f;

    // The planet from space. Latitudes, in degrees: the North Pole's ice, the ring of mountains below it, the ocean
    // round the South Pole and its snowy cap. (The bands are drawn wider than they really are, so they can be seen.)
    const float NorthIceFrom = 76f, MountainsFrom = 56f, OceanFrom = -57f, SouthSnowFrom = -71f;
    static readonly Color Snow = new Color32(226, 234, 242, 255), Ice = new Color32(146, 202, 232, 255), Rock = new Color32(93, 107, 126, 255);
    static readonly Color Water = new Color32(38, 78, 122, 255), Peak = new Color32(245, 249, 252, 255);
    static readonly Color Night = new Color32(5, 8, 16, 255), Air = new Color32(150, 204, 240, 255);
    // The way to the sun, for the planet's day and night sides.
    static readonly Vector3 ToSun = new Vector3(0.72f, 0.42f, 0.55f).normalized;

    PlanetOne planet;
    Camera cam;
    Color skyBefore;
    GameObject scene;
    Transform blackHole;
    bool loading;
    float loadingSince;
    GUIStyle buttonStyle, loadingStyle, stageStyle;
    Texture2D buttonBack, buttonOver;

    public void Setup(PlanetOne planet, Camera cam)
    {
        this.planet = planet;
        scene = new GameObject("Title Scene");
        scene.transform.SetParent(cam.transform, false);
        scene.transform.localPosition = new Vector3(0f, 0f, 10f);
        this.cam = cam;
        skyBefore = cam.backgroundColor;
        cam.backgroundColor = Color.black;

        Make("Stars", DayNight.StarsSprite(), Vector3.zero, Order);
        Make("Sun", DayNight.SunSprite(), SunAt, Order + 1);
        blackHole = Make("Black Hole", DayNight.BlackHoleSprite(), BlackHoleAt, Order + 1);
        blackHole.localRotation = Quaternion.Euler(0f, 0f, -14f);
        Sprite planetSprite = PlanetOne.PaintSprite(PlanetPixels, PlanetPixels, new Vector2(0.5f, 0.5f), PlanetPixels / PlanetSize, PaintPlanet);
        Transform world = Make("Planet 1", planetSprite, PlanetAt, Order + 2);
        // Tipped a little, as planets are.
        world.localRotation = Quaternion.Euler(0f, 0f, -16f);
    }

    Transform Make(string name, Sprite sprite, Vector3 at, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(scene.transform, false);
        go.transform.localPosition = at;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return go.transform;
    }

    // Starts a game: survival, or creative. The buttons call this.
    public void Choose(bool creative)
    {
        if (loading) return;
        loading = true;
        loadingSince = Time.unscaledTime;
        planet.StartGame(creative);
    }

    void Update()
    {
        if (!loading) return;
        blackHole.Rotate(0f, 0f, -LoadingTurn * Time.unscaledDeltaTime);
        // The game has begun: the title scene goes.
        if (planet.Ready)
        {
            cam.backgroundColor = skyBefore;
            Destroy(scene);
            Destroy(this);
        }
    }

    void OnGUI()
    {
        GUI.depth = -20;
        float h = Screen.height, w = Screen.width;
        if (buttonStyle == null)
        {
            buttonBack = Fill(new Color(0.05f, 0.08f, 0.16f, 0.82f));
            buttonOver = Fill(new Color(0.16f, 0.3f, 0.46f, 0.92f));
            buttonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            buttonStyle.normal.background = buttonBack;
            buttonStyle.hover.background = buttonOver;
            buttonStyle.active.background = buttonOver;
            buttonStyle.normal.textColor = new Color(0.88f, 0.95f, 1f);
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;
            loadingStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            loadingStyle.normal.textColor = new Color(0.88f, 0.95f, 1f);
            stageStyle = new GUIStyle(loadingStyle) { fontStyle = FontStyle.Normal };
            stageStyle.normal.textColor = new Color(0.66f, 0.8f, 0.92f);
        }
        buttonStyle.fontSize = Mathf.RoundToInt(h / 22f);
        loadingStyle.fontSize = Mathf.RoundToInt(h / 13f);
        stageStyle.fontSize = Mathf.RoundToInt(h / 30f);

        // The buttons, and the loading words that take their place, are on the right, clear of the planet.
        float left = w * 0.6f, wide = w * 0.3f;
        if (loading)
        {
            int dots = (int)((Time.unscaledTime - loadingSince) * 2f) % 4;
            GUI.Label(new Rect(left, h * 0.46f, wide, h * 0.12f), "Loading" + new string('.', dots) + new string(' ', 3 - dots), loadingStyle);
            GUI.Label(new Rect(left - wide * 0.25f, h * 0.58f, wide * 1.5f, h * 0.06f), planet.LoadingStage, stageStyle);
            return;
        }
        float tall = h * 0.1f, gap = h * 0.035f, top = h * 0.42f;
        if (GUI.Button(new Rect(left, top, wide, tall), "Survival", buttonStyle)) Choose(false);
        if (GUI.Button(new Rect(left, top + tall + gap, wide, tall), "Creative", buttonStyle)) Choose(true);
        if (GUI.Button(new Rect(left, top + (tall + gap) * 2f, wide, tall), "Exit", buttonStyle)) Exit();
    }

    static Texture2D Fill(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    // Leaves the game. In the Unity editor that means stopping play.
    static void Exit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // One pixel of Planet 1 seen from space, with (0, 0) at the top left: a ball lit from the sun's side, with the
    // planet's own lands on it. North is up.
    static Color32 PaintPlanet(int x, int y)
    {
        float half = PlanetPixels / 2f;
        float nx = (x + 0.5f - half) / half, ny = (half - y - 0.5f) / half;
        float fromMiddle = nx * nx + ny * ny;
        if (fromMiddle > 1f) return new Color32(0, 0, 0, 0);
        float nz = Mathf.Sqrt(1f - fromMiddle);

        float latitude = Mathf.Asin(ny) * Mathf.Rad2Deg, longitude = Mathf.Atan2(nx, nz);
        float around = longitude * 5.5f, up = latitude * Mathf.Deg2Rad * 5.5f;
        Color land;
        if (latitude > NorthIceFrom) land = Ice;
        else if (latitude > MountainsFrom) land = Noise(around * 2.4f, up * 2.4f, 31) > 0.6f ? Peak : Noise(around * 1.6f, up * 1.6f, 47) > 0.36f ? Rock : Snow;
        else if (latitude < SouthSnowFrom) land = Noise(around * 2f, up * 2f, 59) > 0.72f ? Ice : Snow;
        else if (latitude < OceanFrom + (Noise(around, 0f, 67) - 0.5f) * 7f) land = Water;
        else
        {
            // The lowlands: snow, lakes of ice and grey rock, as on the ground.
            float e = Noise(around, up, 11) * 0.65f + Noise(around * 2.7f, up * 2.7f, 23) * 0.35f;
            land = e > 0.6f ? Rock : e < 0.42f ? Ice : Snow;
        }

        // Day on the side facing the sun, night on the other, with a soft line between. A thin rim of air at the edge.
        float sunlight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.3f, nx * ToSun.x + ny * ToSun.y + nz * ToSun.z));
        Color lit = Color.Lerp(Color.Lerp(Night, land, 0.1f), land, sunlight);
        if (fromMiddle > 0.93f) lit = Color.Lerp(lit, Air, 0.4f * sunlight * Mathf.InverseLerp(0.93f, 1f, fromMiddle));
        lit.a = 1f;
        return lit;
    }

    // Smooth blobs of noise, from 0 to 1.
    static float Noise(float x, float y, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
        return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0, seed), Hash(x0 + 1, y0, seed), sx), Mathf.Lerp(Hash(x0, y0 + 1, seed), Hash(x0 + 1, y0 + 1, seed), sx), sy);
    }

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393) ^ (uint)(y * 668265263) ^ (uint)(seed * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h >> 8) / 16777216f;
        }
    }
}
