using UnityEngine;

// The player's health, shown as a bar at the top left. Being hurt takes health away; after a while without being
// hurt it comes back. When it runs out the screen goes dark and the player wakes up at the ship, having dropped
// half of one stack of what they carried, picked at random, on the ground where they fell.
public class Health : MonoBehaviour
{
    public const float Max = 100f;
    // Health starts coming back this long after the last time the player was hurt, at this much a second.
    const float RegenDelay = 5f, RegenPerSecond = 5f;
    // How long the screen takes to go dark and to clear again, and how long after waking nothing can hurt the player.
    const float FadeOutSeconds = 0.7f, FadeInSeconds = 1.1f, SafeSeconds = 3f;
    // Dropped items slide back to the player from this far away, and loose blocks are drawn this small.
    const float DropPullRadius = 2f, BlockScale = 0.35f, ItemScale = 0.8f;
    // The ocean round the South Pole is freezing: being in the water takes this much health every this many seconds.
    const float FreezeDamage = 2f, FreezeSeconds = 1.5f;

    static readonly Color BarBack = new Color(0.08f, 0.09f, 0.12f, 0.8f), BarFill = new Color(0.84f, 0.2f, 0.22f), BarLow = new Color(1f, 0.45f, 0.3f);

    public float Current { get; private set; } = Max;
    // True from the moment health runs out until the player has woken up at the ship.
    public bool Down => fellAt >= 0f;
    // Called just before the player wakes up at the ship, after they have dropped what they lost: brings them up out
    // of a cave if they fell in one.
    public System.Action BeforeWaking;

    PlanetOne planet;
    PlayerController player;
    MiningMode mining;
    Inventory inventory;
    Pickups pickups;
    Hud hud;
    Vector2 home;
    float lastHurtAt = -100f, safeUntil, fellAt = -1f;
    bool woken;
    // How long the player has been in the freezing water since it last hurt them, and whether they were in it last frame.
    float freezing;
    bool wasInWater;
    GUIStyle barStyle;

    public void Setup(PlanetOne planet, PlayerController player, MiningMode mining, Inventory inventory, Pickups pickups, Hud hud, Vector2 home)
    {
        this.planet = planet;
        this.player = player;
        this.mining = mining;
        this.inventory = inventory;
        this.pickups = pickups;
        this.hud = hud;
        this.home = home;
    }

    // Sets the player's health, from a saved game.
    public void Restore(float health)
    {
        Current = Mathf.Clamp(health, 1f, Max);
    }

    // Takes health away. Nothing can hurt the player while they are down or have only just woken up.
    public void Hurt(float amount)
    {
        if (Down || Time.time < safeUntil) return;
        Current = Mathf.Max(0f, Current - amount);
        lastHurtAt = Time.time;
        if (Current > 0f) return;
        fellAt = Time.time;
        woken = false;
    }

    void Update()
    {
        if (Down)
        {
            float since = Time.time - fellAt;
            if (!woken && since >= FadeOutSeconds) WakeAtShip();
            if (since >= FadeOutSeconds + FadeInSeconds) fellAt = -1f;
            return;
        }
        // The freezing ocean.
        if (player.InWater)
        {
            if (!wasInWater) hud.Say("The water is too cold to swim in. You are sinking!", 3f);
            freezing += Time.deltaTime;
            if (freezing >= FreezeSeconds)
            {
                freezing -= FreezeSeconds;
                Hurt(FreezeDamage);
            }
        }
        else freezing = 0f;
        wasInWater = player.InWater;
        if (Current < Max && Time.time - lastHurtAt >= RegenDelay) Current = Mathf.Min(Max, Current + RegenPerSecond * Time.deltaTime);
    }

    void WakeAtShip()
    {
        woken = true;
        string lost = DropHalfAStack();
        BeforeWaking?.Invoke();
        if (mining.Active) mining.Toggle();
        player.Respawn(home);
        Current = Max;
        safeUntil = Time.time + FadeInSeconds + SafeSeconds;
        hud.Say(lost != null ? "You wake up at the ship. You dropped " + lost + " where you fell." : "You wake up at the ship.", 5f);
    }

    // Leaves half of one stack, picked at random, on the ground where the player is. Returns what was dropped, for
    // telling the player, or null if they were carrying nothing.
    string DropHalfAStack()
    {
        if (inventory.Stacks.Count == 0) return null;
        Inventory.Stack stack = inventory.Stacks[Random.Range(0, inventory.Stacks.Count)];
        Item item = stack.Item;
        // Half, rounded up, so a single item is dropped too.
        int count = (stack.Count + 1) / 2;
        inventory.Remove(item, count);
        pickups.Drop(item, player.GroundPosition, player.FeetHeight, DropPullRadius, item.Placeable ? BlockScale : ItemScale, count);
        return count + " " + item.Name;
    }

    void OnGUI()
    {
        if (barStyle == null) barStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

        // The bar sits under the line of text at the top left.
        var bar = new Rect(16f, 50f, 240f, 20f);
        GUI.color = BarBack;
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = Current <= Max * 0.3f ? BarLow : BarFill;
        GUI.DrawTexture(new Rect(bar.x + 2f, bar.y + 2f, (bar.width - 4f) * Current / Max, bar.height - 4f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(bar, "Health " + Mathf.CeilToInt(Current) + " / " + (int)Max, barStyle);

        // A red flash when hurt, and the screen going dark and clearing again when the player falls.
        float flash = 0.28f * Mathf.Clamp01(1f - (Time.time - lastHurtAt) / 0.25f);
        float dark = 0f;
        if (Down)
        {
            float since = Time.time - fellAt;
            dark = since < FadeOutSeconds ? since / FadeOutSeconds : 1f - Mathf.Clamp01((since - FadeOutSeconds) / FadeInSeconds);
        }
        var screen = new Rect(0f, 0f, Screen.width, Screen.height);
        if (flash > 0f && dark < 1f)
        {
            GUI.color = new Color(0.8f, 0.05f, 0.05f, flash);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
        }
        if (dark > 0f)
        {
            GUI.color = new Color(0f, 0f, 0f, dark);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }
}
