using UnityEngine;

// Listens to one of the themes as it plays, and then turns it down to the music volume in the settings. Since it
// listens first, the algae can feel the music's beat however loud or quiet the music is set, even all the way down.
// It keeps track of how loud the music is, and how loud its low notes are, where the drums boom.
public class MusicEar : MonoBehaviour
{
    // The low notes are what comes through a gentle filter that lets through only sounds below about this many
    // vibrations a second.
    const float LowNotesBelow = 150f;

    readonly object gate = new object();
    double sum, bassSum;
    int count;
    float lowPass;
    float lowPassBlend;
    float level, bass;
    // The music volume in the settings, from 0 to 1, as the sound system last heard it.
    volatile float volume = 1f;

    void Awake()
    {
        lowPassBlend = 1f - Mathf.Exp(-2f * Mathf.PI * LowNotesBelow / AudioSettings.outputSampleRate);
    }

    // Called by the sound system, away from the game, with each little piece of the music on its way out: it is
    // listened to, and then turned down to the music volume.
    void OnAudioFilterRead(float[] data, int channels)
    {
        double total = 0.0, lows = 0.0;
        for (int i = 0; i < data.Length; i += channels)
        {
            float sample = data[i];
            total += sample * sample;
            lowPass += (sample - lowPass) * lowPassBlend;
            lows += lowPass * lowPass;
        }
        float turnedTo = volume;
        for (int i = 0; i < data.Length; i++) data[i] *= turnedTo;
        lock (gate)
        {
            sum += total;
            bassSum += lows;
            count += data.Length / channels;
        }
    }

    void Update()
    {
        volume = GameSettings.MusicVolume;
        lock (gate)
        {
            if (count == 0) return;
            level = (float)System.Math.Sqrt(sum / count);
            bass = (float)System.Math.Sqrt(bassSum / count);
            sum = bassSum = 0.0;
            count = 0;
        }
    }

    // How loud the music is right now, and how loud its low notes are, as they would sound at full volume.
    public float Level => level;
    public float Bass => bass;
}
