using UnityEngine;

// Taivan's day and night themes for Planet 1 (in Assets/Resources/Music). The day theme plays by day and the night
// theme by night, each over and over. When it gets dark or light, one fades out as the other fades in.
public class Music : MonoBehaviour
{
    // How loud the music plays, from 0 to 1, and how many seconds the fade from one theme to the other takes.
    const float Volume = 0.7f;
    const float FadeSeconds = 4f;

    DayNight dayNight;
    AudioSource day, night;

    public void Setup(DayNight dayNight)
    {
        this.dayNight = dayNight;
        day = MakeSource("Music/Planet 1 Day Theme");
        night = MakeSource("Music/Planet 1 Night Theme");
        // The game starts on whichever theme suits the time of day, already at full volume.
        AudioSource now = IsNight ? night : day;
        if (now != null)
        {
            now.volume = Volume;
            now.Play();
        }
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
        Fade(day, !isNight);
        Fade(night, isNight);
    }

    // Turns a theme up toward full volume if it should be playing, or down to silence if not. A theme starts again
    // from the beginning each time it comes back.
    void Fade(AudioSource source, bool wanted)
    {
        if (source == null) return;
        float step = Volume * Time.deltaTime / FadeSeconds;
        if (wanted)
        {
            if (!source.isPlaying) source.Play();
            source.volume = Mathf.MoveTowards(source.volume, Volume, step);
        }
        else if (source.isPlaying)
        {
            source.volume = Mathf.MoveTowards(source.volume, 0f, step);
            if (source.volume <= 0f) source.Stop();
        }
    }
}
