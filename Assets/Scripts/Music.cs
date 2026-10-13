using UnityEngine;

// Taivan's day and night themes for Planet 1 (in Assets/Resources/Music). The day theme plays by day and the night
// theme by night, each over and over. When it gets dark or light, one fades out as the other fades in.
// How loud they are is the music volume in the settings.
public class Music : MonoBehaviour
{
    // How many seconds the fade from one theme to the other takes.
    const float FadeSeconds = 4f;

    DayNight dayNight;
    AudioSource day, night;
    // How far each theme is faded in, from 0 to 1. What is heard is this times the music volume in the settings.
    float dayLevel, nightLevel;

    public void Setup(DayNight dayNight)
    {
        this.dayNight = dayNight;
        day = MakeSource("Music/Planet 1 Day Theme");
        night = MakeSource("Music/Planet 1 Night Theme");
        // The game starts on whichever theme suits the time of day, which fades in as the title theme fades away.
        Update();
    }

    // It counts as night from halfway through dusk to halfway through dawn, as the sky gets dark.
    bool IsNight => dayNight.Night >= 0.5f;

    AudioSource MakeSource(string path)
    {
        var clip = Resources.Load<AudioClip>(path);
        if (clip == null) return null;
        var source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f;
        return source;
    }

    void Update()
    {
        bool isNight = IsNight;
        dayLevel = Fade(day, dayLevel, !isNight);
        nightLevel = Fade(night, nightLevel, isNight);
    }

    // Turns a theme up toward full if it should be playing, or down to silence if not. A theme starts again from the
    // beginning each time it comes back. The fade goes on while the game is paused, so the volume slider on the pause
    // screen can be heard.
    float Fade(AudioSource source, float level, bool wanted)
    {
        if (source == null) return level;
        level = Mathf.MoveTowards(level, wanted ? 1f : 0f, Time.unscaledDeltaTime / FadeSeconds);
        if (wanted && !source.isPlaying) source.Play();
        else if (!wanted && level <= 0f && source.isPlaying) source.Stop();
        source.volume = level * GameSettings.MusicVolume;
        return level;
    }
}
