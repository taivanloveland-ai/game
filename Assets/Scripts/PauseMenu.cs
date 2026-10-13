using UnityEngine;
using UnityEngine.SceneManagement;

// The pause screen. Escape opens it, unless a menu (the world map, the items, the workbench) is open, which Escape
// closes instead. While it is up the game stands still. From top to bottom: Resume, Save, Settings, Save and Quit.
// Settings has the music volume, whether the mini map shows, the zoom, and full screen. Save and Quit goes back to
// the title screen.
public class PauseMenu : MonoBehaviour
{
    // How long "Saved" stays up after saving, in seconds.
    const float SavedSeconds = 2f;

    PlanetOne planet;
    SaveGame saves;
    Camera cam;
    Hud hud;
    WorldMap map;
    CraftingNook nook;

    // True while the pause screen is up, and while its settings are showing.
    public bool Paused { get; private set; }
    bool settings;
    // Whether a menu was open on the last frame: Escape closed it, so it should not pause as well.
    bool menuWasOpen;
    string note = "";
    float noteUntil;
    GUIStyle buttonStyle, titleStyle, labelStyle, noteStyle;
    Texture2D buttonBack, buttonOver, panelBack;

    public void Setup(PlanetOne planet, SaveGame saves, Camera cam, Hud hud, WorldMap map, CraftingNook nook)
    {
        this.planet = planet;
        this.saves = saves;
        this.cam = cam;
        this.hud = hud;
        this.map = map;
        this.nook = nook;
        ApplyZoom();
        Screen.fullScreen = GameSettings.FullScreen;
    }

    void Update()
    {
        if (GameInput.PausePressed())
        {
            if (settings) settings = false;
            else if (Paused) Resume();
            else if (!menuWasOpen) Pause();
        }
    }

    void LateUpdate()
    {
        menuWasOpen = map.Open || hud.ItemsOpen || nook.Open;
    }

    public void Pause()
    {
        Paused = true;
        settings = false;
        Time.timeScale = 0f;
        GameInput.Blocked = true;
    }

    public void Resume()
    {
        Paused = false;
        settings = false;
        Time.timeScale = 1f;
        GameInput.Blocked = false;
        GameSettings.Keep();
    }

    // The camera is as close as the settings say: twice as close again in a cave.
    void ApplyZoom()
    {
        cam.orthographicSize = planet.InCave ? GameSettings.Zoom / 2f : GameSettings.Zoom;
    }

    void Save()
    {
        bool saved = saves.Save();
        note = saved ? "Saved" : saves.Slot < 0 ? "This game has no save slot" : "Could not save";
        noteUntil = Time.unscaledTime + SavedSeconds;
    }

    // Saves, and goes back to the title screen.
    void SaveAndQuit()
    {
        saves.Save();
        GameSettings.Keep();
        Time.timeScale = 1f;
        GameInput.Blocked = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnGUI()
    {
        if (!Paused) return;
        GUI.depth = -30;
        float h = Screen.height, w = Screen.width;
        if (buttonStyle == null)
        {
            buttonBack = Fill(new Color(0.07f, 0.11f, 0.2f, 0.95f));
            buttonOver = Fill(new Color(0.16f, 0.3f, 0.46f, 1f));
            panelBack = Fill(new Color(0.02f, 0.04f, 0.09f, 0.9f));
            buttonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            buttonStyle.normal.background = buttonBack;
            buttonStyle.hover.background = buttonOver;
            buttonStyle.active.background = buttonOver;
            buttonStyle.normal.textColor = new Color(0.88f, 0.95f, 1f);
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;
            titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = new Color(0.88f, 0.95f, 1f);
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
            labelStyle.normal.textColor = new Color(0.82f, 0.9f, 0.97f);
            noteStyle = new GUIStyle(titleStyle);
            noteStyle.normal.textColor = new Color(0.6f, 0.95f, 0.75f);
        }
        buttonStyle.fontSize = Mathf.RoundToInt(h / 26f);
        titleStyle.fontSize = Mathf.RoundToInt(h / 16f);
        labelStyle.fontSize = Mathf.RoundToInt(h / 34f);
        noteStyle.fontSize = Mathf.RoundToInt(h / 30f);

        // The game behind is dimmed.
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float wide = Mathf.Max(360f, w * 0.3f), tall = h * 0.085f, gap = h * 0.022f;
        float left = (w - wide) / 2f;
        var panel = new Rect(left - wide * 0.12f, h * 0.14f, wide * 1.24f, h * 0.72f);
        GUI.DrawTexture(panel, panelBack);
        GUI.Label(new Rect(panel.x, panel.y + h * 0.02f, panel.width, h * 0.1f), settings ? "Settings" : "Paused", titleStyle);
        float top = panel.y + h * 0.15f;

        if (settings)
        {
            DrawSettings(left, top, wide, tall, gap);
            return;
        }
        if (GUI.Button(new Rect(left, top, wide, tall), "Resume", buttonStyle)) Resume();
        if (GUI.Button(new Rect(left, top + (tall + gap), wide, tall), "Save", buttonStyle)) Save();
        if (GUI.Button(new Rect(left, top + (tall + gap) * 2f, wide, tall), "Settings", buttonStyle)) settings = true;
        if (GUI.Button(new Rect(left, top + (tall + gap) * 3f, wide, tall), "Save and Quit", buttonStyle)) SaveAndQuit();
        if (Time.unscaledTime < noteUntil) GUI.Label(new Rect(panel.x, top + (tall + gap) * 4f, panel.width, tall * 0.7f), note, noteStyle);
    }

    void DrawSettings(float left, float top, float wide, float tall, float gap)
    {
        float row = tall * 0.8f, step = row + gap, half = wide * 0.46f;
        float slider = top + row * 0.3f;

        float volume = GameSettings.MusicVolume;
        GUI.Label(new Rect(left, top, half, row), "Music volume: " + Mathf.RoundToInt(volume * 100f) + "%", labelStyle);
        float newVolume = GUI.HorizontalSlider(new Rect(left + half, slider, wide - half, row * 0.5f), volume, 0f, 1f);
        if (newVolume != volume) GameSettings.MusicVolume = newVolume;

        // Further right on the slider is closer in.
        float zoom = GameSettings.Zoom;
        GUI.Label(new Rect(left, top + step, half, row), "Zoom: " + (GameSettings.UsualZoom / zoom).ToString("0.0") + "x", labelStyle);
        float closeness = GUI.HorizontalSlider(new Rect(left + half, slider + step, wide - half, row * 0.5f), -zoom, -GameSettings.FurthestZoom, -GameSettings.ClosestZoom);
        if (closeness != -zoom)
        {
            GameSettings.Zoom = -closeness;
            ApplyZoom();
        }

        bool mini = GameSettings.ShowMiniMap;
        if (GUI.Button(new Rect(left, top + step * 2f, wide, row), "Mini map: " + (mini ? "On" : "Off"), buttonStyle)) GameSettings.ShowMiniMap = !mini;
        bool full = GameSettings.FullScreen;
        if (GUI.Button(new Rect(left, top + step * 3f, wide, row), "Full screen: " + (full ? "On" : "Off"), buttonStyle)) GameSettings.FullScreen = !full;

        if (GUI.Button(new Rect(left, top + step * 4f + gap, wide, row), "Back", buttonStyle))
        {
            settings = false;
            GameSettings.Keep();
        }
    }

    static Texture2D Fill(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
