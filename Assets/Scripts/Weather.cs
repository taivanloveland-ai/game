using System.Collections.Generic;
using UnityEngine;

// The weather. At the start of each day there is a 1 in 2 chance of a cloud of snow, and a 1 in 10 chance of a cloud
// of hail (now and then both). A cloud is about the size of the algae biome. It starts some way off, some time
// during the day, and drifts across the planet in a straight line in a direction picked at random, on a path that
// passes near where the player is. The cloud itself is never seen: only what falls from it, on whoever is under it.
//
// And every fourth day (the 4th, the 8th, and so on) is a snow day: snow falls over the whole planet, all day and all
// night, from one sunrise to the next.
//
// Snow only changes how things look (and brings the ice boss up out of the polar ice). Hail hurts: every hit takes 1 to 5 health, one hit after another, unless the
// player has a block over their head. There is no weather in the caves.
public class Weather : MonoBehaviour
{
    // The chance each day of a cloud of snow, and of a cloud of hail.
    const float SnowChance = 0.5f, HailChance = 0.1f;
    // Every this many days is a snow day.
    const int SnowDayEvery = 4;
    // A cloud is this many squares from its middle to its edge (the algae biome is about the same), and what falls
    // from it thins out over this many squares at the edge.
    const float CloudRadius = 280f, CloudEdge = 50f;
    // How fast a cloud drifts, in squares a second: the same as the player walks.
    const float CloudSpeed = 4f;
    // A cloud's edge starts this many squares from the player, and its middle passes within this share of its own
    // radius of where the player was then.
    const float StartsAway = 150f, PassesWithin = 0.5f;
    // A cloud comes at some point in this first share of the day, and is gone once it has drifted this far.
    const float ComesWithin = 0.6f, DriftsFor = 2400f;
    // Hail hits come this many seconds apart, each taking a whole number of health from the first to the second.
    const float HailGapFrom = 0.5f, HailGapTo = 1.1f;
    const int HailDamageFrom = 1, HailDamageTo = 5;
    // How many flakes of snow and stones of hail are on screen under the thick of a cloud.
    const int SnowFlakes = 260, HailStones = 70;
    // Drawn over the dark of night and everything in the world.
    const int Order = 150;

    class Cloud
    {
        public bool Hail;
        // Where its middle is, counted on past the edges of the map as positions in the game are, and which way it drifts.
        public Vector2 Middle, Heading;
        // The time of day it comes at, from 0 to 1, and whether it has; and how far it has drifted.
        public float ComesAt;
        public bool Come;
        public float Drifted;
    }

    class Falling
    {
        public Transform At;
        public SpriteRenderer Picture;
        public Vector2 Place;
        public float Speed, Sideways;
    }

    PlanetOne planet;
    PlayerController player;
    Camera cam;
    DayNight dayNight;
    Health health;
    Hud hud;
    readonly List<Cloud> clouds = new List<Cloud>();
    readonly List<Falling> flakes = new List<Falling>(), stones = new List<Falling>();
    SpriteRenderer haze;
    float lastTime;
    float nextHailAt;
    bool warned;
    int lastDay;

    // Set from creative mode's panel: snow or hail falls on the player wherever they are, cloud or no cloud.
    public bool ForceSnow, ForceHail;

    // How hard it is snowing and hailing on the player right now, from 0 (not at all) to 1.
    public float Snowing { get; private set; }
    public float Hailing { get; private set; }

    public void Setup(PlanetOne planet, PlayerController player, Camera cam, DayNight dayNight, Health health, Hud hud, Sprite square)
    {
        this.planet = planet;
        this.player = player;
        this.cam = cam;
        this.dayNight = dayNight;
        this.health = health;
        this.hud = hud;

        var sky = new GameObject("Weather");
        sky.transform.SetParent(cam.transform, false);
        sky.transform.localPosition = new Vector3(0f, 0f, 10f);
        // A faint whiteness over everything while it snows or hails.
        haze = MakePart(sky.transform, square, Order - 1);
        haze.transform.localScale = new Vector3(80f, 50f, 1f);
        haze.enabled = false;
        for (int i = 0; i < SnowFlakes; i++)
        {
            float size = Random.Range(0.07f, 0.15f);
            // Most flakes are a soft blue-grey, so they show against white snow; some are white, for darker ground.
            Color flake = i % 4 == 0 ? new Color(1f, 1f, 1f, 0.95f) : new Color(0.62f, 0.72f, 0.86f, Random.Range(0.75f, 1f));
            flakes.Add(MakeFalling(sky.transform, square, new Vector3(size, size, 1f), flake, Random.Range(1.4f, 3.6f), Random.Range(-1f, -0.2f)));
        }
        for (int i = 0; i < HailStones; i++)
        {
            stones.Add(MakeFalling(sky.transform, square, new Vector3(0.07f, Random.Range(0.16f, 0.28f), 1f), new Color(0.5f, 0.66f, 0.86f, 0.95f), Random.Range(11f, 16f), Random.Range(-2f, -1f)));
        }

        lastTime = dayNight.time;
        lastDay = dayNight.Day;
        RollTheDay(dayNight.time);
    }

    static SpriteRenderer MakePart(Transform parent, Sprite sprite, int order)
    {
        var go = new GameObject("Weather Part");
        go.transform.SetParent(parent, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    Falling MakeFalling(Transform parent, Sprite sprite, Vector3 size, Color color, float speed, float sideways)
    {
        SpriteRenderer picture = MakePart(parent, sprite, Order);
        picture.transform.localScale = size;
        picture.color = color;
        picture.enabled = false;
        // Scattered over an area bigger than any screen; they are brought onto the screen as they fall.
        return new Falling { At = picture.transform, Picture = picture, Place = new Vector2(Random.Range(-20f, 20f), Random.Range(-12f, 12f)), Speed = speed, Sideways = sideways };
    }

    // Whether today is a snow day, with snow over the whole planet.
    public bool SnowDay => dayNight.Day % SnowDayEvery == 0;

    // Decides the day's weather. From is the time of day now: a cloud comes some time after that.
    void RollTheDay(float from)
    {
        // On a snow day it is snowing everywhere anyway.
        if (!SnowDay && Random.value < SnowChance) clouds.Add(new Cloud { Hail = false, ComesAt = Random.Range(from, Mathf.Max(from, ComesWithin)) });
        if (Random.value < HailChance) clouds.Add(new Cloud { Hail = true, ComesAt = Random.Range(from, Mathf.Max(from, ComesWithin)) });
    }

    // Brings a cloud in right away, on a path that passes near the player; or, for trying things out, right overhead.
    public void StartCloud(bool hail, bool overhead = false)
    {
        var cloud = new Cloud { Hail = hail };
        clouds.Add(cloud);
        Arrive(cloud);
        if (overhead) cloud.Middle = player.GroundPosition;
    }

    // A cloud arrives: it is put some way from the player, drifting in a direction picked at random along a line that
    // passes near them.
    void Arrive(Cloud cloud)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        cloud.Heading = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        var across = new Vector2(-cloud.Heading.y, cloud.Heading.x);
        cloud.Middle = player.GroundPosition - cloud.Heading * (CloudRadius + StartsAway) + across * (Random.Range(-PassesWithin, PassesWithin) * CloudRadius);
        cloud.Come = true;
        cloud.Drifted = 0f;
    }

    void Update()
    {
        // A new day: yesterday's clouds that never came are forgotten, and the day's weather is decided.
        float time = dayNight.time;
        // (A jump back of half a day is creative mode switching night to day, not a new day.)
        if (time < lastTime - 0.9f)
        {
            clouds.RemoveAll(cloud => !cloud.Come);
            RollTheDay(0f);
        }
        lastTime = time;
        // A snow day has begun (or a saved game was loaded on one).
        if (dayNight.Day != lastDay)
        {
            lastDay = dayNight.Day;
            if (SnowDay) hud.Say("Snow is falling over the whole planet today.", 5f);
        }

        float snow = 0f, hail = 0f;
        Vector2 at = player.GroundPosition;
        for (int i = clouds.Count - 1; i >= 0; i--)
        {
            Cloud cloud = clouds[i];
            if (!cloud.Come)
            {
                if (time >= cloud.ComesAt) Arrive(cloud);
                continue;
            }
            float step = CloudSpeed * Time.deltaTime;
            cloud.Middle += cloud.Heading * step;
            cloud.Drifted += step;
            if (cloud.Drifted > DriftsFor)
            {
                clouds.RemoveAt(i);
                continue;
            }
            // The planet joins up with itself, so the nearest copy of the cloud is the one that counts.
            float away = Vector2.Distance(planet.Nearest(planet.WrapPoint(cloud.Middle), at), at);
            float strength = Mathf.Clamp01((CloudRadius - away) / CloudEdge);
            if (cloud.Hail) hail = Mathf.Max(hail, strength);
            else snow = Mathf.Max(snow, strength);
        }
        if (SnowDay) snow = 1f;
        // Nothing falls in a cave.
        // In creative mode snow and hail can be turned on by hand, wherever the player is.
        if (ForceSnow) snow = 1f;
        if (ForceHail) hail = 1f;
        if (planet.InCave) snow = hail = 0f;
        Snowing = snow;
        Hailing = hail;

        Fall(flakes, snow);
        Fall(stones, hail);
        float whiteness = Mathf.Max(snow * 0.1f, hail * 0.07f);
        haze.enabled = whiteness > 0.005f;
        haze.color = new Color(0.92f, 0.96f, 1f, whiteness);

        HailHurts(hail);
    }

    // Moves what is falling, and shows as much of it as the cloud overhead is thick.
    void Fall(List<Falling> falling, float strength)
    {
        int showing = Mathf.RoundToInt(falling.Count * strength);
        float halfWide = cam.orthographicSize * cam.aspect + 1f, halfTall = cam.orthographicSize + 1f;
        for (int i = 0; i < falling.Count; i++)
        {
            Falling one = falling[i];
            one.Picture.enabled = i < showing;
            if (i >= showing) continue;
            one.Place += new Vector2(one.Sideways, -one.Speed) * Time.deltaTime;
            // Off the bottom or the side, it comes back in at the top or the other side.
            one.Place.x = Mathf.Repeat(one.Place.x + halfWide, halfWide * 2f) - halfWide;
            one.Place.y = Mathf.Repeat(one.Place.y + halfTall, halfTall * 2f) - halfTall;
            one.At.localPosition = one.Place;
        }
    }

    // Under a cloud of hail, the player is hit again and again unless there is a block over their head. The thinner
    // the hail, the longer between hits.
    void HailHurts(float hail)
    {
        if (hail <= 0.05f)
        {
            nextHailAt = 0f;
            warned = false;
            return;
        }
        if (nextHailAt == 0f) nextHailAt = Time.time + Random.Range(HailGapFrom, HailGapTo) / hail;
        if (Time.time < nextHailAt) return;
        nextHailAt = Time.time + Random.Range(HailGapFrom, HailGapTo) / hail;

        Vector2Int square = Vector2Int.FloorToInt(player.GroundPosition);
        if (planet.IsTunnel(square.x, square.y, Mathf.FloorToInt(player.FeetHeight + 0.01f))) return;
        if (!warned)
        {
            warned = true;
            hud.Say("Hail! Get under a block.", 4f);
        }
        health.Hurt(Random.Range(HailDamageFrom, HailDamageTo + 1));
    }
}
