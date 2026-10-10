using UnityEngine;

// A little nook in the side of the crashed ship, with a hammer and a wrench hanging in it. Right clicking it walks
// the player over and opens a small crafting menu. For now it has one recipe: 8 ferns and 5 ice shards make an ice pickaxe.
// The nook is on the ship's front, so it is used from the top-down view.
public class CraftingNook : MonoBehaviour
{
    const int FernsNeeded = 8;
    const int ShardsNeeded = 5;
    // The player has to be within a block of the nook to use it.
    const float Reach = 1.6f;
    // The walk to the nook is given up if the player stops making progress for this long.
    const float StuckSeconds = 1.5f;

    static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    static readonly Color32 Frame = new Color32(150, 158, 170, 255), Rail = new Color32(104, 112, 124, 255), Recess = new Color32(30, 34, 42, 255);
    static readonly Color32 HammerHead = new Color32(176, 182, 190, 255), Handle = new Color32(140, 100, 62, 255), WrenchMetal = new Color32(190, 196, 204, 255);
    static readonly Color32 IceLight = new Color32(206, 235, 248, 255), IceDark = new Color32(108, 170, 210, 255), Binding = new Color32(58, 182, 162, 255);
    // The nook, one text row per pixel row from the top: F frame, R the rail the tools hang from, d the dark recess,
    // s the cords, M the hammer's head, h its handle, W the wrench.
    static readonly string[] NookPicture =
    {
        "FFFFFFFFFFFFFFFF",
        "FRRRRRRRRRRRRRRF",
        "FdddsddddddsdddF",
        "FdMMMMMdddWdWddF",
        "FdMMMMMdddWdWddF",
        "FdddhdddddWWWddF",
        "FdddhddddddWdddF",
        "FdddhddddddWdddF",
        "FdddhddddddWdddF",
        "FdddhddddddWdddF",
        "FdddhddddddWdddF",
        "FdddhdddddWWWddF",
        "FdddddddddWdWddF",
        "FdddddddddWWWddF",
        "FddddddddddddddF",
        "FFFFFFFFFFFFFFFF",
    };
    // The ice pickaxe: I the dark side of the ice head, i its lit side, H the handle, B the fern binding.
    static readonly string[] PickaxePicture =
    {
        "................",
        ".....IIIIII.....",
        "...IIiiiiiiII...",
        "..Ii....BBiiiI..",
        ".Ii....HHB..IiI.",
        ".I....HHB....II.",
        ".....HHB......I.",
        "....HHB.......I.",
        "...HHB..........",
        "..HHB...........",
        ".HHB............",
        ".HH.............",
        "................",
        "................",
        "................",
        "................",
    };

    // True while the crafting menu is showing.
    public bool Open { get; private set; }
    public Item Pickaxe { get; private set; }

    PlanetOne planet;
    PlayerController player;
    MiningMode mining;
    Gatherables gatherables;
    Inventory inventory;
    Hud hud;
    Camera cam;
    // The square of the ship's front that the nook is set into, and the spot on the ground at its foot.
    Vector2Int cell;
    Vector2 foot;
    bool walking;
    Vector2 lastSpot;
    float lastMovedAt;
    GUIStyle titleStyle, lineStyle, missingStyle;

    public void Setup(PlanetOne planet, PlayerController player, MiningMode mining, Gatherables gatherables, Inventory inventory, Hud hud, Camera cam, Vector2Int cell)
    {
        this.planet = planet;
        this.player = player;
        this.mining = mining;
        this.gatherables = gatherables;
        this.inventory = inventory;
        this.hud = hud;
        this.cam = cam;
        this.cell = cell;
        foot = new Vector2(cell.x + 0.5f, cell.y);

        Pickaxe = new Item("Ice pickaxe", PlanetOne.PaintSprite((x, y) => PickaxePixel(x, y)));

        // The picture sits on the ship's front face, which is drawn one square up from the ground in front of it.
        var go = new GameObject("Crafting Nook");
        go.transform.SetParent(planet.TopDownRoot, false);
        go.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.02f, 0f);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = PlanetOne.PaintSprite(16, 16, new Vector2(0.5f, 0f), (x, y) => NookPixel(x, y));
        // Sorted by its bottom edge, which puts it over the ship and under a player standing in front of it.
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
    }

    static Color32 NookPixel(int x, int y)
    {
        switch (NookPicture[y][x])
        {
            case 'F': return Frame;
            case 'R': case 's': return Rail;
            case 'M': return HammerHead;
            case 'h': return Handle;
            case 'W': return WrenchMetal;
            default: return Recess;
        }
    }

    static Color32 PickaxePixel(int x, int y)
    {
        switch (PickaxePicture[y][x])
        {
            case 'I': return IceDark;
            case 'i': return IceLight;
            case 'H': return Handle;
            case 'B': return Binding;
            default: return Clear;
        }
    }

    bool InReach => Vector2.Distance(player.GroundPosition, foot) <= Reach && player.FeetHeight < 0.5f;

    void Update()
    {
        // The nook is only there in the top-down view.
        if (mining.Active)
        {
            Open = false;
            walking = false;
            return;
        }
        if (GameInput.HarvestPressed()) ClickAt(cam.ScreenToWorldPoint(GameInput.PointerPosition()));
        if (walking) Walk();
        // Walking away or pressing Escape puts the menu away.
        if (Open && (!InReach || GameInput.CancelPressed())) Open = false;
    }

    // Handles a right click at a point on screen (given in world units). If it is on the nook, the player walks over
    // if they need to and the crafting menu opens. Returns false if the click was somewhere else.
    public bool ClickAt(Vector2 point)
    {
        if (point.x < cell.x || point.x > cell.x + 1 || point.y < cell.y || point.y > cell.y + 1) return false;
        gatherables.CancelWalk();
        if (InReach)
        {
            Open = true;
            return true;
        }
        walking = true;
        lastSpot = player.transform.position;
        lastMovedAt = Time.time;
        return true;
    }

    // Steers the player one frame closer to the nook in a straight line, and opens the menu on arrival.
    void Walk()
    {
        // Give up if the player took over the controls or started walking to something else.
        if (player.HasManualInput || gatherables.Walking)
        {
            walking = false;
            return;
        }
        if (InReach)
        {
            walking = false;
            Open = true;
            return;
        }

        Vector2 spot = player.transform.position;
        if (Vector2.Distance(spot, lastSpot) > 0.05f)
        {
            lastSpot = spot;
            lastMovedAt = Time.time;
        }
        else if (Time.time - lastMovedAt > StuckSeconds)
        {
            walking = false;
            return;
        }

        // Aim for the ground just in front of the nook, jumping when the ground just ahead is higher than the player's feet.
        Vector2 heading = (foot + Vector2.down * 0.4f - player.GroundPosition).normalized;
        Vector2 justAhead = player.GroundPosition + heading * 0.55f;
        int topAhead = planet.TopAt(Mathf.FloorToInt(justAhead.x), Mathf.FloorToInt(justAhead.y));
        player.Steer(heading, topAhead <= 1 && topAhead > player.FeetHeight + 0.05f);
    }

    public bool CanCraft => inventory.CountOf(gatherables.PlantItem) >= FernsNeeded && inventory.CountOf(gatherables.ShardItem) >= ShardsNeeded;

    // Uses up the ferns and shards and adds an ice pickaxe to the tools. Returns false if there are not enough.
    public bool Craft()
    {
        if (!CanCraft) return false;
        inventory.Remove(gatherables.PlantItem, FernsNeeded);
        inventory.Remove(gatherables.ShardItem, ShardsNeeded);
        inventory.AddTool(Pickaxe);
        hud.Say("Crafted an ice pickaxe");
        return true;
    }

    void OnGUI()
    {
        if (!Open) return;
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            lineStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            missingStyle = new GUIStyle(lineStyle);
            missingStyle.normal.textColor = new Color(1f, 0.55f, 0.5f);
        }

        const float width = 340f, height = 250f;
        var panel = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        GUI.Box(panel, GUIContent.none);
        GUI.Box(panel, GUIContent.none);
        GUI.Label(new Rect(panel.x, panel.y + 8f, width, 26f), "Ship workbench", titleStyle);
        if (GUI.Button(new Rect(panel.xMax - 34f, panel.y + 6f, 28f, 24f), "X")) Open = false;

        GUI.DrawTexture(new Rect(panel.x + 20f, panel.y + 46f, 56f, 56f), Pickaxe.Icon.texture, ScaleMode.ScaleToFit);
        GUI.Label(new Rect(panel.x + 90f, panel.y + 46f, 220f, 56f), "Ice pickaxe", lineStyle);

        DrawNeed(new Rect(panel.x + 20f, panel.y + 112f, width - 40f, 34f), gatherables.PlantItem, FernsNeeded);
        DrawNeed(new Rect(panel.x + 20f, panel.y + 150f, width - 40f, 34f), gatherables.ShardItem, ShardsNeeded);

        GUI.enabled = CanCraft;
        if (GUI.Button(new Rect(panel.x + 90f, panel.y + 198f, width - 180f, 36f), "Craft")) Craft();
        GUI.enabled = true;
    }

    // One line of the recipe: the item's picture, its name, and how many the player has out of how many are needed.
    void DrawNeed(Rect row, Item item, int needed)
    {
        int have = inventory.CountOf(item);
        GUI.DrawTexture(new Rect(row.x + 8f, row.y + 2f, 30f, 30f), item.Icon.texture, ScaleMode.ScaleToFit);
        GUI.Label(new Rect(row.x + 50f, row.y, row.width - 50f, row.height), item.Name + "   " + have + " / " + needed, have >= needed ? lineStyle : missingStyle);
    }
}
