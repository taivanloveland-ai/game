using UnityEngine;

// Creative mode's panel of extra controls, at the top left under the health bar: sliders for how fast the player
// moves and how high they jump (since 2026-10-20), buttons to turn snow and hail on and off, and a button to switch between day and night.
// (Creative mode's endless blocks, its tools and its fully drawn map are set up when the game starts; see PlanetOne.)
public class CreativeTools : MonoBehaviour
{
    // The slowest and fastest the slider goes, in squares a second. Survival mode's walking speed is 4.
    // (The fastest was 40 until 2026-10-16.)
    const float Slowest = 1f, Fastest = 80f;
    // The times of day the day and night button jumps to: the middle of the day, and the middle of the night.
    const float Noon = 0.25f, Midnight = 0.75f;
    // The lowest and highest a jump can be set to, in blocks, in half-block steps. Survival mode's jump is 1.5.
    const float LowestJump = 1f, HighestJump = 5f;

    PlayerController player;
    Weather weather;
    DayNight dayNight;
    GUIStyle labelStyle, buttonStyle;

    public void Setup(PlayerController player, Weather weather, DayNight dayNight)
    {
        this.player = player;
        this.weather = weather;
        this.dayNight = dayNight;
        GameInput.PointerOverTools = PointerOverPanel;
    }

    static Rect Panel()
    {
        return new Rect(16f, 78f, 320f, 162f);
    }

    // True while the mouse is over the panel, so using it does not also mine or build.
    bool PointerOverPanel()
    {
        Vector2 pointer = GameInput.PointerPosition();
        return Panel().Contains(new Vector2(pointer.x, Screen.height - pointer.y));
    }

    void OnGUI()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold };
        }
        Rect panel = Panel();
        GUI.Box(panel, GUIContent.none);
        float left = panel.x + 8f, wide = panel.width - 16f;
        GameText.Label(new Rect(left, panel.y + 2f, wide, 22f), "Speed: " + Mathf.RoundToInt(player.speed), labelStyle);
        player.speed = Mathf.Round(GUI.HorizontalSlider(new Rect(left, panel.y + 27f, wide, 14f), player.speed, Slowest, Fastest));

        GameText.Label(new Rect(left, panel.y + 46f, wide, 22f), "Jump: " + player.jumpHeight.ToString("0.#") + " blocks", labelStyle);
        player.jumpHeight = Mathf.Round(GUI.HorizontalSlider(new Rect(left, panel.y + 71f, wide, 14f), player.jumpHeight, LowestJump, HighestJump) * 2f) / 2f;

        // Snow and hail, each on or off, wherever the player goes.
        float half = (wide - 6f) / 2f;
        if (GameText.Button(new Rect(left, panel.y + 90f, half, 30f), "Snow: " + (weather.Snowing > 0.05f ? "On" : "Off"), buttonStyle)) weather.ToggleSnow();
        if (GameText.Button(new Rect(left + half + 6f, panel.y + 90f, half, 30f), "Hail: " + (weather.Hailing > 0.05f ? "On" : "Off"), buttonStyle)) weather.ToggleHail();

        // Day or night: the button says which it is now, and jumps to the middle of the other.
        bool night = dayNight.time >= 0.5f;
        if (GameText.Button(new Rect(left, panel.y + 126f, wide, 30f), night ? "Night  (switch to day)" : "Day  (switch to night)", buttonStyle)) dayNight.time = night ? Noon : Midnight;
    }
}
