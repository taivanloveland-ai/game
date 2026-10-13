using UnityEngine;

// Creative mode's extra control: a slider at the top left, under the health bar, for how fast the player moves.
// (Creative mode's endless blocks, its tools and its fully drawn map are set up when the game starts; see PlanetOne.)
public class CreativeTools : MonoBehaviour
{
    // The slowest and fastest the slider goes, in squares a second. Survival mode's walking speed is 4.
    const float Slowest = 1f, Fastest = 40f;

    PlayerController player;
    GUIStyle labelStyle;

    public void Setup(PlayerController player)
    {
        this.player = player;
        GameInput.PointerOverTools = PointerOverSlider;
    }

    static Rect Panel()
    {
        return new Rect(16f, 78f, 240f, 44f);
    }

    // True while the mouse is over the slider, so dragging it does not also mine or build.
    bool PointerOverSlider()
    {
        Vector2 pointer = GameInput.PointerPosition();
        return Panel().Contains(new Vector2(pointer.x, Screen.height - pointer.y));
    }

    void OnGUI()
    {
        if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        Rect panel = Panel();
        GUI.Box(panel, GUIContent.none);
        GUI.Label(new Rect(panel.x + 8f, panel.y + 3f, panel.width - 16f, 20f), "Speed: " + Mathf.RoundToInt(player.speed), labelStyle);
        player.speed = Mathf.Round(GUI.HorizontalSlider(new Rect(panel.x + 8f, panel.y + 25f, panel.width - 16f, 14f), player.speed, Slowest, Fastest));
    }
}
