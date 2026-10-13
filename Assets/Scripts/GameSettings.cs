using UnityEngine;

// The player's settings, from the pause screen. They are kept on the computer between plays, and are the same
// whichever save slot is being played.
public static class GameSettings
{
    // How close the camera can be set: the number of blocks from the middle of the screen to its top.
    // Smaller is closer. 7 is what the game was made at.
    public const float ClosestZoom = 4f, FurthestZoom = 10f, UsualZoom = 7f;

    // How loud the music is, from 0 (silent) to 1.
    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat("MusicVolume", 0.7f);
        set => PlayerPrefs.SetFloat("MusicVolume", Mathf.Clamp01(value));
    }

    // Whether the mini map in the corner is shown.
    public static bool ShowMiniMap
    {
        get => PlayerPrefs.GetInt("ShowMiniMap", 1) != 0;
        set => PlayerPrefs.SetInt("ShowMiniMap", value ? 1 : 0);
    }

    // How close the camera is on the planet's surface. In a cave it is twice as close again.
    public static float Zoom
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat("Zoom", UsualZoom), ClosestZoom, FurthestZoom);
        set => PlayerPrefs.SetFloat("Zoom", Mathf.Clamp(value, ClosestZoom, FurthestZoom));
    }

    // Whether the game fills the whole screen. (This only does anything in a built copy of the game.)
    public static bool FullScreen
    {
        get => PlayerPrefs.GetInt("FullScreen", 1) != 0;
        set
        {
            PlayerPrefs.SetInt("FullScreen", value ? 1 : 0);
            Screen.fullScreen = value;
        }
    }

    // Writes the settings to the computer straight away.
    public static void Keep()
    {
        PlayerPrefs.Save();
    }
}
