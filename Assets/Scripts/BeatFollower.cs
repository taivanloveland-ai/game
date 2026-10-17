using System.Collections.Generic;
using UnityEngine;

// Listens for the drum hits in the music: the low notes jumping up, each hit counted once (the low notes have to fall
// back before another can count). In the night theme that is its big drum, about every two and a half seconds.
public class BeatFollower
{
    // A hit is the low notes, as heard over the last instant, this many times as loud as over the last moment, at
    // most one this many seconds. The low notes have to fall back to the second share of the moment's loudness
    // before another hit can count.
    const float HitJump = 1.12f, Rearm = 1.02f, HitsApart = 0.15f;
    // Hits are remembered for this many seconds, for checking.
    const float RemembersSeconds = 10f;

    readonly List<float> hits = new List<float>();
    float instant, moment, lastHit = -10f;
    bool armed = true;

    // The hits of the last few seconds, for checking.
    public IReadOnlyList<float> Hits => hits;
    public int HitsHeard => hits.Count;

    // Listens to the low notes at a moment in time. Returns true if a drum hit comes now.
    public bool Hear(float bass, float time, float dt)
    {
        instant = Mathf.Lerp(instant, bass, Mathf.Clamp01(dt * 25f));
        moment = Mathf.Lerp(moment, bass, Mathf.Clamp01(dt * 3f));
        if (instant < moment * Rearm) armed = true;
        if (!armed || instant <= moment * HitJump || instant <= 0.005f || time - lastHit < HitsApart) return false;
        armed = false;
        lastHit = time;
        hits.Add(time);
        while (hits.Count > 0 && hits[0] < time - RemembersSeconds) hits.RemoveAt(0);
        return true;
    }

    // Starts afresh, as when the song changes.
    public void Forget()
    {
        hits.Clear();
        armed = true;
    }
}
