using UnityEngine;

// Taivan's title theme (Assets/Resources/Music/Title Theme). It plays over and over on the title screen and the
// loading screen. It fades in when Play is pressed (asked 2026-11-06: "fade in music in play"), and fades away when
// the game begins and the planet's own music comes in. How loud it is is the music volume in the settings.
public class TitleMusic : MonoBehaviour
{
    // How many seconds it takes to fade away once the game has begun.
    const float FadeSeconds = 3f;
    // How many seconds it takes to fade in from silence when it starts.
    const float FadeInSeconds = 3f;

    AudioSource source;
    float level;
    bool ending;

    // Starts the title theme playing, silent at first, on an object of its own. Gives null if there is no title theme.
    public static TitleMusic Play()
    {
        var clip = Resources.Load<AudioClip>("Music/Title Theme");
        if (clip == null) return null;
        var music = new GameObject("Title Music").AddComponent<TitleMusic>();
        music.source = music.gameObject.AddComponent<AudioSource>();
        music.source.clip = clip;
        music.source.loop = true;
        music.source.volume = 0f;
        music.source.Play();
        return music;
    }

    // Lets the theme fade away, after which it is gone.
    public void End()
    {
        ending = true;
    }

    void Update()
    {
        if (ending) level = Mathf.MoveTowards(level, 0f, Time.unscaledDeltaTime / FadeSeconds);
        else level = Mathf.MoveTowards(level, 1f, Time.unscaledDeltaTime / FadeInSeconds);
        source.volume = level * GameSettings.MusicVolume;
        if (ending && level <= 0f) Destroy(gameObject);
    }
}
