using UnityEngine;

// The font all the game's writing is in: Taivan's font (Assets/Resources/Fonts/GameFont), for now. Everything on
// screen that has words — the title screen, the pause screen, the hotbar, the maps, the messages — is drawn with
// the same set of styles, and this gives that set its font. If the font file is missing, the usual font is used.
public class GameFont : MonoBehaviour
{
    Font font;

    void Awake()
    {
        font = Resources.Load<Font>("Fonts/GameFont");
    }

    void OnGUI()
    {
        // Before anything else on screen is drawn.
        GUI.depth = 1000;
        if (font != null && GUI.skin.font != font) GUI.skin.font = font;
    }
}
