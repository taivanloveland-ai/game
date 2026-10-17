using UnityEngine;

// The battery the ship needs (Taivan's picture, Assets/Resources/Art/Battery). It lies on the South Pole's snowy cap,
// due south of the crash site, right by where the snow gives way to the freezing ocean. Walking over it picks it up.
// Once taken it is gone for good, in saved games too.
public class SouthPoleBattery : MonoBehaviour
{
    // It lies this many squares in from the edge of the cap, and is picked up from this close.
    const float InFromEdge = 1.5f, PickedUpWithin = 0.8f;

    PlanetOne planet;
    PlayerController player;
    MiningMode mining;
    Inventory inventory;
    CraftingNook nook;
    Hud hud;
    SpriteRenderer picture;
    Vector2 spot;

    // Whether the player has picked it up.
    public bool Taken;

    public void Setup(PlanetOne planet, PlayerController player, MiningMode mining, Inventory inventory, CraftingNook nook, Hud hud)
    {
        this.planet = planet;
        this.player = player;
        this.mining = mining;
        this.inventory = inventory;
        this.nook = nook;
        this.hud = hud;
        int x = planet.CrashSite.x;
        spot = new Vector2(x + 0.5f, planet.SouthCapEdge(x) - InFromEdge);
        if (nook.Battery == null) return;
        var go = new GameObject("Battery");
        go.transform.SetParent(planet.TopDownRoot, false);
        picture = go.AddComponent<SpriteRenderer>();
        picture.sprite = nook.Battery.Icon;
        // Sorted by its foot, so the player walks in front of it or behind it correctly.
        picture.spriteSortPoint = SpriteSortPoint.Pivot;
    }

    void Update()
    {
        if (picture == null) return;
        bool shown = !Taken && !planet.InCave && !mining.Active;
        picture.enabled = shown;
        if (!shown) return;
        // The planet joins up with itself, so it is drawn at its copy nearest the player.
        Vector2 at = planet.Nearest(spot, player.GroundPosition);
        // Its picture is centred, so it is raised a little to sit on the snow.
        picture.transform.position = at + new Vector2(0f, 0.3f);
        if (Vector2.Distance(player.GroundPosition, at) > PickedUpWithin || player.FeetHeight > 0.6f) return;
        Taken = true;
        inventory.Add(nook.Battery);
        hud.Say("You found the battery!", 3f);
    }

    // For checking: where it lies.
    public Vector2 Spot => spot;
}
